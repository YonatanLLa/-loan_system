# API — Módulo Security (`BE-SEC`)

> Referencia de endpoints del módulo **Security** del backend, derivada del código real en
> `src/Modules/Security/`. Complementa a [`PLAN_01_SECURITY.md`](PLAN_01_SECURITY.md) (plan) y a los
> análisis `ANALISIS_03_MFA_TOTP.md`, `ANALISIS_09_CUATRO_OJOS.md`, `ANALISIS_10_JWT_RS256_Y_SECRETOS.md`.
>
> Fecha: 2026-11-13 · Estado: **implementado** (login, MFA, refresh con detección de reúso, logout,
> perfil, suspensión/activación, auditoría, JWKS RS256 y aprobaciones de cuatro ojos).

---

## 1. Convenciones generales

- **Base URL:** las rutas son absolutas tal como se registran (`/api/auth/...`, `/api/me`, `/api/users/...`,
  `/api/audit-logs`, `/api/approvals/...`, `/.well-known/jwks.json`, `/api/security/ping`).
- **Formato:** JSON en request y response. `Content-Type: application/json`.
- **Autenticación:** JWT **Bearer** en la cabecera `Authorization: Bearer <access_token>`.
  El token se firma con **RS256** y se valida contra el JWKS publicado (BE-SEC-10).
- **Errores:** todos los fallos se devuelven como **RFC 7807 Problem Details**. El mapeo de tipo de
  error a HTTP (`src/Common/.../ProblemDetailsResultMapper.cs`) es:

  | `ErrorType` | HTTP |
  |---|---|
  | `Validation` | `400 Bad Request` |
  | `Unauthorized` | `401 Unauthorized` |
  | `Forbidden` | `403 Forbidden` |
  | `NotFound` | `404 Not Found` |
  | `Conflict` | `409 Conflict` |
  | `Unprocessable` | `422 Unprocessable Entity` |
  | `Locked` | `423 Locked` |
  | (otro) | `500 Internal Server Error` |

  El cuerpo de error incluye un `code` estable (ej. `Security.InvalidCredentials`) y un `detail`.
- **Idempotencia:** las escrituras admiten la cabecera `Idempotency-Key` (middleware global activo).
- **Access token:** vida configurable vía `Jwt:AccessTokenMinutes` (por defecto **30 min**).
  `ExpiresInSeconds` en la respuesta indica la vigencia real.
- **Refresh token:** vida por defecto **14 días** (`Security:RefreshTokenDays`). Rotativo y revocable.

### 1.1 Roles y políticas (BE-SEC-05)

Las políticas se aplican por endpoint con `.RequireAuthorization("ROLE_...")`. `ROLE_ADMIN` está
incluido implícitamente en las demás. Roles disponibles:

`ROLE_ADMIN` · `ROLE_ANALYST` · `ROLE_OPERATOR` · `ROLE_COLLECTOR` · `ROLE_AUDITOR` · `ROLE_CLIENT`

---

## 2. Autenticación (`/api/auth`)

### 2.1 `POST /api/auth/login` — Login (BE-SEC-01, BE-SEC-02, BE-SEC-03)

Autenticación anónima. Según el rol y el estado de MFA devuelve tokens o un **desafío MFA**.

**Request**
```json
{ "email": "admin@loan.io", "password": "••••••••" }
```

**Response 200 — según `status`:**

- `AUTHENTICATED` (sin MFA requerido):
```json
{
  "status": "AUTHENTICATED",
  "accessToken": "eyJ...",
  "refreshToken": "f3a9...",
  "expiresInSeconds": 1800,
  "role": "ROLE_ANALYST",
  "mfaToken": null,
  "mfaExpiresInSeconds": null
}
```
- `MFA_REQUIRED` (rol con MFA ya enrolado → continuar en `/api/auth/mfa/verify`):
```json
{ "status": "MFA_REQUIRED", "mfaToken": "mfa_...", "mfaExpiresInSeconds": 300 }
```
- `MFA_ENROLLMENT_REQUIRED` (rol privilegiado sin MFA → continuar en `/api/auth/mfa/enroll`):
```json
{ "status": "MFA_ENROLLMENT_REQUIRED", "mfaToken": "mfa_...", "mfaExpiresInSeconds": 600 }
```

