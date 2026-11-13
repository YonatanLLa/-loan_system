# DECISIÓN 02: detección de reúso de refresh tokens y rotación atómica

| Campo | Valor |
|---|---|
| Fecha | 2026-11-10 |
| Estado | Aplicada |
| Módulo | Security (sesiones / refresh tokens) |
| Requerimiento | BE-SEC-01 (refresh token rotativo y revocable) |
| Archivos | `Security.Core/Domain/RefreshToken.cs`, `Security.Core/Application/RefreshTokenUseCase.cs`, `Security.Core/Application/TokenIssuer.cs`, `Security.Core/Application/LogoutUseCase.cs`, `Security.Core/Application/SuspendUserUseCase.cs`, `Security.Core/Ports/Outbound/IRefreshTokenRepository.cs`, `Security.Adapters/Outbound/Persistence/RefreshTokenRepository.cs`, `Security.Adapters/Outbound/Persistence/SecurityDbContext.cs`, `Security.Adapters/Inbound/Http/SecurityEndpoints.cs`, `Common.Core/Ports/Outbound/ConcurrencyConflictException.cs`, `Common.Adapters/Persistence/ModuleDbContext.cs`, `database/migrations/16_refresh_tokens_family.sql` |

## 1. Problema detectado

El refresh token ya rotaba: cada `POST /api/auth/refresh` revocaba el token presentado y emitía uno nuevo. Pero había dos huecos.

**a) Robo de token sin detección.** El caso de uso buscaba el token con `FindActiveByHashAsync`, que filtra `revoked_at IS NULL`. Un token revocado era indistinguible de uno inexistente. Si un atacante roba un refresh token:

1. El atacante lo usa primero y obtiene un token nuevo. Desde ahí mantiene la cadena indefinidamente.
2. Cuando el usuario legítimo presenta el token viejo, recibe un 401 genérico. Nadie se entera de que hubo dos partes usando la misma sesión.

Lo mismo ocurre en el orden inverso: el atacante presenta un token que el usuario ya rotó y solo recibe un 401, sin consecuencias para la sesión.

**b) Carrera entre dos refresh simultáneos.** Dos peticiones con el mismo token (dos pestañas, un reintento de red) leían el token como activo, lo revocaban en memoria e insertaban cada una un token nuevo. El `UPDATE` no tenía condición, así que **ambas tenían éxito**. Resultado: dos tokens activos derivados del mismo token, es decir, la sesión se bifurcaba.

## 2. Decisión

1. **Familias de tokens.** Cada login abre una familia (`family_id` = id del token emitido en el login). Cada rotación emite el siguiente token **de la misma familia** y marca el anterior como revocado con `replaced_by_token_id` = id del nuevo (`RefreshToken.Rotate`).

2. **Detección de reúso.** Si llega un token que **ya fue rotado** (`replaced_by_token_id IS NOT NULL`), dos partes tienen la cadena y no podemos saber cuál es la legítima. Se hace lo siguiente:
   - Se revoca **toda la familia** con un único `UPDATE refresh_tokens SET revoked_at = now WHERE family_id = @f AND revoked_at IS NULL` (`RevokeFamilyAsync`, vía `ExecuteUpdateAsync`).
   - Se responde **401 con el código `Security.RefreshTokenReused`**.
   - El actor de auditoría es el dueño del token. `refresh_tokens` está excluida de `audit_logs` (BE-SEC-07), así que la señal queda en el **log de la aplicación**: `SecurityEndpoints` registra un `Warning` con IP y user-agent. Core no depende de `ILogger` y no se agregó infraestructura nueva.
   - Las **demás familias del usuario** (otros dispositivos) siguen activas.

3. **Qué NO es reúso.** Un token revocado **sin** reemplazo (logout, suspensión del usuario o familia ya revocada) o un token **vencido** devuelven `Security.InvalidRefreshToken` y no revocan nada. Así un usuario suspendido sigue recibiendo `InvalidRefreshToken`, y una pestaña olvidada tras un logout no dispara alarmas.

4. **Rotación atómica con token de concurrencia.** `revoked_at` está configurado como `IsConcurrencyToken()` en `SecurityDbContext`. La rotación hace, en **un solo `SaveChanges`** (una transacción):

   ```sql
   INSERT INTO refresh_tokens (...)  -- token nuevo, misma familia
   UPDATE refresh_tokens SET revoked_at = @now, replaced_by_token_id = @nuevo
    WHERE id = @viejo AND revoked_at IS NULL;  -- condición agregada por el token de concurrencia
   ```

   Si otra petición ganó la carrera, el `UPDATE` afecta 0 filas. EF lanza `DbUpdateConcurrencyException`, la transacción completa se revierte (incluido el `INSERT`) y `ModuleDbContext` la traduce a `ConcurrencyConflictException` (Common.Core). Así Core no conoce EF.

