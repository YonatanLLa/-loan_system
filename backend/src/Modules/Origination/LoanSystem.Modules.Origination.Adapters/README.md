# LoanSystem.Modules.Origination.Adapters

## 1. ¿Para qué sirve?
`LoanSystem.Modules.Origination.Adapters` es la **Capa de Adaptadores (Entrada y Salida)** del módulo de Originación y Evaluación Crediticia (`Origination`). Conecta el núcleo `LoanSystem.Modules.Origination.Core` con los endpoints HTTP REST bajo `/api/v1/origination` y con la persistencia en PostgreSQL 17 dentro del esquema `origination`.

---

## 2. ¿Qué hace?

### Responsabilidades actuales y archivos `.cs`

- **`OriginationModule.cs`**
  - Clase pública de composición del módulo que expone `AddOriginationModule` (registrando `OriginationDbContext`, `IOriginationUnitOfWork` e `IOriginationOutbox`) y `MapOriginationEndpoints`.

- **`Inbound/Http/OriginationEndpoints.cs`**
  - Adaptador primario HTTP (Minimal APIs) que define el grupo `/api/v1/origination` con la etiqueta OpenAPI `"Origination"`, destinado al registro de solicitudes de crédito, envío a evaluación, dictamen de analistas (`ANALYST`) y resolución de aprobadores (`APPROVER`).

- **`Outbound/Persistence/OriginationDbContext.cs`**
  - Adaptador secundario de persistencia que hereda de `ModuleDbContext`.
  - Configura por defecto el esquema **`origination`** (`modelBuilder.HasDefaultSchema("origination")`), mapeando las tablas creadas en `database/migrations/05_loan_applications.sql` (`loan_applications`, `application_evaluations`, `application_approvals`, `application_status_history`).

### ¿Qué se agrega aquí conforme crece el módulo?
- Endpoints HTTP protegidos con políticas RBAC específicas (`CUSTOMER` o `ANALYST` para crear solicitudes, `ANALYST` para evaluar, `APPROVER` para aprobar/rechazar) y soporte de `Idempotency-Key`.
- Configuraciones EF Core (`IEntityTypeConfiguration<LoanApplication>`) y repositorios en `Outbound/Persistence/`.

---

## 3. ¿Por qué está diseñado así?

1. **Trazabilidad Completa del Comité de Crédito (`origination` schema):**
   Cada cambio de estado y cada dictamen sobre una solicitud queda registrado en el esquema `origination` con auditoría automática (`AuditLogInterceptor`). Al finalizar la aprobación, `OriginationDbContext` guarda el cambio de estado y el mensaje `LoanApplicationApprovedIntegrationEvent` en `public.outbox_messages` en una sola transacción ACID.
2. **Encapsulamiento Modular (`internal`):**
   `OriginationEndpoints` y `OriginationDbContext` son `internal sealed`, asegurando que la comunicación con `Customers`, `Products` y `Loans` respete los límites arquitectónicos validados por `NetArchTest`.
