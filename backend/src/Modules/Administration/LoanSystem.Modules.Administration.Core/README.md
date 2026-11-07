# LoanSystem.Modules.Administration.Core

## 1. ¿Para qué sirve?
`LoanSystem.Modules.Administration.Core` es el **Núcleo de Dominio y Aplicación del Módulo de Administración y Configuración Global (`Administration`)**. Dentro de nuestra Arquitectura Hexagonal de Monolito Modular, este proyecto gobierna los parámetros operativos del sistema (`system_parameters`), el catálogo de sucursales/agencias (`branches`), el calendario de días feriados (`holidays` para cálculo de vencimientos en días hábiles), las secuencias correlativas de documentos (`document_sequences`) y las políticas de auditoría (`audit_log`).

---

## 2. ¿Qué hace?

### Responsabilidades actuales y archivos `.cs`

- **`Ports/Outbound/IAdministrationUnitOfWork.cs`**
  - Puerto de salida que hereda de `IUnitOfWork` (`LoanSystem.Common.Core`) para confirmar de forma atómica los cambios en los parámetros del sistema, sucursales y calendarios feriados.

- **`Ports/Outbound/IAdministrationOutbox.cs`**
  - Puerto de salida que hereda de `IOutboxRepository` para publicar eventos de integración en la tabla `public.outbox_messages` dentro de la misma transacción (por ejemplo, cuando cambia un parámetro financiero global o se agrega un feriado nacional que otros módulos deben conocer).

### ¿Qué se agrega aquí conforme crece el módulo?
- **Entidades y Objetos de Valor (`Domain/`):**
  - `SystemParameter` (clave, valor tipado, categoría, indicador de edición permitida), `Branch` (código de sucursal, nombre, ubicación, estado activo), `Holiday` (fecha en calendario peruano, descripción, indicador de feriado recurrente), `DocumentSequence` (prefijo, serie y correlativo actual para contratos, solicitudes y recibos de pago).
- **Puertos de Entrada (`Ports/Inbound/`) y Casos de Uso (`Application/`):**
  - `IGetSystemParametersUseCase`, `IUpdateSystemParameterUseCase`, `IManageHolidaysUseCase`, `IGenerateNextDocumentNumberUseCase`, `IQueryAuditLogUseCase`.
- **Puertos de Salida (`Ports/Outbound/`):**
  - `ISystemParameterRepository`, `IBranchRepository`, `IHolidayCalendarRepository`.

---

## 3. ¿Por qué está diseñado así?

1. **Separación Estricta `.Core` vs `.Adapters`:**
   Aunque la administración maneja tablas de configuración (`database/migrations/01_admin_config.sql`), las reglas de validación (por ejemplo, impedir que un parámetro crítico de tasa máxima o horario de corte tome un valor inválido, o validar que no se registre un feriado duplicado) pertenecen al dominio puro en `.Core` y retornan `Result<T>` sin depender de EF Core ni de ASP.NET Core.
2. **Inmutabilidad del Registro de Auditoría (`audit_log`):**
   El esquema `admin` en PostgreSQL define un trigger que bloquea cualquier `UPDATE` o `DELETE` sobre `audit_log` (*append-only*). `Administration.Core` expone únicamente casos de uso de consulta y verificación sobre la pista de auditoría, garantizando el cumplimiento normativo y antifraude.
3. **Visibilidad `internal` con `InternalsVisibleTo`:**
   Los casos de uso de administración se declaran `internal` y solo son visibles para `LoanSystem.Modules.Administration.Adapters` y los proyectos de pruebas, manteniendo el encapsulamiento verificado por `NetArchTest`.