> MFA obligatorio para `ROLE_ADMIN` y `ROLE_OPERATOR` (configurable en `Security:Mfa:RequiredRoles`).

**Errores**
| HTTP | `code` | Causa |
|---|---|---|
| 400 | `Security.MissingCredentials` | Falta email o password |
| 401 | `Security.InvalidCredentials` | Email o password incorrectos |
| 403 | `Security.UserNotActive` | Cuenta `INACTIVE`/suspendida |
| 423 | `Security.AccountLocked` | Bloqueo tras 5 intentos fallidos en 15 min (BE-SEC-02) |

### 2.2 `POST /api/auth/mfa/verify` — Verificar segundo factor (BE-SEC-03)

Anónimo. Completa un desafío `MFA_REQUIRED` con un código TOTP o un **código de recuperación** de un solo uso.

**Request**
```json
{ "mfaToken": "mfa_...", "code": "123456" }
```
o
```json
{ "mfaToken": "mfa_...", "recoveryCode": "ABCD-EFGH-IJKL" }
```

**Response 200:** `AuthTokensResponse` (`accessToken`, `refreshToken`, `expiresInSeconds`, `role`).

### 2.3 `POST /api/auth/mfa/enroll` — Iniciar enrolamiento TOTP (BE-SEC-03)

Anónimo. Usa un `mfaToken` de tipo `ENROLL`. Genera el secreto y la URI para el QR.

**Request**
```json
{ "mfaToken": "mfa_..." }
```

**Response 200**
```json
{ "secret": "JBSWY3DPEHPK3PXP", "otpauthUri": "otpauth://totp/LoanSystem:admin@loan.io?secret=..." }
```

### 2.4 `POST /api/auth/mfa/enroll/confirm` — Confirmar enrolamiento (BE-SEC-03)

Anónimo. Confirma el enrolamiento con el primer código de 6 dígitos del autenticador; emite tokens y
devuelve los **códigos de recuperación de un solo uso** (mostrar una sola vez).

**Request**
```json
{ "mfaToken": "mfa_...", "code": "123456" }
```

**Response 200**
```json
{
  "tokens": { "accessToken": "eyJ...", "refreshToken": "f3a9...", "expiresInSeconds": 1800, "role": "ROLE_ADMIN" },
  "recoveryCodes": ["ABCD-EFGH", "IJKL-MNOP", "..."]
}
```

### 2.5 `POST /api/auth/refresh` — Rotar refresh token (BE-SEC-01)

Anónimo. Revoca el token presentado y emite un nuevo par en la **misma familia**. Reutilizar un token ya
rotado revoca **toda la familia** (detección de reúso, `DECISION_02`).

**Request**
```json
{ "refreshToken": "f3a9..." }
```

**Response 200:** `AuthTokensResponse`.

**Errores**
| HTTP | `code` | Causa |
|---|---|---|
| 400 | `Security.MissingRefreshToken` | Falta el token |
| 401 | `Security.InvalidRefreshToken` | Token inexistente, expirado o revocado |
| 401 | `Security.RefreshTokenReused` | Reúso detectado → familia revocada (se registra un warning con IP/User-Agent) |

### 2.6 `POST /api/auth/logout` — Cerrar sesión (BE-SEC-04)

Requiere autenticación. Si se envía `refreshToken` revoca ese token; si se omite, revoca **todas** las
sesiones activas del usuario autenticado.

**Request (opcional)**
```json
{ "refreshToken": "f3a9..." }
```
**Response:** 200/204 (vacío).

---

