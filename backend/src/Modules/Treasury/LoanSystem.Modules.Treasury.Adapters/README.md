# LoanSystem.Modules.Treasury.Adapters

## 1. ¿Para qué sirve?
`LoanSystem.Modules.Treasury.Adapters` es la **Capa de Adaptadores (Entrada y Salida)** del módulo de Tesorería (`Treasury`). Conecta los casos de uso de `LoanSystem.Modules.Treasury.Core` con los endpoints HTTP REST (`/api/v1/treasury`), los dos Jobs programados de Quartz.NET (`DailyBalanceJob` y `DisbursementsInTransitJob`) y la persistencia en PostgreSQL bajo el esquema `treasury`.

---

## 2. ¿Qué hace?

### Responsabilidades actuales y archivos `.cs`

- **`TreasuryModule.cs`**
  - Clase pública que registra `TreasuryDbContext`, los puertos de salida (`ITreasuryUnitOfWork`, `ITreasuryOutbox`) y los casos de uso de entrada (`IRunDailyBalanceUseCase`, `ICheckDisbursementsInTransitUseCase`) en el contenedor de inyección de dependencias, además de exponer `MapTreasuryEndpoints`.

- **`Inbound/Http/TreasuryEndpoints.cs`**
  - Adaptador primario HTTP que define el grupo `/api/v1/treasury` con tag OpenAPI `"Treasury"` para consultas de saldos diarios, conciliaciones bancarias y estado de desembolsos.

- **`Inbound/Jobs/DailyBalanceJob.cs`**
  - Job de Quartz (`IJob`, `[DisallowConcurrentExecution]`) que invoca `IRunDailyBalanceUseCase` y registra en el log estructurado la fecha de balance en Lima, el total desembolsado, el total recaudado y el indicador `IsBalanced`.

- **`Inbound/Jobs/DisbursementsInTransitJob.cs`**
  - Job de Quartz (`IJob`, `[DisallowConcurrentExecution]`) que invoca `ICheckDisbursementsInTransitUseCase` para verificar y actualizar los desembolsos en tránsito cada 15 minutos.

- **`Inbound/Jobs/TreasuryJobs.cs`**
  - Extensión `AddTreasuryJobs(this IServiceCollectionQuartzConfigurator quartz)` que programa ambos trabajos usando la zona horaria **`LimaTimeZone.Instance` (`America/Lima`)**:
    - `DailyBalanceJob`: Cron **`0 0 6 * * ?`** (todos los días a las **06:00 AM** hora Lima).
    - `DisbursementsInTransitJob`: Cron **`0 0/15 * * * ?`** (cada **15 minutos**).

- **`Outbound/Persistence/TreasuryDbContext.cs`**
  - Hereda de `ModuleDbContext` y aisla las tablas del módulo bajo el esquema **`treasury`** (`modelBuilder.HasDefaultSchema("treasury")`), correspondiente a `database/migrations/08_treasury_reconciliation.sql`.

### ¿Qué se agrega aquí conforme crece el módulo?
- Endpoints HTTP protegidos por roles (`CASHIER`, `ADMIN`) para consultar cierres de caja/tesorería y ejecutar conciliaciones.
- Repositorios concretos de persistencia para las tablas `daily_balances` y `bank_reconciliations`.
- Consumidores de eventos de integración (`IIntegrationEventHandler`) provenientes de `Loans` (desembolsos emitidos) y `Payments` (pagos recibidos).

---

## 3. ¿Por qué está diseñado así?

1. **Adaptadores de Entrada Múltiples sobre el Mismo Núcleo:**
   Tanto `TreasuryEndpoints` (HTTP) como `DailyBalanceJob` y `DisbursementsInTransitJob` (Quartz) viven en `Inbound/` porque ambos son adaptadores primarios que conducen la aplicación a través de sus puertos `Ports/Inbound/`. Esto permite disparar un cuadre de tesorería tanto automáticamente a las 06:00 AM como bajo demanda vía API REST sin duplicar una sola línea de negocio.
2. **Aislamiento de Base de Datos (`treasury` schema):**
   `TreasuryDbContext` gestiona únicamente el esquema `treasury`, respetando las reglas verificadas por `LoanSystem.Architecture.Tests`.
