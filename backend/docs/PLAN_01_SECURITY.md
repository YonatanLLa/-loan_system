# Plan de Implementación — Sección 1: Security (`BE-SEC`)

> Guía de implementación del **núcleo MVP** del módulo `Security` del backend, derivada de la
> Sección 1 de [`../../docs/REQUERIMIENTOS_BACKEND.md`](../../docs/REQUERIMIENTOS_BACKEND.md).
> Respeta la Arquitectura Hexagonal por módulo descrita en
> [`../ARQUITECTURA_Y_CONFIGURACION.md`](../ARQUITECTURA_Y_CONFIGURACION.md).
>
> Fecha: 2026-11-07 · Estado del módulo hoy: **scaffold** (solo `/ping`).

---

## 1. Contexto y alcance

La Sección 1 define diez requisitos de autenticación, autorización, sesiones y auditoría. Este plan
cubre el **núcleo MVP**; los requisitos avanzados se documentan solo como pendientes.

| ID | Requerimiento (resumen) | Fase | En este plan |
|---|---|---|---|
| **BE-SEC-01** | Login con hash Argon2id/BCrypt, JWT corto (15–60 min), refresh token rotativo y revocable | MVP | ✅ En alcance |
| **BE-SEC-02** | Bloqueo tras 5 intentos fallidos en 15 min, con IP; respuesta `423` | MVP | ✅ En alcance |
| **BE-SEC-04** | `POST /api/auth/logout` y `GET /api/me` con permisos efectivos | MVP | ✅ En alcance |
| **BE-SEC-05** | Autorización por política en **cada** endpoint; `403` sin ejecutar nada | MVP | ✅ En alcance |
| **BE-SEC-06** | Suspender un usuario invalida sus tokens al instante | MVP | ✅ En alcance |
| **BE-SEC-07** | Interceptor de auditoría: entidad, acción, antes/después, usuario, IP y **user-agent**, en la misma transacción | MVP | ✅ En alcance |
| **BE-SEC-08** | `GET /api/audit-logs` con filtros, solo Auditor y Admin | MVP | ✅ En alcance |
| **BE-SEC-03** | MFA (TOTP) obligatorio para `ROLE_ADMIN` y `ROLE_OPERATOR` | MVP (Nuevo) | ⏳ Pendiente (§8) |
| **BE-SEC-09** | **Cuatro ojos**: reversos, castigos, ajustes, cambios de tasa/rol requieren segundo aprobador | F2 | ⏳ Pendiente (§8) |
| **BE-SEC-10** | JWT firmado con **RS256** y claves rotadas; secretos solo en gestor de secretos | MVP | ⏳ Pendiente (§8) — hoy se usa HS256 |

> **Regla transversal del proyecto:** montos en `decimal` (no aplica aquí), nada financiero se borra,
> y toda escritura sensible queda auditada. En Security la regla clave es: **contraseñas y tokens
> nunca en claro** (solo hash).

---

## 2. Estado actual y patrón a replicar

### 2.1 Scaffold existente del módulo Security

```
src/Modules/Security/
├── LoanSystem.Modules.Security.Core/
│   ├── Domain/            (vacío)                    ← crear entidades
│   ├── Application/       (vacío)                    ← crear casos de uso
│   ├── Contracts/         (vacío)                    ← crear DTOs
│   └── Ports/
│       ├── Inbound/       (vacío)                    ← crear puertos de casos de uso
│       └── Outbound/
│           ├── ISecurityUnitOfWork.cs   (: IUnitOfWork)   ✅ existe
│           └── ISecurityOutbox.cs       (: IOutbox)       ✅ existe
└── LoanSystem.Modules.Security.Adapters/
    ├── SecurityModule.cs          (AddSecurityModule, DI base) ✅ existe
    ├── Inbound/
    │   ├── Http/SecurityEndpoints.cs  (solo /ping)            ✅ existe
    │   └── Jobs/          (vacío)
    └── Outbound/
        ├── Persistence/SecurityDbContext.cs  (stub)           ✅ existe
        └── Providers/     (vacío)                             ← crear hasher y JWT
```

Ya está registrado en `src/LoanSystem.WebApi/Program.cs`
(`AddSecurityModule(...)` y `MapSecurityEndpoints()`).

