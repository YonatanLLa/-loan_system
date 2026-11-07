# LoanSystem.Modules.Payments.Adapters

## 1. ¿Para qué sirve?
`LoanSystem.Modules.Payments.Adapters` es la **Capa de Adaptadores (Entrada y Salida)** del módulo de Pagos y Libro Mayor (`Payments`). Conecta el núcleo `LoanSystem.Modules.Payments.Core` con los endpoints HTTP REST bajo `/api/v1/payments`, el middleware de idempotencia HTTP y la persistencia en PostgreSQL 17 dentro del esquema `payments`.

---

## 2. ¿Qué hace?

### Responsabilidades actuales y archivos `.cs`

- **`PaymentsModule.cs`**
  - Clase pública de composición del módulo que expone `AddPaymentsModule` (registrando `PaymentsDbContext` con `AuditLogInterceptor`, `IPaymentsUnitOfWork` e `IPaymentsOutbox`) y `MapPaymentsEndpoints`.

- **`Inbound/Http/PaymentsEndpoints.cs`**
  - Adaptador primario HTTP (Minimal APIs) que define el grupo `/api/v1/payments` con la etiqueta OpenAPI `"Payments"`, preparado para recibir cobros en ventanilla (`CASHIER`), pagos digitales (`CUSTOMER`), previsualizaciones de prelación de pago y consultas de recibos y asientos contables.

- **`Outbound/Persistence/PaymentsDbContext.cs`**
  - Adaptador secundario de persistencia que hereda de `ModuleDbContext`.
  - Configura por defecto el esquema **`payments`** (`modelBuilder.HasDefaultSchema("payments")`), mapeando las tablas creadas en `database/migrations/07_payments_ledger.sql` (`payments`, `payment_allocations`, `payment_receipts`, `accounting_entries`).

### ¿Qué se agrega aquí conforme crece el módulo?
- Endpoints HTTP `POST /api/v1/payments` protegidos por `IdempotencyMiddleware` (exigiendo `Idempotency-Key` para impedir cobros duplicados) y políticas RBAC (`CASHIER`, `ADMIN`, `CUSTOMER`).
- Configuraciones EF Core (`IEntityTypeConfiguration<Payment>`, `IEntityTypeConfiguration<AccountingEntry>`) y repositorios en `Outbound/Persistence/`.
- Manejadores de eventos de integración (`Inbound/Events/`) cuando `Integrations` confirme un pago recibido por webhook desde una pasarela externa.

---

## 3. ¿Por qué está diseñado así?

1. **Doble Barrera contra Pagos Duplicados (Idempotencia HTTP + Unicidad SQL):**
   En operaciones de caja o pasarelas móviles, un corte de conexión puede provocar que el cliente o cajero envíe dos veces el mismo pago. `Payments.Adapters` combina el `IdempotencyMiddleware` en la capa HTTP con índices únicos y transacciones atómicas en `PaymentsDbContext` (`payments` schema + `public.outbox_messages`) para garantizar procesamiento *exactly-once* efectivo.
2. **Aislamiento Contable:**
   Ningún otro módulo puede insertar filas directamente en `payments.accounting_entries`; solo `PaymentsDbContext` tiene acceso a dicho esquema.
