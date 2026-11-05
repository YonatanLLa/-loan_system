# @prestamos/mocks — API simulada

Responde `/api/*` con datos semilla y **estado en memoria** (se reinicia al recargar la página), para desarrollar el frontend mientras no exista el backend. Usa [MSW](https://mswjs.io): intercepta las peticiones reales de Axios, así que el código del frontend es el mismo que usará con la API .NET.

Implementa las rutas de `docs/REQUERIMIENTOS_BACKEND.md`. Los tipos de entrada y salida están en `packages/api-client/src/contracts.ts` (provisionales hasta tener el OpenAPI).

## Activarla

- **Desarrollo:** `VITE_USE_MOCKS=true` en `apps/*/.env.development` (ya viene activada). Con `false` las apps usan el proxy hacia el backend real (`VITE_API_PROXY_TARGET`).
- **Producción:** `VITE_USE_MOCKS=false`; el código de las mocks y el service worker **no entran** al build.
- **Pruebas:** `import { server } from '@prestamos/mocks/node'` + `resetDb()` en `beforeEach` (ver `src/__tests__/api.spec.ts`).

## Usuarios (clave `Demo1234!`)

| Correo | Rol | Entra a |
|---|---|---|
| `admin@sistema.local` | Administrador | Backoffice, todo |
| `analista@sistema.local` | Analista | Clientes, solicitudes, préstamos, reportes |
| `operador@sistema.local` | Operador | + pagos, tesorería, integraciones |
| `cobranza@sistema.local` | Cobranza | Clientes, préstamos, cobranza |
| `auditor@sistema.local` | Auditor | Auditoría, reportes y consulta |
| `cliente@correo.pe` (o el celular `987000000`) | Cliente | Portal (`/client/login`) |

## Escenarios listos

| Qué | Dónde |
|---|---|
| Préstamo al día | `loan-001` (María Quispe, portal) |
| Préstamo en mora con mora devengada | `loan-002` (Carlos Ramírez, ~45 días) y `loan-003` (Lucía Fernández) |
| Préstamo pagado | `loan-004` |
| Cuota por vencer en 5 días (cobranza preventiva) | `loan-005` |
| Mora > 180 días, elegible para castigo | `loan-007` (Rosa Chávez) |
| Promesa de pago incumplida | `loan-002` |
| Solicitud aprobada lista para desembolsar | `app-006` (José Mendoza) — su CCI termina en `0000`: **el banco la rechaza** a los 6 s |
| Desembolso ya fallido en la bandeja | `dis-` de `loan-006` (reintento: `POST /disbursements/:id/retry`) |
| Cliente con KYC pendiente / rechazado | `cus-005`, `cus-006` (el desembolso responde 422) |
| Pago en validación (voucher) | `OP-445566`; se concilia con `POST /reconciliation/import` |
| Eventos de integración en DLQ | `GET /integrations/events` |

## Comportamientos simulados

- **Desembolso asíncrono:** `PENDING` → `PROCESSING` (2 s) → `COMPLETED` o `FAILED` (6 s). El cronograma se genera solo al completarse.
- **Pagos con QR/tarjeta:** se confirman solos a los 5 s (webhook simulado); el voucher por transferencia queda `PENDING_CONFIRMATION`.
- **Prelación:** mora → interés → comisión → capital, por cuota más antigua; el excedente va a saldo a favor.
- **Reverso:** contra-asiento; el segundo reverso responde `409`.
- **Idempotencia:** misma `Idempotency-Key` ⇒ misma respuesta, sin duplicar.
- **Seguridad:** `401` sin token, `403` sin permiso, `404` en préstamos ajenos (portal), `423` tras 5 contraseñas erróneas.
- **Errores:** formato `ProblemDetails` con `errors` por campo (`Amount`, `Term`…), igual que el backend real.
- **Dinero:** montos como texto (`"1250.50"`), calculados en centavos enteros; el capital siempre cuadra.

## Endpoints propuestos (no están aún en las historias)

`PATCH /customers/:id/kyc`, `GET|POST /customers/:id/bank-accounts`, `POST /customer-bank-accounts/:id/verify`, `POST /loan-applications/:id/start-review`, `GET /loans/:id/payments`, `POST /payments/:id/confirm`, `POST /disbursements/:id/retry`, `POST /reconciliation/:id/match`, `GET /integrations/events`, `POST /integrations/events/:id/retry`, `GET /loans/:id/collection-actions`, `GET /client/*` (préstamos, estado de cuenta, cuentas, pagos).

## Limitaciones

- No hay OTP, carga real de archivos ni PDFs reales (devuelve un PDF mínimo).
- El refresco de sesión se simula con `localStorage` (el real usará una cookie `HttpOnly`).
- Los permisos (`customers.read`, `payments.reverse`…) son provisionales hasta que el backend publique los suyos.