5. **El perdedor de la carrera recibe 401 `Security.InvalidRefreshToken` y NO se revoca la familia.** Es el caso benigno (dos pestañas, reintento). Si fuera un ladrón, se le detecta en el siguiente uso: el token ya quedó revocado **y** enlazado a su reemplazo, así que volver a presentarlo es reúso.

6. **Efectos colaterales en otros casos de uso que tocan `revoked_at`:**
   - `LogoutUseCase` (con token): ahora busca con `FindByHashAsync` y solo revoca si no está revocado. Si hay conflicto de concurrencia (otro proceso lo revocó primero), responde **éxito**, porque el objetivo del logout ya se cumplió. El comportamiento externo no cambia.
   - `LogoutUseCase` (sin token) y `SuspendUserUseCase`: `RevokeAllForUserAsync` pasa a ser un `UPDATE` masivo (`ExecuteUpdateAsync`), que no compite con el token de concurrencia y además alcanza tokens insertados después de cualquier lectura previa. En la suspensión va dentro de una transacción explícita junto con el cambio de estado del usuario, así se mantiene la atomicidad. Los bloqueos de fila de ese `UPDATE` hacen que una rotación concurrente de esos tokens pierda la carrera.
   - `FindActiveByHashAsync` se eliminó: ya no tenía usos.

7. **Relación FK y orden de comandos.** `replaced_by_token_id` referencia a `refresh_tokens(id)`. EF conoce la auto-relación (sin navegación) para insertar el token nuevo antes del `UPDATE` del viejo, y además la FK es `DEFERRABLE INITIALLY DEFERRED`, así que se valida al `COMMIT`.

### Flujo de `POST /api/auth/refresh`

| Situación | Respuesta | Efecto |
|---|---|---|
| Token vacío | 400 `Security.MissingRefreshToken` | — |
| Token desconocido | 401 `Security.InvalidRefreshToken` | — |
| Token ya rotado (reúso) | 401 `Security.RefreshTokenReused` | Revoca toda la familia + log `Warning` |
| Token revocado sin reemplazo (logout/suspensión) | 401 `Security.InvalidRefreshToken` | — |
| Token vencido | 401 `Security.InvalidRefreshToken` | — |
| Usuario inexistente o inactivo | 401 `Security.InvalidRefreshToken` | — |
| Rotación pierde la carrera (conflicto de concurrencia) | 401 `Security.InvalidRefreshToken` | Nada (se revierte todo) |
| Token activo | 200 con nuevo par de tokens | Token viejo revocado y enlazado; token nuevo en la misma familia |

## 3. Cambios de API y errores

- **Nuevo código de error:** `Security.RefreshTokenReused` (HTTP 401, `errorType: "Unauthorized"`).
- **Mismo mensaje** que `Security.InvalidRefreshToken` ("The refresh token is invalid, expired or revoked."). El atacante no aprende nada nuevo del texto. El código distinto sirve al cliente legítimo (y a los logs/alertas) para saber que la sesión completa se cerró por seguridad.
- El contrato de `POST /api/auth/refresh` (request/response) **no cambia**.

## 4. Migración 16

`database/migrations/16_refresh_tokens_family.sql` (idempotente). Se agregó al final de la lista de `LoanSystem.DbMigrator/Program.cs` y a `database/run_all.sql`:

- `family_id UUID`: se rellena con `family_id = id` para los tokens existentes (cada uno es su propia familia) y luego pasa a `NOT NULL`.
- `replaced_by_token_id UUID NULL`, con FK a `refresh_tokens(id)` `ON DELETE SET NULL DEFERRABLE INITIALLY DEFERRED`.
- Índice `idx_refresh_tokens_family_id`.
- `COMMENT ON COLUMN` en español para ambas columnas. También se actualizaron `database/docs/diccionario-de-datos.md` y `diagrama-entidad-relacion.md`.

Los tokens emitidos antes de la migración siguen funcionando: al rotarlos, el nuevo hereda su familia.

## 5. Impacto en el frontend

Como la rotación ahora es estricta, **dos refresh simultáneos con el mismo token ya no tienen éxito los dos**. El segundo recibe 401, y si llega después de que el primero terminó, se detecta como reúso y **se cierra la sesión completa**. Por eso el frontend debe cumplir:

