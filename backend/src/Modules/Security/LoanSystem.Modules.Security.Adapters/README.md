# LoanSystem.Modules.Security.Adapters

## 1. ¿Para qué sirve?
`LoanSystem.Modules.Security.Adapters` es la **Capa de Adaptadores (Entrada y Salida)** del módulo de Seguridad (`Security`). Conecta el núcleo `LoanSystem.Modules.Security.Core` con los endpoints HTTP de autenticación y gestión de usuarios bajo `/api/v1/security`, los generadores de tokens JWT/hashing de contraseñas y la persistencia en PostgreSQL 17 dentro del esquema `security`.

---

## 2. ¿Qué hace?

### Responsabilidades actuales y archivos `.cs`

- **`SecurityModule.cs`**
  - Clase pública de composición del módulo que expone `AddSecurityModule` (registrando `SecurityDbContext` con `AuditLogInterceptor`, `ISecurityUnitOfWork` e `ISecurityOutbox`) y `MapSecurityEndpoints`.

- **`Inbound/Http/SecurityEndpoints.cs`**
  - Adaptador primario HTTP (Minimal APIs) que define el grupo `/api/v1/security` con la etiqueta OpenAPI `"Security"`, donde se ubican los endpoints de inicio de sesión (`/login`), renovación de token (`/refresh`), cierre de sesión (`/logout`) y administración de usuarios y roles RBAC.

- **`Outbound/Persistence/SecurityDbContext.cs`**
  - Adaptador secundario de persistencia que hereda de `ModuleDbContext`.
  - Configura por defecto el esquema **`security`** (`modelBuilder.HasDefaultSchema("security")`), mapeando las tablas creadas en `database/migrations/02_security_users.sql` (`users`, `roles`, `permissions`, `user_roles`, `role_permissions`, `refresh_tokens`).

### ¿Qué se agrega aquí conforme crece el módulo?
- En `Outbound/Authentication/`: Implementaciones concretas de `IJwtTokenGenerator` y `IPasswordHasher`.
- En `Outbound/Persistence/`: Configuraciones EF Core (`IEntityTypeConfiguration<User>`, etc.) y `UserRepository`.
- En `Inbound/Http/`: Endpoints de administración de usuarios protegidos con `.RequireAuthorization("ADMIN")`.

---

## 3. ¿Por qué está diseñado así?

1. **Aislamiento de Credenciales y Tokens en el Esquema `security`:**
   Los hashes de contraseñas (`password_hash`), permisos y `refresh_tokens` son altamente sensibles. Al confinarlos en el esquema `security` bajo el control exclusivo de `SecurityDbContext`, ningún otro módulo de negocio (`Customers`, `Loans`, `Reports`, etc.) puede consultar ni exponer accidentalmente credenciales en sus consultas.
2. **Integración Limpia con JWT + 6 Políticas RBAC en `WebApi`:**
   `Security.Adapters` emite los claims estándar de rol que `LoanSystem.WebApi` valida en su middleware de autorización para las políticas `ADMIN`, `ANALYST`, `APPROVER`, `CASHIER`, `COLLECTOR` y `CUSTOMER`.
