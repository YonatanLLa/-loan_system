# LoanSystem.Modules.Loans.Adapters

## 1. ¿Para qué sirve?
`LoanSystem.Modules.Loans.Adapters` es la **Capa de Adaptadores (Entrada y Salida)** del módulo de Préstamos y Cartera Activa (`Loans`). Conecta el núcleo `LoanSystem.Modules.Loans.Core` con los endpoints HTTP REST bajo `/api/v1/loans`, los consumidores de eventos de integración de otros módulos y la base de datos PostgreSQL 17 dentro del esquema `loans`.

---

## 2. ¿Qué hace?

### Responsabilidades actuales y archivos `.cs`

- **`LoansModule.cs`**
  - Clase pública de composición del módulo que expone `AddLoansModule` (registrando `LoansDbContext` con `AuditLogInterceptor`, `ILoansUnitOfWork` e `ILoansOutbox`) y `MapLoansEndpoints`.

- **`Inbound/Http/LoansEndpoints.cs`**
  - Adaptador primario HTTP (Minimal APIs) que define el grupo `/api/v1/loans` con la etiqueta OpenAPI `"Loans"`, donde se exponen las operaciones de consulta de préstamos, visualización de cronogramas de pago vigentes, ejecución de desembolsos (con soporte obligatorio de `Idempotency-Key`) y refinanciamientos.

- **`Outbound/Persistence/LoansDbContext.cs`**
  - Adaptador secundario de persistencia que hereda de `ModuleDbContext`.
  - Configura por defecto el esquema **`loans`** (`modelBuilder.HasDefaultSchema("loans")`), mapeando las tablas creadas en `database/migrations/06_loans_schedules.sql` (`loans`, `loan_schedules`, `loan_installments`, `loan_disbursements`, `loan_refinancings`).

### ¿Qué se agrega aquí conforme crece el módulo?
- Endpoints HTTP protegidos por políticas RBAC (`CASHIER` para desembolsos, `ANALYST`/`APPROVER` para reprogramaciones, `COLLECTOR` para consulta de deuda y `CUSTOMER` para consultar sus propios préstamos).
- Configuraciones EF Core (`IEntityTypeConfiguration<Loan>`, etc.) con control de concurrencia optimista (`xmin` / `version`) e implementaciones de `ILoanRepository`.
- Manejadores de eventos de integración (`Inbound/Events/`) para reaccionar a `ApplicationApprovedIntegrationEvent` (desde `Origination`), `PaymentAppliedIntegrationEvent` (desde `Payments`) y actualizaciones de mora (desde `Collections`).

---

## 3. ¿Por qué está diseñado así?

1. **Protección contra Desembolsos Duplicados (`IdempotencyMiddleware` + Transacciones):**
   El desembolso de un préstamo es una de las operaciones de mayor riesgo financiero. Al exponer los endpoints de mutación en `LoansEndpoints` detrás del `IdempotencyMiddleware` y persistir los cambios junto con el Outbox en `LoansDbContext`, se evita cualquier doble desembolso ante reintentos de red.
2. **Aislamiento de Esquema (`loans`):**
   `LoansDbContext` es dueño exclusivo del esquema `loans` en PostgreSQL, cumpliendo con las reglas de arquitectura verificadas automáticamente en `LoanSystem.Architecture.Tests`.
