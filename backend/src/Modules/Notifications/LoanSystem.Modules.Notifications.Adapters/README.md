# LoanSystem.Modules.Notifications.Adapters

## 1. ¿Para qué sirve?
`LoanSystem.Modules.Notifications.Adapters` es la **Capa de Adaptadores (Entrada y Salida)** del módulo de Notificaciones (`Notifications`). Conecta el núcleo `LoanSystem.Modules.Notifications.Core` con los endpoints HTTP (`/api/v1/notifications`), el Job diario `DailyRemindersJob` en Quartz.NET y el esquema de base de datos `notifications` en PostgreSQL.

---

## 2. ¿Qué hace?

### Responsabilidades actuales y archivos `.cs`

- **`NotificationsModule.cs`**
  - Clase pública de registro modular (`AddNotificationsModule` y `MapNotificationsEndpoints`). Registra `NotificationsDbContext`, enlaza `INotificationsUnitOfWork` e `INotificationsOutbox`, y registra el caso de uso `ISendDailyRemindersUseCase` → `SendDailyRemindersUseCase`.

- **`Inbound/Http/NotificationsEndpoints.cs`**
  - Define el grupo de rutas Minimal API `/api/v1/notifications` con tag OpenAPI `"Notifications"`, destinado a la administración de plantillas, consulta del historial de notificaciones enviadas a un cliente y reenvío manual de avisos.

- **`Inbound/Jobs/DailyRemindersJob.cs`**
  - Adaptador primario de Quartz (`IJob`, `[DisallowConcurrentExecution]`) que invoca `ISendDailyRemindersUseCase` y registra en los logs la fecha de ejecución en Lima y el número de recordatorios encolados (`RemindersQueuedCount`).

- **`Inbound/Jobs/NotificationsJobs.cs`**
  - Método de extensión `AddNotificationsJobs(this IServiceCollectionQuartzConfigurator quartz)` que agenda `DailyRemindersJob` con la expresión Cron **`0 0 9 * * ?`** (todos los días a las **09:00 AM**) utilizando explícitamente **`LimaTimeZone.Instance` (`America/Lima`)**.

- **`Outbound/Persistence/NotificationsDbContext.cs`**
  - Hereda de `ModuleDbContext` y configura el esquema por defecto **`notifications`** (`modelBuilder.HasDefaultSchema("notifications")`), mapeando las tablas de `database/migrations/09_collections_notifications.sql` (`notification_templates`, `notifications`).

### ¿Qué se agrega aquí conforme crece el módulo?
- Adaptadores de salida hacia proveedores de mensajería (SMTP/SendGrid para Email, Twilio/Infobip para SMS/WhatsApp) implementando las interfaces definidas en `Notifications.Core`.
- Endpoints HTTP para CRUD de plantillas (`notification_templates`) protegidos con política RBAC `ADMIN`.

---

## 3. ¿Por qué está diseñado así?

1. **Trazabilidad y Persistencia Propia (`NotificationsDbContext`):**
   En un sistema crediticio regulado, es indispensable demostrar cuándo y qué se notificó a un cliente (ej. aviso previo de vencimiento o constancia de pago). Al tener su propio esquema `notifications` y su propio `DbContext`, el módulo guarda la auditoría completa de cada intento de envío sin contaminar las tablas de `loans` o `customers`.
2. **Programación Horaria Segura (`09:00` Hora Lima):**
   `NotificationsJobs` vincula el trigger de Quartz a `LimaTimeZone.Instance` para asegurar que los recordatorios salgan puntualmente a las 09:00 AM de Perú independientemente de la zona horaria del servidor host.
