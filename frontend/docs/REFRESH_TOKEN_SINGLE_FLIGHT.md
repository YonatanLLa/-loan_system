# Refresh token: un solo refresco a la vez (single-flight)

| Campo | Valor |
|---|---|
| Fecha | 2026-11-10 |
| Origen | Cambio en el backend, ver `backend/docs/DECISION_02_REFRESH_TOKEN_REUSE_DETECTION.md` |
| Afecta a | backoffice, portal, mobile |
| Paquete | `@prestamos/api-client` (`packages/api-client/src/http.ts`) y los stores de sesión de cada app |

## 1. Qué cambió en el backend

`POST /api/auth/refresh` ya rotaba el refresh token: cada uso revoca el token presentado y entrega uno nuevo. Ahora además:

1. **La rotación es atómica.** Si llegan dos refresh con el **mismo** token al mismo tiempo, solo uno tiene éxito. El otro recibe **401 `Security.InvalidRefreshToken`**. Antes los dos tenían éxito.
2. **Hay detección de reúso.** Si se presenta un token que **ya fue rotado** (por ejemplo, un segundo refresh que llega después de que el primero terminó), el backend asume que el token fue robado: **revoca toda la sesión** (la familia de tokens de ese login) y responde **401 `Security.RefreshTokenReused`**. Las sesiones de otros dispositivos del usuario no se tocan.

**Consecuencia para el frontend:** lanzar dos refresh en paralelo con el mismo token, o reintentar un refresh con un token ya usado, **cierra la sesión del usuario**. Antes no pasaba nada.

## 2. Requisito: single-flight

- **Un solo refresh en vuelo a la vez.** Todas las peticiones que reciben 401 deben esperar la **misma** promesa de refresco y luego reintentarse con el access token nuevo.
- **Un único punto de entrada.** Cualquier código que refresque (interceptor, restauración de sesión al recargar, timers) debe pasar por la misma promesa compartida.
- **Entre pestañas, si es posible.** Cada pestaña tiene su propio estado en memoria, así que dos pestañas pueden refrescar a la vez. Opciones:
  - Web Locks API: `navigator.locks.request('auth-refresh', () => refrescar())`. Luego cada pestaña, dentro del lock, verifica si otra ya obtuvo un token nuevo antes de llamar al backend.
  - `BroadcastChannel('auth')`: la pestaña que refresca avisa del resultado a las demás.
- **Nunca reintentar `/api/auth/refresh` con el mismo token** (ni por timeout ni por error de red). Si hay duda, se va al login.
- **Mobile:** guardar el refresh token nuevo **inmediatamente** al recibirlo. Si la app conserva el token viejo (por ejemplo, porque se cerró antes de guardarlo), el siguiente refresh se detecta como reúso y la sesión se cierra.

## 3. Qué hacer ante 401 del endpoint de refresh

| Respuesta de `/api/auth/refresh` | Significado | Acción |
|---|---|---|
| 200 | Token rotado | Guardar el access token (y en mobile, el refresh token nuevo) y reintentar las peticiones pendientes |
| 401 `Security.InvalidRefreshToken` | Token inválido, vencido, revocado (logout/suspensión) o se perdió una carrera | Limpiar la sesión y llevar al login |
| 401 `Security.RefreshTokenReused` | Se detectó reúso: la sesión completa fue revocada por seguridad | Limpiar la sesión y llevar al login. Se puede mostrar: "Tu sesión se cerró por seguridad. Vuelve a iniciar sesión." |

En ambos casos el mensaje (`detail`) es el mismo a propósito. Para distinguirlos hay que leer `code` del ProblemDetails. **No hay que reintentar el refresh** en ningún caso de 401.

## 4. Estado actual del código (solo lectura, 2026-11-10)

**`packages/api-client/src/http.ts`: sí tiene single-flight, pero solo dentro de una instancia del cliente (una pestaña).**

- La variable `refreshing: Promise<boolean> | null` y `refreshing ??= session.refresh().finally(() => { refreshing = null })` hacen que todas las respuestas 401 simultáneas de esa instancia compartan una sola llamada a `session.refresh()`.
- `original._retried` evita reintentar la misma petición más de una vez, y `skipAuthRefresh` evita que la propia llamada de refresco dispare otro refresco.
- **No coordina entre pestañas.** No usa `BroadcastChannel` ni Web Locks.
- **No distingue códigos de error.** Si `session.refresh()` devuelve `false`, llama a `session.onSessionExpired()` sin importar si fue `InvalidRefreshToken`, `RefreshTokenReused` o un error de red.

**Stores de sesión de backoffice y portal (`apps/*/src/app/stores/session.ts`):**

- `refreshAccessToken()` hace `POST /auth/refresh` con cuerpo `null` y `skipAuthRefresh: true`. Ante cualquier error llama a `clear()` y devuelve `false`.
- En backoffice, `ensure()` (restaurar la sesión al recargar) deduplica con **su propia** promesa `restoring` y llama directamente a `refreshAccessToken()`. Esa promesa es **distinta** de `refreshing` en `http.ts`, así que un refresco iniciado por `ensure()` y otro iniciado por el interceptor ante un 401 **pueden correr en paralelo**. Con el backend nuevo eso termina en 401 y posiblemente en reúso detectado.
- Los comentarios asumen que el refresh token viaja en una **cookie HttpOnly**. El backend actual (`RefreshRequest`) espera el token en el cuerpo JSON (`{ "refreshToken": "..." }`) y lo devuelve en el cuerpo de la respuesta, sin cookie. El contrato está marcado como "provisional" en el store y hay que alinearlo aparte.

**Mobile (`apps/mobile/src/lib/api.ts`):**

- `session.refresh()` lee el refresh token del almacén seguro, hace `POST /api/auth/refresh` con `{ refreshToken }` y guarda el token nuevo con `saveRefreshToken`. Ante cualquier error devuelve `false`.
- Usa el mismo `createApiClient`, así que hereda el single-flight de `http.ts` dentro de la app.

## 5. Cambios sugeridos (no aplicados)

1. Hacer que `ensure()` (backoffice/portal) use la misma promesa compartida que el interceptor, por ejemplo exponiendo un `refreshOnce()` desde el cliente o moviendo la deduplicación al store.
2. Coordinar entre pestañas con Web Locks o `BroadcastChannel`.
3. Leer `code` del ProblemDetails del refresh para mostrar el aviso de cierre por seguridad cuando sea `Security.RefreshTokenReused`.
4. Alinear el transporte del refresh token (cookie HttpOnly frente a cuerpo JSON) entre frontend y backend.