## 3. Perfil y gestión de usuarios

### 3.1 `GET /api/me` — Perfil y permisos efectivos (BE-SEC-04)

Requiere autenticación.

**Response 200**
```json
{
  "id": "…", "userId": "…", "fullName": "Ada Lovelace", "email": "ada@loan.io",
  "role": "ROLE_ADMIN",
  "permissions": ["dashboard.view", "audit.read", "approvals.users.role", "..."],
  "mfaEnabled": true
}
```

Permisos efectivos por rol (`RolePermissions.cs`):

| Rol | Permisos |
|---|---|
| `ROLE_ADMIN` | todos (incluye `*.manage`, `approvals.*`) |
| `ROLE_ANALYST` | `dashboard.view`, `customers.read`, `applications.read`, `loans.read`, `reports.read` |
| `ROLE_OPERATOR` | lo de Analyst + `payments.read`, `treasury.read`, `integrations.read` |
| `ROLE_COLLECTOR` | `dashboard.view`, `customers.read`, `loans.read`, `collections.read` |
| `ROLE_AUDITOR` | `dashboard.view`, `audit.read`, `reports.read`, `customers.read`, `loans.read`, `payments.read`, `treasury.read`, `approvals.read` |
| `ROLE_CLIENT` | `client.portal` |

### 3.2 `POST /api/users/{id}/suspend` — Suspender usuario (BE-SEC-06)

`ROLE_ADMIN`. Suspende la cuenta e **invalida al instante todos sus refresh tokens**. Response 200/204.

### 3.3 `POST /api/users/{id}/activate` — Reactivar usuario (BE-SEC-06)

`ROLE_ADMIN`. Reactiva una cuenta suspendida. Response 200/204.

### 3.4 `POST /api/users/{id}/mfa/reset` — Resetear MFA (BE-SEC-03)

`ROLE_ADMIN`. Borra la configuración MFA del usuario (deberá re-enrolar) y revoca sus sesiones activas.

### 3.5 `PUT /api/users/{id}/role` — Solicitar cambio de rol (BE-SEC-09)

`ROLE_ADMIN`. **No** cambia el rol directamente: crea una solicitud de **cuatro ojos**.

**Request**
```json
{ "newRole": "ROLE_OPERATOR", "reason": "Cambio de área aprobado por RRHH" }
```

**Response 202 Accepted** con `Location: /api/approvals/{id}` y el cuerpo `ApprovalRequestSummary`.

---

## 4. Auditoría (`GET /api/audit-logs`) — BE-SEC-08

`ROLE_AUDITOR` (y `ROLE_ADMIN`). Consulta el log inmutable con filtros y paginación.

**Query params** (admite nombres del frontend y del esquema):

| Param | Tipo | Nota |
|---|---|---|
| `entity` / `entityName` | string | Nombre de entidad |
| `entityId` | string | Id de la entidad |
| `action` | string | `INSERT` / `UPDATE` / `DELETE` |
| `userId` / `user` | guid / string | Autor de la acción |
| `from`, `to` | date (`YYYY-MM-DD`) | Rango de fechas |
| `page` | int | Por defecto `1` |
| `pageSize` | int | Por defecto `50` |
| `approvalRequestId` | guid | Trazabilidad de cuatro ojos |

**Response 200:** lista de `AuditLogItem`:
```json
[{
  "id": 1024, "entityName": "loans", "entityId": "…", "action": "UPDATE",
  "stateBefore": { }, "stateAfter": { },
  "userId": "…", "userName": "Ada", "userEmail": "ada@loan.io",
  "userIp": "10.0.0.5", "userAgent": "Mozilla/5.0 …",
  "createdAt": "2026-11-13T12:00:00Z", "approvalRequestId": null
}]
```

---

## 5. Aprobaciones de cuatro ojos (`/api/approvals`) — BE-SEC-09

