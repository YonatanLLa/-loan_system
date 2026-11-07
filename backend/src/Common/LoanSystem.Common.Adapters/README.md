# LoanSystem.Common.Adapters

## 1. ¿Para qué sirve?
`LoanSystem.Common.Adapters` es la capa de **Infraestructura Transversal y Adaptadores Compartidos** del Monolito Modular. Mientras que `LoanSystem.Common.Core` define los puertos e interfaces abstractas (`IClock`, `ICurrentUser`, `IUnitOfWork`, `IOutboxRepository`, `Result<T>`), este proyecto provee las **implementaciones técnicas reutilizables** basadas en PostgreSQL (`Npgsql` / `EF Core`), Redis, ASP.NET Core (`HttpContext`, `ProblemDetails`, `IdempotencyMiddleware`) y Quartz.NET.

Su rol en la Arquitectura Hexagonal es evitar la duplicación de código técnico complejo (como el procesador de Outbox con bloqueo pesimista `FOR UPDATE SKIP LOCKED`, el interceptor de auditoría automática o el middleware de idempotencia HTTP) a lo largo de los 12 módulos de negocio.

---

## 2. ¿Qué hace?

### Responsabilidades actuales y archivos `.cs`

#### Reloj y Zona Horaria (`Clock/`)
- **`Clock/LimaTimeZone.cs`**: Resuelve de forma multiplataforma (Windows `SA Pacific Standard Time` y Linux/Docker `America/Lima`) la zona horaria oficial de Perú (UTC-5) y provee métodos de conversión segura desde UTC.
- **`Clock/SystemClock.cs`**: Implementa el puerto `IClock` de `Common.Core`, entregando `UtcNow`, `LimaNow` y `LimaToday` (`DateOnly`).

#### Configuración (`Configuration/`)
- **`Configuration/ConfigurationExtensions.cs`**: Métodos de extensión para leer cadenas de conexión y opciones requeridas desde `IConfiguration` fallando rápido (*fail-fast*) en el arranque si falta alguna variable crítica.

#### Adaptadores HTTP e Idempotencia (`Http/`)
- **`Http/CurrentUserProvider.cs`**: Implementa `ICurrentUser` extrayendo `sub`/`NameIdentifier`, nombre de usuario, roles RBAC y dirección IP desde `IHttpContextAccessor` (o retornando un usuario de sistema cuando se ejecuta desde un Job en background).
- **`Http/ProblemDetailsResultMapper.cs`**: Traduce cualquier `Result` o `Result<T>` fallido en una respuesta HTTP estándar **RFC 7807 (`ProblemDetails`)** mapeando `ErrorType.Validation` → `400 Bad Request`, `ErrorType.Unauthorized` → `401`, `ErrorType.Forbidden` → `403`, `ErrorType.NotFound` → `404`, `ErrorType.Conflict` → `409 Conflict` y `ErrorType.Failure` → `500`.
- **`Http/Idempotency/IIdempotencyStore.cs`**: Contrato para almacenar y consultar llaves de idempotencia (`Idempotency-Key`) junto con el hash SHA-256 del payload, el código de estado HTTP y el cuerpo de la respuesta.
- **`Http/Idempotency/IdempotencyMiddleware.cs`**: Middleware HTTP que intercepta peticiones mutadoras (`POST`, `PUT`, `PATCH`) cuando incluyen la cabecera `Idempotency-Key`.
  - Si la petición está en vuelo concurrentemente, devuelve `409 Conflict`.
  - Si la misma llave se reutiliza con un cuerpo (`body` hash) distinto, devuelve `422 Unprocessable Entity` / `409 Conflict`.
  - Si ya fue procesada exitosamente, devuelve la respuesta cacheada sin re-ejecutar el caso de uso (vital para desembolsos y pagos).
- **`Http/Idempotency/RedisIdempotencyStore.cs` e `InMemoryIdempotencyStore.cs`**: Implementaciones del almacén de idempotencia para producción (Redis distribuido con TTL) y para pruebas/desarrollo local en memoria.
- **`Http/Idempotency/IdempotencyServiceCollectionExtensions.cs`**: Registro en DI del almacenamiento y middleware de idempotencia.