### 2.2 Patrón de referencia: módulo `Products` (copiar end-to-end)

El flujo de un caso de uso se arma en 5 piezas. Replicar exactamente este patrón:

| Pieza | Archivo de referencia |
|---|---|
| DTO (`record`) | `src/Modules/Products/LoanSystem.Modules.Products.Core/Contracts/SimulateScheduleContracts.cs` |
| Puerto inbound (`: IUseCase<TReq,TRes>`) | `src/Modules/Products/LoanSystem.Modules.Products.Core/Ports/Inbound/ISimulateScheduleUseCase.cs` |
| Caso de uso (`internal sealed`, devuelve `Result<T>`) | `src/Modules/Products/LoanSystem.Modules.Products.Core/Application/SimulateScheduleUseCase.cs` |
| Endpoint Minimal API (`.ToHttpResult()`) | `src/Modules/Products/LoanSystem.Modules.Products.Adapters/Inbound/Http/ProductsEndpoints.cs` |
| Registro DI | `src/Modules/Products/LoanSystem.Modules.Products.Adapters/ProductsModule.cs` |

Ejemplo del endpoint (patrón a seguir en Security):

```csharp
group.MapPost("/simulate", async (SimulateScheduleRequest request, ISimulateScheduleUseCase useCase, CancellationToken ct) =>
{
    var result = await useCase.ExecuteAsync(request, ct);
    return result.ToHttpResult();
});
```

### 2.3 Bloques reutilizables de `Common` (NO reimplementar)

| Bloque | Ruta |
|---|---|
| `Result` / `Result<T>` | `src/Common/LoanSystem.Common.Core/Application/Result.cs` |
| `Error` / `ErrorType` (incluye `Unauthorized`, `Validation`, `NotFound`, `Conflict`, `Forbidden`, `Unprocessable`) | `src/Common/LoanSystem.Common.Core/Application/Error.cs` |
| `Entity<TId>` / `AggregateRoot<TId>` | `src/Common/LoanSystem.Common.Core/Domain/Entity.cs` |
| `IUseCase<,>` / `IUseCase<>` / `IEmptyUseCase<>` | `src/Common/LoanSystem.Common.Core/Ports/Inbound/IUseCase.cs` |
| `IClock` / `ICurrentUser` / `IUnitOfWork` / `IOutbox` | `src/Common/LoanSystem.Common.Core/Ports/Outbound/OutboundPorts.cs` |
| `CurrentUserProvider` (lee claims `sub`, `email`, `role`) | `src/Common/LoanSystem.Common.Adapters/Http/CurrentUserProvider.cs` |
| `ModuleDbContext` (base con Outbox) | `src/Common/LoanSystem.Common.Adapters/Persistence/ModuleDbContext.cs` |
| `ProblemDetailsResultMapper.ToHttpResult()` (mapea `ErrorType`→HTTP) | `src/Common/LoanSystem.Common.Adapters/Http/ProblemDetailsResultMapper.cs` |
| `NpgsqlConnectionFactory` (para Dapper) | `src/Common/LoanSystem.Common.Adapters/Persistence/NpgsqlConnectionFactory.cs` |
| `AuditLogInterceptor` (existe, no registrado) | `src/Common/LoanSystem.Common.Adapters/Persistence/AuditLogInterceptor.cs` |

> ⚠️ `ProblemDetailsResultMapper` **no mapea `423 Locked`** hoy (solo 400/401/403/404/409/422/500). Ver §5.2.

---

## 3. Esquema de base de datos disponible (SQL-First)

El esquema se administra con **scripts SQL + DbUp** (`LoanSystem.DbMigrator`), **no** con migraciones EF.
Las tablas ya existen en `database/migrations/02_security_users.sql`:

- **`users`** — `id` UUID, `email` UNIQUE, `password_hash`, `full_name`, `phone`,
  `role user_role`, `status user_status_type` (`DEFAULT 'ACTIVE'`),
  `failed_login_count SMALLINT DEFAULT 0`, `locked_until TIMESTAMPTZ`, `last_login_at`,
  `invitation_token`, `invitation_sent_at`, `password_changed_at`, `created_at`, `updated_at`.
