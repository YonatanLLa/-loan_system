# LoanSystem.IntegrationTests

## 1. ¿Para qué sirve?
`LoanSystem.IntegrationTests` es la **Suite de Pruebas de Integración con Infraestructura Real (`Testcontainers`)**. En un sistema financiero SQL-First sobre **PostgreSQL 17**, usar bases de datos falsas en memoria (*EF Core In-Memory* o *SQLite*) en los tests de integración es peligroso porque no validan tipos `ENUM` nativos, funciones PL/pgSQL, triggers de inmutabilidad (`audit_log` / `accounting_entries`), restricciones `CHECK` sobre `NUMERIC(18,2)` ni bloqueos concurrentes de fila como **`FOR UPDATE SKIP LOCKED`**.

Este proyecto levanta contenedores efímeros reales de **PostgreSQL 17 (`postgres:17-alpine`)** mediante **Testcontainers for .NET** para verificar de extremo a extremo las migraciones SQL, la persistencia transaccional y el motor de **Transactional Outbox**.

---

## 2. ¿Qué hace?

### Responsabilidades actuales y archivos `.cs`

- **`DatabaseMigrationIntegrationTests.cs`**
  - Levanta un contenedor PostgreSQL 17 limpio con `PostgreSqlBuilder`.
  - Ejecuta `DatabaseMigrator.Migrate(connectionString, sqlRoot)` (`LoanSystem.DbMigrator`) aplicando los 15 scripts SQL de `database/` (`00_extensions_and_enums.sql` hasta `13_outbox_messages.sql`).
  - Verifica contra el catálogo `information_schema` y `pg_tables` de PostgreSQL que:
    - Todas las tablas críticas de dominio existan (`loans`, `loan_schedules`, `payments`, `accounting_entries`, `audit_log`, `outbox_messages`, etc.).
    - La migración sea **idempotente** (ejecutar `DatabaseMigrator.Migrate` una segunda vez sobre la misma base de datos retorna éxito sin volver a correr scripts ya registrados en `schemaversions`).
    - Los **triggers de inmutabilidad** funcionen en la base de datos real (por ejemplo, verifica que intentar ejecutar un `DELETE` o `UPDATE` sobre `audit_log` o `accounting_entries` sea bloqueado por PostgreSQL).

- **`OutboxIntegrationTests.cs`**
  - Verifica el ciclo completo del patrón **Transactional Outbox** contra PostgreSQL 17 real:
    - Inserta un evento de integración (`IIntegrationEvent`) en la tabla `public.outbox_messages`.
    - Ejecuta `OutboxProcessor.ProcessPendingMessagesAsync(...)`, el cual utiliza la consulta nativa `SELECT ... FROM public.outbox_messages WHERE processed_at IS NULL ORDER BY occurred_on_utc LIMIT @batchSize FOR UPDATE SKIP LOCKED`.
    - Comprueba que el `IIntegrationEventHandler<T>` registrado en DI reciba el evento deserializado exactamente con sus mismos datos y que la fila en PostgreSQL quede marcada con `processed_at IS NOT NULL` y `last_error IS NULL`.

### ¿Qué se agrega aquí conforme crece el sistema?
- Pruebas de integración de flujos completos de negocio usando `WebApplicationFactory<Program>`:
  - Flujo `Origination` (aprobar solicitud) → `Outbox` → `Loans` (crear préstamo y cronograma Francés) → `Payments` (aplicar pago con `Idempotency-Key` y verificar asiento en `accounting_entries`).
  - Pruebas de concurrencia con múltiples instancias de `OutboxProcessor` corriendo en paralelo sobre el mismo contenedor PostgreSQL para demostrar que `FOR UPDATE SKIP LOCKED` nunca procesa el mismo mensaje dos veces.

---

## 3. ¿Por qué está diseñado así?

1. **Fidelidad Total con Producción (`postgres:17-alpine` en Testcontainers):**
   Cada corrida de `LoanSystem.IntegrationTests` crea una instancia real y aislada de PostgreSQL 17 en Docker y la destruye automáticamente al terminar (`IAsyncLifetime`). Así eliminamos el clásico problema de *"en mis mocks pasaba pero en PostgreSQL falló la constraint SQL"*.
2. **Validación del Bloqueo Pesimista (`FOR UPDATE SKIP LOCKED`):**
   La cláusula `FOR UPDATE SKIP LOCKED` es específica de motores relacionales avanzados como PostgreSQL y no puede simularse en memoria. Probar `OutboxProcessor` aquí certifica que el despacho asíncrono entre los 12 módulos del monolito modular es 100% seguro bajo alta concurrencia.
