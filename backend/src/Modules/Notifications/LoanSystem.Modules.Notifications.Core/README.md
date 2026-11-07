# LoanSystem.Modules.Notifications.Core

## 1. ¿Para qué sirve?
`LoanSystem.Modules.Notifications.Core` es el **Núcleo de Dominio y Aplicación del Módulo de Notificaciones (`Notifications`)**. En nuestra Arquitectura Hexagonal, este proyecto centraliza la lógica de negocio para la gestión de plantillas de comunicación, programación y envío de avisos multicanal (correo electrónico, SMS, WhatsApp, notificaciones in-app), incluyendo los recordatorios preventivos de pago y las alertas de vencimiento o desembolso.

---

## 2. ¿Qué hace?

### Responsabilidades actuales y archivos `.cs`

- **`Ports/Inbound/ISendDailyRemindersUseCase.cs`**
  - Define el puerto de entrada `ISendDailyRemindersUseCase : IUseCase<SendDailyRemindersResponse>` y su respuesta `SendDailyRemindersResponse(DateOnly ExecutionDateLima, int RemindersQueuedCount)`.

- **`Application/SendDailyRemindersUseCase.cs`**
  - Caso de uso `internal sealed` que coordina la campaña diaria de recordatorios de vencimiento de cuota:
    - Consulta la fecha actual en Perú mediante `_clock.LimaToday`.
    - Identifica los avisos preventivos y recordatorios de pago que corresponden enviarse en el día, los encola para despacho y confirma la operación a través de `INotificationsUnitOfWork`.

- **`Ports/Outbound/INotificationsUnitOfWork.cs` y `Ports/Outbound/INotificationsOutbox.cs`**
  - Puertos de salida para persistencia transaccional de envíos/logs de notificación y publicación de eventos en el Outbox.

### ¿Qué se agrega aquí conforme crece el módulo?
- **Entidades y Objetos de Valor (`Domain/`):**
  - `NotificationTemplate` (plantillas parametrizadas con variables como monto de cuota, fecha de vencimiento y nombre del cliente), `NotificationLog` (registro de cada envío con estado `Pending`, `Sent`, `Failed`, proveedor utilizado y conteo de reintentos), `NotificationChannel` (`Email`, `Sms`, `WhatsApp`).
- **Puertos de Salida de Proveedores (`Ports/Outbound/`):**
  - `IEmailSender`, `ISmsSender`, `INotificationTemplateRepository` (implementados en `Notifications.Adapters` o coordinados con `Integrations`).
- **Manejadores de Eventos de Integración (`Application/`):**
  - Consumidores que reaccionan a eventos del Outbox como `LoanDisbursedIntegrationEvent`, `PaymentAppliedIntegrationEvent` o `InstallmentOverdueIntegrationEvent` para notificar inmediatamente al cliente.

---

## 3. ¿Por qué está diseñado así?

1. **Desacoplamiento Total respecto a los Módulos Financieros:**
   Cuando `Payments` registra un pago o `Origination` aprueba una solicitud, esos módulos no deben bloquear su transacción esperando a que un servidor SMTP o pasarela de SMS responda ( ni deben fallar si el proveedor de correo está caído). En su lugar, publican un evento en el Outbox y `Notifications.Core` lo procesa de forma asíncrona y resiliente.
2. **Respeto de Ventanas Horarias de Contacto (`09:00 America/Lima`):**
   El caso de uso `SendDailyRemindersUseCase` está diseñado para ejecutarse en horario hábil en Perú (09:00 AM), evitando el envío de SMS o correos de cobranza en la madrugada cuando corre el proceso técnico de mora (`00:05 AM`).