- **`refresh_tokens`** — `id` UUID, `user_id` FK, `token_hash` UNIQUE, `issued_at`, `expires_at`,
  `revoked_at` (NULL = vigente), `user_ip INET`, `user_agent TEXT`.
- **`audit_logs`** — `id` BIGSERIAL, `entity_name`, `entity_id`, `action`, `state_before JSONB`,
  `state_after JSONB`, `user_id` FK, `user_ip INET`, `created_at`. **Solo INSERT (inmutable).**

Enums (`database/migrations/00_extensions_and_enums.sql`):
- `user_role` = `ROLE_ADMIN | ROLE_ANALYST | ROLE_OPERATOR | ROLE_COLLECTOR | ROLE_AUDITOR | ROLE_CLIENT`
  → **ya coinciden** con las políticas de `Program.cs`.
- `user_status_type` = `ACTIVE | INACTIVE | LOCKED`.

### 3.1 Gap de BD a cubrir (para BE-SEC-07)

`audit_logs` **no tiene** columna `user_agent`. Crear un **nuevo script SQL** en `database/migrations/`
(numeración siguiente disponible), aplicado por DbUp:

```sql
-- database/migrations/NN_audit_logs_user_agent.sql
ALTER TABLE audit_logs ADD COLUMN user_agent TEXT;
COMMENT ON COLUMN audit_logs.user_agent IS
    'Cliente que originó la acción (navegador/app). Requerido por BE-SEC-07.';
```

> No usar `dotnet ef migrations`. El esquema es la fuente de verdad en SQL.

---

## 4. Componentes a crear, por capa

### 4.1 Core / Domain — `.../Security.Core/Domain/`

- **`User : AggregateRoot<Guid>`** — propiedades `Email`, `PasswordHash`, `Role`, `Status`,
  `FailedLoginCount`, `LockedUntil`, `LastLoginAt`. Métodos de dominio (aquí viven BE-SEC-02 y 06):
  - `RegisterFailedLogin(IClock clock, int maxAttempts = 5, TimeSpan window)` → incrementa contador;
    si alcanza `maxAttempts` dentro de `window`, fija `LockedUntil` y `Status = LOCKED`.
  - `ResetFailedLogins()` → contador a 0 (login exitoso).
  - `IsLockedOut(IClock clock)` → `LockedUntil > clock.UtcNow`.
  - `RecordLogin(IClock clock)` → fija `LastLoginAt`.
  - `Suspend()` → `Status = INACTIVE` (BE-SEC-06).
- **`RefreshToken : Entity<Guid>`** — `UserId`, `TokenHash`, `ExpiresAt`, `RevokedAt`, `UserIp`,
  `UserAgent`. Métodos: `IsActive(IClock)` (`RevokedAt is null && ExpiresAt > now`), `Revoke(IClock)`.
- **Enums de dominio** `SecurityRole` y `UserStatus`, mapeados a los enums PostgreSQL.

> Sin referencias a EF/ASP.NET en `.Core` (validado por `Architecture.Tests`).

### 4.2 Core / Contracts — `.../Security.Core/Contracts/`

```csharp
public sealed record LoginRequest(string Email, string Password);
public sealed record LoginResponse(string AccessToken, string RefreshToken, int ExpiresInSeconds, string Role);
public sealed record RefreshRequest(string RefreshToken);
public sealed record LogoutRequest(string RefreshToken);
public sealed record MeResponse(Guid UserId, string Email, string Role, IReadOnlyCollection<string> Permissions);
public sealed record SuspendUserRequest(Guid UserId);
public sealed record AuditLogQuery(string? EntityName, Guid? UserId, DateOnly? From, DateOnly? To, int Page = 1, int PageSize = 50);
public sealed record AuditLogItem(long Id, string EntityName, string EntityId, string Action, Guid? UserId, string? UserIp, string? UserAgent, DateTimeOffset CreatedAt);
```

### 4.3 Core / Ports/Inbound — `.../Security.Core/Ports/Inbound/`

