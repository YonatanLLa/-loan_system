# Plan — Verificación de correo + Recuperación de contraseña (Resend)

> Documento de planificación. **No implementa código todavía**: describe qué se construirá, dónde y por qué,
> para ejecutarlo en una tarea posterior. Extiende el módulo **Security** sobre lo ya hecho en
> `PLAN_01_SECURITY.md` (BE-SEC-01/02). Español, coherente con el resto del repo.

---

## 1. Contexto y alcance

Hoy el login (BE-SEC-01) valida **correo + contraseña** contra la tabla `users` (hash BCrypt) y emite
JWT + refresh token rotativo. **Falta** el ciclo de vida de la cuenta por correo:

| ID propuesto | Requisito | En alcance |
|---|---|---|
| **BE-SEC-11** | **Verificación de correo**: al crear/invitar un usuario se envía un enlace de confirmación; hasta confirmarlo el correo queda marcado como no verificado | ✅ Sí |
| **BE-SEC-12** | **Recuperación de contraseña**: flujo "olvidé mi contraseña" → enlace por correo → fijar nueva contraseña | ✅ Sí |
| **BE-SEC-13** | **Envío de correos** vía **Resend** como adaptador de salida (`IEmailSender`) | ✅ Sí |
| — | Plantillas HTML con marca/branding avanzado, i18n, colas de reintento de correo | ⚠️ Nota / fase posterior |
| — | MFA/TOTP (BE-SEC-03), social login (Google/Microsoft) | ❌ Fuera de alcance |

> **Nota de numeración**: `BE-SEC-11..13` son IDs **propuestos** (el doc de requerimientos original llega hasta
> `BE-SEC-10`). La tabla `users` ya tiene columnas del flujo de invitación (`invitation_token`,
> `invitation_sent_at`, `password_changed_at`, US-43), que este plan reemplaza/consolida en un modelo de tokens
> dedicado y más seguro.

### Por qué Resend y no Auth0
Se evaluó Auth0: obligaría a **migrar toda la autenticación ya hecha** (login/refresh/BCrypt/roles) a su SDK,
y su envío de correos propio es **solo para pruebas** — en producción igual exige conectar un proveedor externo.
Resend cubre únicamente la pieza que falta (envío de email) sin tocar la identidad, que sigue viviendo en **nuestra
PostgreSQL** (requisito para datos regulados de un sistema de préstamos). Encaja como un adaptador de salida más
en la arquitectura hexagonal.

---

## 2. Estado actual y patrón a replicar

Se reutiliza **exactamente** la cadena ya probada en BE-SEC-01:

`Contracts (record)` → `Ports/Inbound/IXxxUseCase : IUseCase<TReq[,TRes]>` → `Application/XxxUseCase : internal sealed` (devuelve `Result`/`Result<T>`) → endpoint `auth.MapPost(...).ToHttpResult()` en `SecurityEndpoints` → registro en `SecurityModule.AddSecurityModule`.

Piezas existentes que se reaprovechan tal cual:
- **Hasheo de tokens**: `src/Modules/Security/.../Core/Application/RefreshTokenHasher.cs` (genera token aleatorio 32 bytes base64 + hash SHA-256). Se generaliza para los tokens de cuenta — ver §4.
- **Entidad con revocación/expiración**: patrón de `Core/Domain/RefreshToken.cs` (`IsActive(IClock)`, `Revoke(IClock)`).
- **Hash de contraseña**: `IPasswordHasher` + `BcryptPasswordHasher` (ya inyectado) para re-hashear en el reset.
- **Reloj**: `IClock` (Common) para expiraciones — sin `DateTime.Now` directo.
- **Resultado funcional**: `Result`/`Error`/`ErrorType` + `ProblemDetailsResultMapper.ToHttpResult()`.
- **Invalidación de sesiones**: `IRefreshTokenRepository` (al cambiar contraseña se revocan los refresh tokens, enlaza con BE-SEC-06).

---

## 3. Cambios de base de datos (SQL-First / DbUp — nunca migraciones EF)

Las migraciones viven en `database/migrations/` y las aplica `LoanSystem.DbMigrator` (DbUp) en orden. El último
número usado es `13_outbox_messages.sql`, así que se crean **dos** scripts nuevos.

