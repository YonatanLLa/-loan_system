# LoanSystem.Modules.Reports.Adapters

## 1. ¿Para qué sirve?
`LoanSystem.Modules.Reports.Adapters` es la **Capa de Adaptadores (Entrada y Salida)** del módulo de Reportes (`Reports`). Conecta el núcleo `LoanSystem.Modules.Reports.Core` con los endpoints HTTP REST bajo `/api/v1/reports` y con la persistencia/proyecciones en PostgreSQL 17 dentro del esquema `reports`.

---

## 2. ¿Qué hace?

### Responsabilidades actuales y archivos `.cs`

- **`ReportsModule.cs`**
  - Clase pública de composición del módulo que expone `AddReportsModule` (registrando `ReportsDbContext`, `IReportsUnitOfWork` e `IReportsOutbox`) y `MapReportsEndpoints`.

- **`Inbound/Http/ReportsEndpoints.cs`**
  - Adaptador primario HTTP (Minimal APIs) que define el grupo `/api/v1/reports` con la etiqueta OpenAPI `"Reports"`, donde se exponen los endpoints de consulta de dashboards, reportes de cartera vencida, colocaciones por analista/sucursal y exportaciones.

- **`Outbound/Persistence/ReportsDbContext.cs`**
  - Adaptador secundario de persistencia que hereda de `ModuleDbContext`.
  - Configura por defecto el esquema **`reports`** (`modelBuilder.HasDefaultSchema("reports")`) para almacenar proyecciones de lectura, vistas materializadas e historial de reportes generados.

### ¿Qué se agrega aquí conforme crece el módulo?
- Endpoints HTTP protegidos con políticas RBAC (`ADMIN`, `APPROVER`, `ANALYST`, `COLLECTOR` según el tipo de reporte).
- Adaptadores de lectura SQL de alto rendimiento utilizando `NpgsqlConnectionFactory` y proyecciones alimentadas por eventos de integración (`Inbound/Events/`) provenientes de `Loans`, `Payments`, `Collections` y `Treasury`.

---

## 3. ¿Por qué está diseñado así?

1. **Desacoplamiento mediante Proyecciones y Esquema Propio (`reports`):**
   Al contar con su propio `ReportsDbContext` y esquema `reports`, el módulo puede mantener tablas desnormalizadas o vistas optimizadas para lectura rápida sin bloquear las tablas transaccionales de `loans` o `payments`.
2. **Encapsulamiento `internal`:**
   `ReportsEndpoints` y `ReportsDbContext` son `internal sealed`, manteniendo `ReportsModule` como la única superficie pública del ensamblado.