```csharp
public interface ILoginUseCase          : IUseCase<LoginRequest, LoginResponse> { }
public interface IRefreshTokenUseCase   : IUseCase<RefreshRequest, LoginResponse> { }
public interface ILogoutUseCase         : IUseCase<LogoutRequest> { }           // Result (sin valor)
public interface IGetCurrentUserUseCase : IEmptyUseCase<MeResponse> { }         // lee ICurrentUser
public interface ISuspendUserUseCase    : IUseCase<SuspendUserRequest> { }
public interface IQueryAuditLogsUseCase : IUseCase<AuditLogQuery, IReadOnlyList<AuditLogItem>> { }
```

### 4.4 Core / Ports/Outbound — `.../Security.Core/Ports/Outbound/`

Mantener `ISecurityUnitOfWork` / `ISecurityOutbox` y añadir:

```csharp
public interface IUserRepository
{
    Task<User?> FindByEmailAsync(string email, CancellationToken ct = default);
    Task<User?> FindByIdAsync(Guid id, CancellationToken ct = default);
    void Update(User user);
}
public interface IRefreshTokenRepository
{
    Task AddAsync(RefreshToken token, CancellationToken ct = default);
    Task<RefreshToken?> FindActiveByHashAsync(string tokenHash, CancellationToken ct = default);
    Task RevokeAllForUserAsync(Guid userId, IClock clock, CancellationToken ct = default);
}
public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string passwordHash);
}
public interface IJwtTokenGenerator
{
    (string Token, DateTimeOffset ExpiresAt) Generate(User user);
}
public interface IAuditLogReader   // Dapper (solo lectura, BE-SEC-08)
{
    Task<IReadOnlyList<AuditLogItem>> QueryAsync(AuditLogQuery query, CancellationToken ct = default);
}
```

### 4.5 Core / Application — `.../Security.Core/Application/` (`internal sealed`)

- **`LoginUseCase`** (BE-SEC-01, 02): `FindByEmailAsync`; si no existe o hash inválido →
  `Error.Unauthorized`. Si `IsLockedOut` → error de tipo bloqueo (→ `423`, ver §5.2). En fallo:
  `RegisterFailedLogin` + `SaveChangesAsync`. En éxito: `ResetFailedLogins`, `RecordLogin`, generar JWT
  corto vía `IJwtTokenGenerator`, crear `RefreshToken` (guardando **hash**, `UserIp`, `UserAgent`) y
  persistir. Devuelve `LoginResponse`.
- **`RefreshTokenUseCase`** (BE-SEC-01): `FindActiveByHashAsync`; si no activo → `Unauthorized`.
  **Rotación**: `Revoke` del token usado + alta de uno nuevo + nuevo JWT, en una transacción.
  > ✅ Actualizado (2026-11-10): familias de tokens, detección de reúso (`Security.RefreshTokenReused`)
  > y rotación atómica con token de concurrencia. Ver `DECISION_02_REFRESH_TOKEN_REUSE_DETECTION.md`.
- **`LogoutUseCase`** (BE-SEC-04): `Revoke` del refresh token indicado.
- **`GetCurrentUserUseCase`** (BE-SEC-04): construye `MeResponse` desde `ICurrentUser` + permisos
  efectivos derivados del rol.
- **`SuspendUserUseCase`** (BE-SEC-06): `Suspend()` + `RevokeAllForUserAsync` (invalida sesiones al
  instante) en una transacción.
- **`QueryAuditLogsUseCase`** (BE-SEC-08): delega en `IAuditLogReader`.

### 4.6 Adapters / Outbound/Providers — `.../Security.Adapters/Outbound/Providers/`

- **`PasswordHasher : IPasswordHasher`** — usar BCrypt (añadir p. ej. `BCrypt.Net-Next` a
  `Directory.Packages.props`) o Argon2id. Centralizar factor de coste.
- **`JwtTokenGenerator : IJwtTokenGenerator`** — HS256 con `Jwt:Key` ya configurada en `Program.cs`;
  claims `sub` (UserId), `email`, `role`; `exp` corto (15–60 min, configurable). *(RS256 → §8.)*

### 4.7 Adapters / Outbound/Persistence — `.../Security.Adapters/Outbound/Persistence/`

- Configurar **`SecurityDbContext`**: `DbSet<User>`, `DbSet<RefreshToken>` con
  `IEntityTypeConfiguration<>` mapeando a las tablas `users` / `refresh_tokens`, columnas snake_case,
  y los enums `user_role` / `user_status_type` (mapeo de enum PostgreSQL de Npgsql).