### 3.1 `database/migrations/14_account_email.sql`
```sql
-- Verificación de correo sobre la tabla users (BE-SEC-11)
ALTER TABLE users ADD COLUMN email_verified     BOOLEAN     NOT NULL DEFAULT FALSE;
ALTER TABLE users ADD COLUMN email_verified_at  TIMESTAMPTZ;

-- Propósito del token de cuenta
CREATE TYPE account_token_purpose AS ENUM (
    'EMAIL_VERIFICATION',
    'PASSWORD_RESET'
);

-- Tokens de un solo uso para verificación de correo y reset de contraseña.
-- Se guarda SOLO el hash SHA-256; el token en claro viaja únicamente en el enlace del correo.
CREATE TABLE account_tokens (
    id          UUID                   PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id     UUID                   NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    purpose     account_token_purpose  NOT NULL,
    token_hash  VARCHAR(255)           NOT NULL UNIQUE,
    issued_at   TIMESTAMPTZ            NOT NULL DEFAULT NOW(),
    expires_at  TIMESTAMPTZ            NOT NULL,
    used_at     TIMESTAMPTZ,                      -- NULL = aún sin usar
    user_ip     INET,
    user_agent  TEXT
);
CREATE INDEX ix_account_tokens_user_purpose ON account_tokens (user_id, purpose);

COMMENT ON TABLE account_tokens IS
    'Tokens de un solo uso para verificación de correo (BE-SEC-11) y reset de contraseña (BE-SEC-12). Solo hash, nunca el token en claro.';
```