- **Single-flight:** solo un refresh en vuelo a la vez. Todas las peticiones que reciben 401 esperan esa misma promesa. Idealmente se comparte entre pestañas (`BroadcastChannel` o Web Locks API `navigator.locks.request('refresh', ...)`). Nunca debe reintentarse el refresh con el mismo token.
- **Ante 401 de `/api/auth/refresh`** (cualquiera de los dos códigos): limpiar la sesión y llevar al login. Con `Security.RefreshTokenReused` se puede mostrar un aviso del tipo "Tu sesión se cerró por seguridad. Vuelve a iniciar sesión."
- Ver la guía `frontend/docs/REFRESH_TOKEN_SINGLE_FLIGHT.md`, que incluye el estado actual de `packages/api-client/src/http.ts`.

## 6. Pruebas

- `tests/LoanSystem.Domain.UnitTests/RefreshTokenTests.cs`: `Issue` abre familia (`FamilyId == Id`); `Rotate` hereda la familia, revoca y enlaza; no se puede rotar un token revocado o vencido; `Revoke` no enlaza reemplazo.
- `tests/LoanSystem.Application.UnitTests/SecurityAuthUseCasesTests.cs`:
  - La rotación mantiene la familia, fija `ReplacedByTokenId`, revoca el viejo y usa un solo `SaveChanges`.
  - Reusar un token rotado revoca todos los tokens activos de la familia, devuelve `Security.RefreshTokenReused`, fija el actor de auditoría y deja activas las otras familias del mismo usuario.
  - Un token vencido devuelve 401 `InvalidRefreshToken` sin revocar la familia; un token cerrado por logout tampoco cuenta como reúso.
  - Un conflicto de concurrencia en el `SaveChanges` devuelve 401 `InvalidRefreshToken` sin revocar la familia.
  - El logout con conflicto de concurrencia sigue respondiendo éxito; el logout de un token ya revocado no guarda nada; la suspensión confirma su transacción.
- `tests/LoanSystem.Adapters.UnitTests/RefreshTokenConcurrencyTests.cs` (sin Docker): el modelo EF tiene `RevokedAt` como único token de concurrencia, las columnas `family_id`/`replaced_by_token_id` y la auto-relación; y `ModuleDbContext` traduce `DbUpdateConcurrencyException` a `ConcurrencyConflictException` (proveedor InMemory).
- `tests/LoanSystem.IntegrationTests/RefreshTokenRotationIntegrationTests.cs` (PostgreSQL real con Testcontainers; sin Docker retorna sin verificar, como el resto):
  - El reúso revoca la familia en BD y deja activa la sesión del otro dispositivo.
  - 6 refresh concurrentes con el mismo token: como máximo uno tiene éxito y nunca quedan dos tokens activos en la familia.

## 7. Pendientes a tener en cuenta

- [ ] **Ventana de gracia opcional:** aceptar el token recién rotado durante unos segundos (devolviendo el mismo sucesor) para tolerar reintentos de red sin cerrar la sesión. Hoy no existe: el reintento tardío cuenta como reúso.
- [ ] **Alcance de la revocación:** hoy solo se revoca la familia afectada. Evaluar revocar **todas** las sesiones del usuario ante un reúso (más seguro, peor experiencia de uso).
- [ ] **Notificar al usuario por correo** cuando se detecta un reúso (depende de PLAN_02, Resend).
- [ ] **Rate limiting en `/api/auth/refresh`** para frenar fuerza bruta y abuso.
- [ ] **Limpieza de tokens vencidos o revocados** (job periódico). La FK `ON DELETE SET NULL` ya permite borrar eslabones viejos de la cadena.
- [ ] **`LogoutUseCase` no verifica que el token pertenezca al usuario autenticado:** cualquier usuario autenticado puede revocar un refresh token ajeno si conoce el valor en claro.
- [ ] **Vida absoluta de la sesión:** cada rotación renueva el vencimiento a 14 días (ventana deslizante). Evaluar un máximo absoluto por familia.
- [ ] **Ventana residual en la revocación de familia:** si una rotación hace `COMMIT` justo mientras corre el `UPDATE` masivo de la familia, el token recién insertado puede quedar fuera de ese `UPDATE` (no estaba en su snapshot). Queda detectable en el siguiente reúso. Se podría cerrar repitiendo `RevokeFamilyAsync` o con un `SELECT ... FOR UPDATE` de la familia.
- [ ] **Alertas:** conectar el `Warning` de reúso a métricas o alertas (hoy solo queda en el log).