- **`UserRepository`**, **`RefreshTokenRepository`** (EF Core sobre `SecurityDbContext`).
- **`AuditLogReader`** (Dapper vía `NpgsqlConnectionFactory`, SQL parametrizado con filtros + paginación).

### 4.8 Adapters / Inbound/Http — `SecurityEndpoints.cs`

| Método + ruta | Caso de uso | Autorización |
|---|---|---|
| `POST /api/auth/login` | `ILoginUseCase` | `.AllowAnonymous()` |
| `POST /api/auth/refresh` | `IRefreshTokenUseCase` | `.AllowAnonymous()` |
| `POST /api/auth/logout` | `ILogoutUseCase` | `.RequireAuthorization()` |
| `GET /api/me` | `IGetCurrentUserUseCase` | `.RequireAuthorization()` |
| `POST /api/users/{id}/suspend` | `ISuspendUserUseCase` | `.RequireAuthorization("ROLE_ADMIN")` |
| `GET /api/audit-logs` | `IQueryAuditLogsUseCase` | `.RequireAuthorization("ROLE_AUDITOR")` |
| `GET /api/security/ping` | — | conservar |

Todos retornan `result.ToHttpResult()`. Las escrituras (`logout`, `suspend`) admiten `Idempotency-Key`
(middleware global ya activo).

> **BE-SEC-05**: el `403` por política se logra aplicando `.RequireAuthorization("ROLE_...")` por endpoint
> en **todos** los módulos. Las políticas (`ROLE_ADMIN`, `ROLE_ANALYST`, `ROLE_OPERATOR`, `ROLE_COLLECTOR`,
> `ROLE_AUDITOR`, `ROLE_CLIENT`) **ya existen** en `Program.cs` (ADMIN está incluido en las demás).

---

## 5. Auditoría (BE-SEC-07)

### 5.1 Interceptor

`AuditLogInterceptor` existe pero **no captura usuario/IP/user-agent ni está registrado**. Plan:

1. Inyectarle `ICurrentUser` y un accesor de `HttpContext` (para `RemoteIpAddress` y cabecera
   `User-Agent`).
2. En `SavingChangesAsync`, por cada entidad `Added/Modified/Deleted` de entidades auditables, insertar
   una fila en `audit_logs` (`entity_name`, `entity_id`, `action`, `state_before`/`state_after` JSONB,
   `user_id`, `user_ip`, `user_agent`) dentro de la **misma** `SaveChanges` (misma transacción, RNF-02).
3. Registrarlo en el `DbContext` (vía `AddInterceptors`) — primero en Security y luego adoptado por los
   demás módulos.

Requiere el `ALTER TABLE audit_logs ADD COLUMN user_agent` de §3.1.

### 5.2 Mapear `423 Locked` (BE-SEC-02)

`ErrorType` no tiene valor para bloqueo y `ProblemDetailsResultMapper` no emite `423`. Elegir una opción:
- **(A)** Añadir `ErrorType.Locked` y su mapeo a `StatusCodes.Status423Locked` en el mapper (cambio en
  `Common`; afecta a todos los módulos, pero es el lugar correcto).
- **(B)** Devolver `Results.StatusCode(423)` puntualmente en el endpoint de login al detectar el `Error`
  de bloqueo.

Recomendado: **(A)** por coherencia con el patrón `Result → ProblemDetails`.

---

## 6. Cableado (DI y `Program.cs`)

Ampliar `AddSecurityModule` (`SecurityModule.cs`):

```csharp
// Repos y proveedores
services.AddScoped<IUserRepository, UserRepository>();
services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
services.AddScoped<IAuditLogReader, AuditLogReader>();
services.AddSingleton<IPasswordHasher, PasswordHasher>();
services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
// Interceptor de auditoría
services.AddScoped<AuditLogInterceptor>();
// Casos de uso
services.AddScoped<ILoginUseCase, LoginUseCase>();
services.AddScoped<IRefreshTokenUseCase, RefreshTokenUseCase>();
services.AddScoped<ILogoutUseCase, LogoutUseCase>();
services.AddScoped<IGetCurrentUserUseCase, GetCurrentUserUseCase>();
services.AddScoped<ISuspendUserUseCase, SuspendUserUseCase>();
services.AddScoped<IQueryAuditLogsUseCase, QueryAuditLogsUseCase>();
```

