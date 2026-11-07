# LoanSystem.Modules.Administration.Adapters

## 1. ¿Para qué sirve?
`LoanSystem.Modules.Administration.Adapters` es la **Capa de Adaptadores (Entrada y Salida)** del módulo de Administración (`Administration`). Conecta el núcleo `LoanSystem.Modules.Administration.Core` con la API HTTP bajo `/api/v1/administration` (orientada principalmente al rol RBAC `ADMIN`) y con la base de datos PostgreSQL 17 en el esquema `admin`.

---

## 2. ¿Qué hace?

### Responsabilidades actuales y archivos `.cs`

- **`AdministrationModule.cs`**
  - Clase estática pública que actúa como punto de registro del módulo para el host:
    - `AddAdministrationModule(this IServiceCollection services, IConfiguration configuration)`: Registra `AdministrationDbContext` con la cadena de conexión a PostgreSQL y el interceptor `AuditLogInterceptor`, y enlaza los puertos `IAdministrationUnitOfWork` e `IAdministrationOutbox`.
    - `MapAdministrationEndpoints(this IEndpointRouteBuilder endpoints)`: Registra las rutas HTTP del módulo.

- **`Inbound/Http/AdministrationEndpoints.cs`**
  - Adaptador primario HTTP (Minimal APIs) que agrupa los endpoints bajo `/api/v1/administration` con la etiqueta OpenAPI `"Administration"`, incluyendo el endpoint `/api/v1/administration/ping` y el espacio para los endpoints de gestión de parámetros, sucursales, feriados y consulta de auditoría.

- **`Outbound/Persistence/AdministrationDbContext.cs`**
  - Adaptador secundario de persistencia que hereda de `ModuleDbContext`.
  - Configura el esquema por defecto **`admin`** (`modelBuilder.HasDefaultSchema("admin")`), el cual aloja las tablas definidas en `database/migrations/01_admin_config.sql` (`system_parameters`, `branches`, `holidays`, `document_sequences`, `audit_log`).

### ¿Qué se agrega aquí conforme crece el módulo?
- En `Inbound/Http/`: Endpoints protegidos con `.RequireAuthorization("ADMIN")` y soporte de `Idempotency-Key` para actualizar parámetros del sistema, dar de alta sucursales, gestionar el calendario de feriados y consultar el `audit_log`.
- En `Outbound/Persistence/`: Configuraciones `IEntityTypeConfiguration<T>` y repositorios concretos que implementen los puertos de `Administration.Core`.

---

## 3. ¿Por qué está diseñado así?

1. **Aislamiento de Esquema (`admin`):**
   `AdministrationDbContext` opera exclusivamente sobre el esquema `admin` en PostgreSQL. Ningún otro módulo puede modificar directamente `admin.system_parameters` o `admin.holidays`; cualquier sincronización hacia otros módulos se realiza mediante contratos explícitos o eventos de integración.
2. **Encapsulamiento Verificado por `NetArchTest`:**
   Tanto `AdministrationEndpoints` como `AdministrationDbContext` son `internal sealed`. Únicamente `AdministrationModule` es `public`, impidiendo que otros módulos referencien clases internas de persistencia o rutas de `Administration`.