Todos requieren autenticación. Operaciones sensibles (reversos de pago, castigos, ajustes, cambios de
tasa/rol, reestructuras) requieren un **segundo aprobador distinto del solicitante**.

### 5.1 `GET /api/approvals` — Listar solicitudes

**Query:** `status`, `operationType`, `targetId`, `requestedBy`, `scope` (`toDecide` | `mine` | `all`),
`page` (1), `pageSize` (20). **Response 200:** lista de `ApprovalRequestSummary`.

### 5.2 `GET /api/approvals/{id}` — Detalle

Devuelve `ApprovalRequestDto` con `payload`, `targetSnapshot` (estado al solicitar), `currentTargetState`
(estado actual), solicitante, fechas, y decisión si existe.

### 5.3 `POST /api/approvals/{id}/approve` — Aprobar y ejecutar

Aprueba y **ejecuta atómicamente** la operación. Requiere **step-up TOTP** (`mfaCode`).

**Request**
```json
{ "comment": "Revisado y validado", "mfaCode": "123456" }
```
**Response 200:** `ApprovalDecisionResponse` (`id`, `status`, `result` con entidad/id resultante).

### 5.4 `POST /api/approvals/{id}/reject` — Rechazar

**Comentario obligatorio.**
```json
{ "comment": "Falta evidencia del cliente" }
```

### 5.5 `POST /api/approvals/{id}/cancel` — Cancelar

Solo el **solicitante original** puede cancelar su solicitud pendiente.

---

## 6. Infraestructura

### 6.1 `GET /.well-known/jwks.json` — JWKS (BE-SEC-10)

Anónimo. Publica las claves públicas RS256 (activa + retenidas para rotación). Cacheable
(`Cache-Control: public, max-age=300`).

**Response 200**
```json
{ "keys": [ { "kty": "RSA", "use": "sig", "alg": "RS256", "kid": "…", "n": "…", "e": "AQAB" } ] }
```

### 6.2 `GET /api/security/ping` — Health del módulo

`ROLE_ADMIN`. Devuelve `{ "module": "Security", "status": "Ready" }`.

---

## 7. Resumen de endpoints

| Método | Ruta | Autorización | Requisito |
|---|---|---|---|
| POST | `/api/auth/login` | Anónimo | BE-SEC-01/02/03 |
| POST | `/api/auth/mfa/verify` | Anónimo | BE-SEC-03 |
| POST | `/api/auth/mfa/enroll` | Anónimo | BE-SEC-03 |
| POST | `/api/auth/mfa/enroll/confirm` | Anónimo | BE-SEC-03 |
| POST | `/api/auth/refresh` | Anónimo | BE-SEC-01 |
| POST | `/api/auth/logout` | Autenticado | BE-SEC-04 |
| GET | `/api/me` | Autenticado | BE-SEC-04 |
| POST | `/api/users/{id}/suspend` | ROLE_ADMIN | BE-SEC-06 |
| POST | `/api/users/{id}/activate` | ROLE_ADMIN | BE-SEC-06 |
| POST | `/api/users/{id}/mfa/reset` | ROLE_ADMIN | BE-SEC-03 |
| PUT | `/api/users/{id}/role` | ROLE_ADMIN | BE-SEC-09 |
| GET | `/api/audit-logs` | ROLE_AUDITOR | BE-SEC-08 |
| GET | `/api/approvals` | Autenticado | BE-SEC-09 |
| GET | `/api/approvals/{id}` | Autenticado | BE-SEC-09 |
| POST | `/api/approvals/{id}/approve` | Autenticado | BE-SEC-09 |
| POST | `/api/approvals/{id}/reject` | Autenticado | BE-SEC-09 |
| POST | `/api/approvals/{id}/cancel` | Autenticado | BE-SEC-09 |
| GET | `/.well-known/jwks.json` | Anónimo | BE-SEC-10 |
| GET | `/api/security/ping` | ROLE_ADMIN | — |
</content>
</invoke>