`Program.cs` ya tiene JWT + políticas + `MapSecurityEndpoints()`; **no requiere cambios** salvo (si se
elige §5.2.A) el mapeo de `423`. Los nuevos endpoints viven dentro de `SecurityEndpoints`.

> Config nueva a añadir en `appsettings`: duración del access token y ventana/umbral de bloqueo
> (p. ej. `Security:AccessTokenMinutes`, `Security:MaxFailedAttempts`, `Security:LockWindowMinutes`).

---

## 7. Pruebas

| Suite | Casos a cubrir |
|---|---|
| **Domain.UnitTests** | `RegisterFailedLogin` bloquea al 5.º intento dentro de la ventana; `ResetFailedLogins`; `IsLockedOut`; `RefreshToken.IsActive`/`Revoke`. |
| **Application.UnitTests** | `LoginUseCase`: credenciales inválidas → `Unauthorized`; usuario bloqueado → error de bloqueo; éxito → tokens + reset. `RefreshTokenUseCase` rota y revoca el anterior. `LogoutUseCase` revoca. `SuspendUserUseCase` invalida todas las sesiones. |
| **Adapters.UnitTests** | `AuditLogInterceptor` captura `user_id`, `user_ip` y `user_agent`. |
| **IntegrationTests** (Testcontainers PostgreSQL) | Flujo `login → /api/me → refresh → logout`; `GET /api/audit-logs` con filtros y RBAC (`403` sin rol Auditor/Admin). |
| **Architecture.Tests** | Confirmar que `Security.Core` no referencia EF Core ni ASP.NET. |

---

## 8. Pendiente / siguientes fases (fuera de alcance de este plan)

- **BE-SEC-03 — MFA (TOTP)** para `ROLE_ADMIN` y `ROLE_OPERATOR`: requiere almacenar el secreto TOTP
  (nueva tabla/columna), paso de verificación en el login y endpoints de enrolamiento. *(MVP, Nuevo.)*
- **BE-SEC-09 — Aprobación de 4 ojos** (F2): modelo de aprobaciones (solicitud → segundo aprobador
  distinto) para reversos, castigos, ajustes y cambios de tasa/rol. Transversal a varios módulos.
- **BE-SEC-10 — RS256 + rotación de claves**: migrar la firma de JWT de HS256 (simétrica, actual) a
  RS256 (par de claves), con claves gestionadas por un gestor de secretos y rotación. Afecta a
  `JwtTokenGenerator` y a la validación en `Program.cs`.

---

## 9. Trazabilidad: requisito → artefactos

| ID | Dominio | Puerto / Caso de uso | Endpoint | Prueba clave |
|---|---|---|---|---|
| BE-SEC-01 | `User`, `RefreshToken`, `IPasswordHasher`, `IJwtTokenGenerator` | `ILoginUseCase`, `IRefreshTokenUseCase` | `POST /api/auth/login`, `/api/auth/refresh` | login éxito + rotación de refresh |
| BE-SEC-02 | `User.RegisterFailedLogin` / `IsLockedOut` | `ILoginUseCase` | `POST /api/auth/login` → `423` | bloqueo al 5.º intento |
| BE-SEC-04 | — | `ILogoutUseCase`, `IGetCurrentUserUseCase` | `POST /api/auth/logout`, `GET /api/me` | logout revoca; `/me` con permisos |
| BE-SEC-05 | — | (políticas existentes) | `.RequireAuthorization("ROLE_...")` | `403` sin rol |
| BE-SEC-06 | `User.Suspend`, `RevokeAllForUserAsync` | `ISuspendUserUseCase` | `POST /api/users/{id}/suspend` | suspender invalida sesiones |
| BE-SEC-07 | — | `AuditLogInterceptor` (+ `ALTER TABLE`) | (transversal) | captura usuario/IP/user-agent |
| BE-SEC-08 | — | `IQueryAuditLogsUseCase`, `IAuditLogReader` | `GET /api/audit-logs` | filtros + RBAC Auditor/Admin |
