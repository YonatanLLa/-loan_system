# LoanSystem.Modules.Security.Core

## 1. ¿Para qué sirve?
`LoanSystem.Modules.Security.Core` es el **Núcleo de Dominio y Aplicación del Módulo de Seguridad, Identidad y Control de Acceso (`Security`)**. Dentro de nuestra Arquitectura Hexagonal, este proyecto modela la gestión de usuarios (`User`), el modelo de **Control de Acceso Basado en Roles (RBAC)** con los **6 roles oficiales del negocio** (`ADMIN`, `ANALYST`, `APPROVER`, `CASHIER`, `COLLECTOR`, `CUSTOMER`), los permisos granulares, las políticas de contraseñas/bloqueo de cuenta y el ciclo de vida de sesiones y *Refresh Tokens*.

---

## 2. ¿Qué hace?

### Responsabilidades actuales y archivos `.cs`

- **`Ports/Outbound/ISecurityUnitOfWork.cs`**
  - Puerto de salida que hereda de `IUnitOfWork` para confirmar atómicamente el alta de usuarios, la asignación de roles RBAC, el registro de intentos fallidos de inicio de sesión y la rotación/revocación de tokens de refresco.

- **`Ports/Outbound/ISecurityOutbox.cs`**
  - Puerto de salida que hereda de `IOutboxRepository` para publicar eventos de integración de seguridad (por ejemplo, `UserCreatedIntegrationEvent`, `UserLockedOutIntegrationEvent` o `UserRolesUpdatedIntegrationEvent`).

### ¿Qué se agrega aquí conforme crece el módulo?
- **Entidades y Objetos de Valor (`Domain/`):**
  - `User` (Aggregate Root con username, email, hash de contraseña seguro, contador de intentos fallidos, estado de bloqueo temporal y estado activo).
  - `Role` y `SystemRole` (`ADMIN`, `ANALYST`, `APPROVER`, `CASHIER`, `COLLECTOR`, `CUSTOMER`).
  - `Permission` y `RefreshToken` (token con fecha de expiración, IP de origen y marca de revocación).
- **Puertos de Entrada (`Ports/Inbound/`) y Casos de Uso (`Application/`):**
  - `ILoginUseCase` (autenticación y emisión de JWT + Refresh Token), `IRefreshTokenUseCase`, `IRevokeSessionUseCase`, `ICreateUserUseCase`, `IAssignUserRoleUseCase`.
- **Puertos de Salida (`Ports/Outbound/`):**
  - `IUserRepository`, `IPasswordHasher`, `IJwtTokenGenerator`.

---

## 3. ¿Por qué está diseñado así?

1. **Independencia de Criptografía y Transporte en el Dominio:**
   Las reglas de negocio de seguridad (por ejemplo: bloquear una cuenta tras $N$ intentos fallidos, impedir que un usuario inactivo inicie sesión o validar qué combinaciones de los 6 roles RBAC son válidas) residen en `Security.Core` y se prueban sin depender de librerías JWT ni de ASP.NET Core Identity. Los detalles criptográficos (`BCrypt`/`Argon2`, firma HMAC/RSA de `JwtSecurityToken`) se abstraen detrás de puertos de salida e implementan en `Security.Adapters`.
2. **Modelo RBAC de 6 Roles Alineado al Negocio Financiero:**
   Alinear el dominio de seguridad con los 6 actores reales de la operación crediticia (`ADMIN`, `ANALYST`, `APPROVER`, `CASHIER`, `COLLECTOR`, `CUSTOMER`) garantiza el principio de mínimo privilegio en cada endpoint de la plataforma.
