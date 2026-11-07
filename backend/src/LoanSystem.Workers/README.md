# LoanSystem.Workers

## 1. ¿Para qué sirve?
`LoanSystem.Workers` es el **Host de Procesamiento en Segundo Plano (*Background Worker Host*) y Raíz de Composición Asíncrona** del sistema. En la Arquitectura Hexagonal, un Job programado (Cron) o un despachador de eventos es simplemente otro **Adaptador Primario (Inbound Adapter)** que dispara un caso de uso del núcleo (`*.Core`), exactamente igual que lo haría una petición HTTP.

Este servicio independiente ejecuta tareas críticas de corte diario, conciliación bancaria, alertas de cobranza y el despacho continuo del **Transactional Outbox** sin afectar la latencia ni los recursos del servidor web (`LoanSystem.WebApi`).

---

## 2. ¿Qué hace?

### Responsabilidades actuales y archivos

- **`Program.cs`**
  - Inicializa el `HostApplicationBuilder` de .NET 10 con Serilog y registra la infraestructura común (`IClock`, `NpgsqlConnectionFactory`, `AuditLogInterceptor`, `AddOutboxInfrastructure`) junto con los 12 módulos de negocio.
  - Configura **Quartz.NET** (`AddQuartz` y `AddQuartzHostedService(options => options.WaitForJobsToComplete = true)`) forzando explícitamente el uso de la zona horaria **`LimaTimeZone.Instance` (`America/Lima`, UTC-5)** para todos los cronogramas financieros:
    1. **`DailyMoraCalculationJob` (`00:05` hora Lima - Cron `0 5 0 * * ?`)**:
       - Registrado por el módulo `Collections` (`AddCollectionsJobs`).
       - Calcula la mora diaria al iniciar el día contable sobre todas las cuotas vencidas (`overdue`) y actualiza los tramos de antigüedad de deuda (*aging buckets*: `1-30`, `31-60`, `61-90`, `90+` días).
    2. **`DailyBalanceJob` (`06:00` hora Lima - Cron `0 0 6 * * ?`)**:
       - Registrado por el módulo `Treasury` (`AddTreasuryJobs`).
       - Ejecuta el cuadre y cierre de saldos diarios de tesorería (`daily_balances`), comparando los desembolsos y recaudaciones registrados contra los movimientos del libro mayor.
    3. **`DailyRemindersJob` (`09:00` hora Lima - Cron `0 0 9 * * ?`)**:
       - Registrado por el módulo `Notifications` (`AddNotificationsJobs`).
       - Envía recordatorios preventivos de vencimiento de cuota y avisos de cobranza en horario hábil (09:00 AM Lima) para cumplir con las buenas prácticas de contacto al cliente.
    4. **`DisbursementsInTransitJob` (Cada 15 minutos - Cron `0 0/15 * * * ?`)**:
       - Registrado por el módulo `Treasury` (`AddTreasuryJobs`).
       - Monitorea y concilia desembolsos bancarios en estado `in_transit` para confirmar su liquidación o detectar rechazos operativos.
    5. **`OutboxDispatcherJob` (Cada 10 segundos - Intervalo simple)**:
       - Registrado por `LoanSystem.Common.Adapters` (`AddOutboxJobs`).
       - Extrae lotes de eventos pendientes en `public.outbox_messages` usando `FOR UPDATE SKIP LOCKED` y ejecuta los `IIntegrationEventHandler<T>` correspondientes.

- **`appsettings.json`**
  - Configuración de conexión a PostgreSQL y niveles de logging del proceso Worker.

### ¿Qué se agrega aquí conforme crece el sistema?
- El registro de nuevos grupos de Jobs modulares (`q.Add<Module>Jobs()`) cuando otro módulo requiera tareas programadas (por ejemplo, generación de reportes regulatorios mensuales en `Reports` o sincronización de catálogos externos en `Integrations`).
- **Nunca** se escribe lógica de negocio directamente en `Program.cs`; cada Job vive dentro de `Inbound/Jobs/` de su respectivo proyecto `*.Adapters` y delega el trabajo a un `IUseCase` en `*.Core`.

---

## 3. ¿Por qué está diseñado así?

1. **Separación de Cargas Operativas (WebApi vs Workers):**
   El cálculo masivo de mora a las 00:05 o el cuadre contable a las 06:00 no deben competir por CPU/memoria con las peticiones HTTP de clientes y cajeros. Al separar `LoanSystem.Workers` en su propio ejecutable, ambos procesos pueden escalar de forma independiente en contenedores Docker/Kubernetes.
2. **Precisión Horaria Financiera (`America/Lima`):**
   Un servidor en la nube suele correr en UTC. Si el cron de mora se ejecutara a las `00:05 UTC` (que son las `19:05` del día anterior en Perú), se cobraría mora indebida a clientes que aún tienen casi 5 horas para pagar su cuota del día. Por ello, todos los `CronSchedule` de Quartz están anclados explícitamente a `LimaTimeZone.Instance`.
3. **Apagado Gracioso (`WaitForJobsToComplete = true`):**
   Durante un despliegue o reinicio del contenedor, Quartz espera a que el lote actual de Outbox o cálculo financiero termine su transacción antes de cerrar el proceso, evitando estados inconsistentes.