> El enum UPPER_SNAKE `account_token_purpose` se mapea siguiendo la convención del módulo
> (`NpgsqlNullNameTranslator`, miembros C# idénticos a las etiquetas) — ver §4.

### 3.2 (Opcional) limpieza del flujo de invitación legacy
Las columnas `invitation_token`/`invitation_sent_at` quedan **obsoletas** al unificar en `account_tokens`. No se
borran en este plan (evitar romper seeds); se marcan como deprecadas en un comentario.

---

## 4. Componentes a crear por capa

Rutas bajo `src/Modules/Security/`.

### Core / Domain (`...Core/Domain/`)
- **`AccountToken.cs`** — `internal sealed class AccountToken : Entity<Guid>`:
  - Props: `UserId`, `Purpose (AccountTokenPurpose)`, `TokenHash`, `IssuedAt`, `ExpiresAt`, `UsedAt`, `UserIp`, `UserAgent`.
  - `static AccountToken Issue(userId, purpose, tokenHash, IClock, TimeSpan lifetime, IPAddress?, string?)`.
  - `bool IsUsable(IClock)` → `UsedAt is null && ExpiresAt > clock.UtcNow`.
  - `void Consume(IClock)` → fija `UsedAt` (un solo uso).
- **`SecurityEnums.cs`** (editar) — añadir `internal enum AccountTokenPurpose { EMAIL_VERIFICATION, PASSWORD_RESET }` (nombres idénticos a las etiquetas PG).
- **`User.cs`** (editar) — añadir:
  - `public bool EmailVerified { get; private set; }` y `DateTimeOffset? EmailVerifiedAt`.
  - `void MarkEmailVerified(IClock)` (idempotente si ya está verificado).
  - `void ChangePassword(string newPasswordHash, IClock)` → fija hash y `password_changed_at`; llama a `ResetFailedLogins()`.
  - Actualizar `Reconstitute(...)` para incluir el nuevo estado.

### Core / Contracts (`...Core/Contracts/`) — nuevo `AccountContracts.cs`
```csharp
public sealed record ForgotPasswordRequest(string Email);
public sealed record ResetPasswordRequest(string Token, string NewPassword);
public sealed record VerifyEmailRequest(string Token);
public sealed record ResendVerificationRequest(string Email);
```

### Core / Ports/Inbound
- `IRequestPasswordResetUseCase : IUseCase<ForgotPasswordRequest>`
- `IResetPasswordUseCase      : IUseCase<ResetPasswordRequest>`
- `IVerifyEmailUseCase        : IUseCase<VerifyEmailRequest>`
- `IResendVerificationUseCase : IUseCase<ResendVerificationRequest>`

### Core / Ports/Outbound
- **`IAccountTokenRepository`**: `void Add(AccountToken)`, `Task<AccountToken?> FindUsableByHashAsync(string hash, AccountTokenPurpose, CancellationToken)`, `Task InvalidateActiveAsync(Guid userId, AccountTokenPurpose, IClock, CancellationToken)` (marca `used_at` a los previos del mismo propósito).
- **`IEmailSender`** (BE-SEC-13): `Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken)`. Puerto genérico; el adaptador Resend lo implementa.
- Reutiliza `IUserRepository` (ya tiene `FindByEmailAsync`/`FindByIdAsync`), `IPasswordHasher`, `IRefreshTokenRepository`, `ISecurityUnitOfWork`, `IClientInfoProvider`.

### Core / Application (`internal sealed`, devuelven `Result`)
- **Generalizar `RefreshTokenHasher`** → extraer un helper compartido `TokenHasher` (o reutilizar su `Create()`/`Hash()`) para los tokens de cuenta. No duplicar la lógica SHA-256.
- **`RequestPasswordResetUseCase`** (BE-SEC-12):
  - Busca usuario por email. **Si no existe o está inactivo → devuelve `Result.Success()` igual** (NO revelar si la cuenta existe — anti-enumeración).
  - Si existe: `InvalidateActiveAsync(PASSWORD_RESET)`, genera token (`Create()`), persiste `AccountToken.Issue(... lifetime ~30–60 min)`, arma enlace `"{Email:AppBaseUrl}/reset-password?token={raw}"`, llama `IEmailSender.SendAsync(...)`, `SaveChangesAsync`.
- **`ResetPasswordUseCase`** (BE-SEC-12):
  - Hashea el token entrante → `FindUsableByHashAsync(PASSWORD_RESET)`. Si no usable → `Error.Unauthorized("Security.InvalidResetToken")`.
  - Valida fortaleza mínima de `NewPassword` (→ `Error.Validation`).
  - `user.ChangePassword(hasher.Hash(new), clock)`, `token.Consume(clock)`, **revoca todos los refresh tokens del usuario** (invalidación de sesiones, BE-SEC-06), `SaveChangesAsync`.
- **`VerifyEmailUseCase`** (BE-SEC-11): hashea token → `FindUsableByHashAsync(EMAIL_VERIFICATION)`; si usable → `user.MarkEmailVerified(clock)`, `token.Consume(clock)`, `SaveChangesAsync`. Si no → `Error.Unauthorized`.
- **`ResendVerificationUseCase`** (BE-SEC-11): como el reset pero con `EMAIL_VERIFICATION`; también anti-enumeración (éxito aunque no exista / ya verificado).

### Adapters / Outbound/Providers
- **`ResendEmailSender.cs`** (`internal sealed : IEmailSender`): usa el paquete oficial **`Resend`** (NuGet) o `HttpClient` directo a `https://api.resend.com/emails`. Lee `Email:Resend:ApiKey`, `Email:From` (remitente verificado), `Email:AppBaseUrl` (para construir enlaces). Registrado vía `services.AddHttpClient<...>()`.

### Adapters / Outbound/Persistence
- **`AccountTokenRepository.cs`** (EF Core sobre `SecurityDbContext`).
- **`SecurityDbContext.cs`** (editar): `DbSet<AccountToken>`, `HasPostgresEnum<AccountTokenPurpose>("public","account_token_purpose", EnumNameTranslator)`, mapeo de la tabla `account_tokens` (columnas snake_case; todo secreto que el interceptor no reconozca por nombre se marca con `.IsAuditSensitive()`) y las nuevas columnas de `users` (`email_verified`, `email_verified_at`).
- **`SecurityModule.cs`** (editar): `npgsql.MapEnum<AccountTokenPurpose>("account_token_purpose","public", ...)`.

### Adapters / Inbound/Http (`SecurityEndpoints.cs`, editar)
Todos bajo el grupo `/api/auth` existente, **`.AllowAnonymous()`** (el usuario aún no tiene sesión), devolviendo `result.ToHttpResult()`:
- `POST /api/auth/forgot-password`     → `IRequestPasswordResetUseCase`
- `POST /api/auth/reset-password`      → `IResetPasswordUseCase`
- `POST /api/auth/verify-email`        → `IVerifyEmailUseCase`
- `POST /api/auth/resend-verification` → `IResendVerificationUseCase`

### Cableado (DI)
En `AddSecurityModule`: registrar `IAccountTokenRepository`, `IEmailSender` (+ `AddHttpClient`), los 4 casos de uso, y leer la config `Email:*`. Añadir el paquete `Resend` a `Directory.Packages.props` y la `PackageReference` en `Security.Adapters.csproj`.

---

## 5. Configuración y secretos
`appsettings.json` (sin valores reales) + `appsettings.Development.json` / **user-secrets** / variables de entorno:
```json
"Email": {
  "From": "no-reply@tudominio.com",
  "AppBaseUrl": "http://localhost:5173",
  "Resend": { "ApiKey": "" }
}
```
- La **API key de Resend NUNCA** va al repo: usar `dotnet user-secrets` en dev y variable de entorno en prod.
- `From` debe ser un dominio **verificado** en Resend (si no, Resend rechaza el envío).
- La app debe fallar claro si falta la API key cuando el envío está habilitado.

---

## 6. Seguridad (requisitos transversales)
- **Solo se persiste el hash** del token (SHA-256); el token en claro viaja únicamente en el enlace del correo.
- **Un solo uso** (`used_at`) y **expiración corta**: reset ~30–60 min, verificación ~24 h.
- **Invalidar tokens previos** del mismo propósito al emitir uno nuevo.
- **Anti-enumeración**: `forgot-password` y `resend-verification` responden igual exista o no el correo.
- **Reset invalida todas las sesiones** (revoca refresh tokens) — enlaza con BE-SEC-06.
- **Rate limiting** por IP/email en estos endpoints (nota: usar el middleware de rate limiting de ASP.NET; detalle en fase de endurecimiento).

---

## 7. Pruebas
- **Domain.UnitTests**: `AccountToken` usable/expirado/consumido; `User.MarkEmailVerified` idempotente; `User.ChangePassword` resetea intentos fallidos.
- **Application.UnitTests** (con `FakeEmailSender`, `FakeAccountTokenRepository`, reutilizando los fakes de `SecurityAuthUseCasesTests`):
  - `forgot-password`: email desconocido → `Success` sin enviar / email conocido → envía y persiste token.
  - `reset-password`: token válido → cambia hash + revoca refresh tokens; token expirado/usado → `Unauthorized`; contraseña débil → `Validation`.
  - `verify-email`: marca verificado; token inválido → `Unauthorized`.
  - anti-enumeración comprobada (mismo `Result` con/ sin usuario).
- **Architecture.Tests**: siguen pasando (Core sin EF/ASP.NET; `IEmailSender` es un puerto en Core, el adaptador Resend en `.Adapters`).

---

## 8. Archivos que tocará la EJECUCIÓN (no esta tarea)
**Nuevos**: `14_account_email.sql`; `Core/Domain/AccountToken.cs`; `Core/Contracts/AccountContracts.cs`;
4 interfaces en `Core/Ports/Inbound/`; `IAccountTokenRepository.cs` + `IEmailSender.cs` en `Core/Ports/Outbound/`;
4 use cases en `Core/Application/`; `Adapters/Outbound/Providers/ResendEmailSender.cs`;
`Adapters/Outbound/Persistence/AccountTokenRepository.cs`; tests nuevos.
**Editados**: `Core/Domain/SecurityEnums.cs`, `Core/Domain/User.cs`, `Core/Application/RefreshTokenHasher.cs` (generalizar);
`Adapters/Outbound/Persistence/SecurityDbContext.cs`, `Adapters/SecurityModule.cs`,
`Adapters/Inbound/Http/SecurityEndpoints.cs`; `Directory.Packages.props`, `Security.Adapters.csproj`; `appsettings*.json`.

## 9. Verificación de la ejecución
- `dotnet build LoanSystem.slnx` → 0 errores; Architecture.Tests y Application.UnitTests en verde.
- `DbMigrator` aplica `14_account_email.sql` sin error; existen `account_tokens` y las columnas nuevas en `users`.
- Flujo manual en Scalar: `forgot-password` → llega correo (o log en dev) → `reset-password` con el token → login con la nueva contraseña OK y los refresh previos quedan revocados.

## 10. Orden de ejecución sugerido
1. SQL `14_account_email.sql` + `DbMigrator`.
2. Dominio (`AccountToken`, enum, cambios en `User`) + tests de dominio.
3. Puertos + casos de uso + tests de aplicación (con `FakeEmailSender`).
4. Adaptadores (persistencia, `ResendEmailSender`, DbContext/DI).
5. Endpoints + config + prueba manual end-to-end en Scalar.