#### Transactional Outbox (`Outbox/`)
- **`Outbox/IntegrationEventTypeRegistry.cs`**: Registro seguro que mapea nombres lógicos de eventos (`event_type`) a sus tipos CLR (`Type`) para serialización/deserialización JSON determinista.
- **`Outbox/OutboxProcessor.cs`**: Motor de despacho que consulta lotes de mensajes pendientes en `public.outbox_messages` usando SQL nativo con **`FOR UPDATE SKIP LOCKED`**, resuelve los `IIntegrationEventHandler<T>` registrados en DI, marca el mensaje como procesado (`processed_at`) o incrementa `retry_count` registrando `last_error` en caso de fallo.
- **`Outbox/OutboxDispatcherJob.cs` y `Outbox/OutboxJobs.cs`**: Job de Quartz (`[DisallowConcurrentExecution]`) que invoca periódicamente (cada 10 segundos en `LoanSystem.Workers`) al `OutboxProcessor`.
- **`Outbox/OutboxServiceCollectionExtensions.cs`**: Métodos de extensión para registrar el procesador y el registro de tipos de eventos.

#### Persistencia y Auditoría (`Persistence/`)
- **`Persistence/ModuleDbContext.cs`**: Clase base abstracta para los 12 `DbContext` modulares (`ProductsDbContext`, `LoansDbContext`, etc.). Implementa `IUnitOfWork` e `IOutboxRepository` de forma nativa para que cualquier módulo pueda guardar sus entidades y encolar mensajes en `outbox_messages` dentro de la misma transacción.
- **`Persistence/OutboxMessage.cs`**: Entidad de persistencia que mapea a la tabla transversal `public.outbox_messages`.
- **`Persistence/AuditLogInterceptor.cs`**: `SaveChangesInterceptor` de EF Core que captura automáticamente las mutaciones de entidades y genera trazas de auditoría (`created_at`, `updated_at`, usuario ejecutor) alineadas con `audit_log`.
- **`Persistence/EfTransactionScope.cs`**: Adaptador de `ITransactionScope` sobre `IDbContextTransaction`.
- **`Persistence/NpgsqlConnectionFactory.cs`**: Fábrica de conexiones `NpgsqlConnection` para consultas de alto rendimiento o bloqueo explícito (`OutboxProcessor`, Dapper/SQL directo).

### ¿Qué se agrega aquí conforme crece el sistema?
- Clientes base de observabilidad (métricas OpenTelemetry personalizadas, enriquecedores de Serilog).
- Adaptadores transversales de almacenamiento de archivos (S3/Blob Storage) o bloqueo distribuido.
- **Nunca** se debe agregar lógica de negocio de ningún módulo específico.

---

## 3. ¿Por qué está diseñado así?

1. **Outbox Transaccional con `FOR UPDATE SKIP LOCKED`:**
   En un sistema financiero, publicar un evento a otro módulo (ej. *"Pago Aplicado"* → actualizar *"Cobranzas"* y enviar *"Notificación"*) nunca debe perderse ni duplicarse si el proceso cae a mitad de camino. Al guardar `OutboxMessage` en el mismo `SaveChangesAsync` del `ModuleDbContext`, garantizamos atomicidad ACID en PostgreSQL. El uso de `FOR UPDATE SKIP LOCKED` permite escalar horizontalmente múltiples instancias de `LoanSystem.Workers` sin colisiones ni condiciones de carrera.
2. **Idempotencia HTTP de Grado Bancario:**
   En redes móviles o integraciones con pasarelas de pago, los reintentos por timeout son frecuentes. `IdempotencyMiddleware` protege todos los endpoints financieros contra cobros o desembolsos duplicados validando la llave y el hash criptográfico del request.
3. **Estandarización de Errores con `ProblemDetails`:**
   En lugar de `try/catch` repetitivos en cada endpoint Minimal API, `ProblemDetailsResultMapper` convierte cualquier `Result<T>` en respuestas HTTP semánticas y uniformes para el frontend y consumidores externos.
