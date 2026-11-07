# Requerimientos del móvil — Portal del cliente

> App `@prestamos/mobile`. Reúne los requerimientos del **cliente prestatario**
> (`ROLE_CLIENT`) en su versión nativa. Los códigos `FE-CP-*` y `FE-NF-*` se reusan de
> [`docs/REQUERIMIENTOS_FRONTEND.md`](../../../../docs/REQUERIMIENTOS_FRONTEND.md) para mantener
> trazabilidad; los códigos **`MOV-*`** son propios de móvil nativo.

---

## 1. Alcance

El móvil replica el **portal del cliente**: onboarding, KYC, solicitud, firma, cronograma y
pagos. **El backoffice no aplica.** Todas las operaciones van contra endpoints `/api/client/*`
con `Authorization: Bearer <JWT ROLE_CLIENT>` y aislamiento por `customer_id` (el backend
responde `403`/`404` ante accesos cruzados).

Leyenda de fase: **MVP** = mínimo viable · **F2** = operación · **F3** = enterprise.

---

## 2. Requerimientos funcionales (reusan `FE-CP-NN`)

### 2.1 Autenticación y onboarding

| ID | Requerimiento | Endpoint(s) | Fase | Adaptación móvil |
|---|---|---|---|---|
| FE-CP-01 | Registro con verificación OTP (SMS/WhatsApp, 6 dígitos) | `POST /api/client/register`, `POST /api/client/verify-otp` | MVP | Teclado numérico, autollenado de OTP del SMS |
| FE-CP-02 | Login del cliente + cierre automático a los 15 min de inactividad | `POST /api/client/login` | MVP | Inactividad vía `AppState`; reingreso biométrico (`MOV-03`) |
| FE-CP-03 | Onboarding KYC: DNI anverso/reverso + selfie con prueba de vida | `POST /api/client/documents` | MVP | Cámara nativa (`expo-camera`, `MOV-05`); compresión antes de subir |
| FE-CP-04 | Consentimientos separados por finalidad (datos, consulta crediticia, marketing) | — (parte del onboarding) | MVP | Checkboxes nativos, registro de aceptación |
| FE-CP-05 | Registrar cuenta de desembolso (CCI 20 díg. o billetera) y ver su verificación | `POST /api/client/bank-accounts`, `PUT /api/client/bank-accounts/{id}/verify` | MVP | Validación `cci` de `@prestamos/utils`; cuenta a nombre del cliente |

### 2.2 Solicitud de préstamo

| ID | Requerimiento | Endpoint(s) | Fase | Adaptación móvil |
|---|---|---|---|---|
| FE-CP-06 | Simulador interactivo monto/plazo → cuota, TEA, TCEA, costo total (cálculo en backend) | `POST /api/client/loan-applications/simulate`, `GET /api/loan-products` | MVP | Sliders nativos; recálculo con debounce |
| FE-CP-07 | Solicitud con datos laborales + adjuntos de ingresos (boleta/recibo) | `POST /api/client/loan-applications` | MVP | Adjuntar desde cámara o archivos |
| FE-CP-08 | Línea de tiempo: `SOLICITADO` → `EN_EVALUACION` → `APROBADO` → `DESEMBOLSADO` | `GET /api/client/loan-applications/{id}` | MVP | Stepper vertical |

### 2.3 Contrato

| ID | Requerimiento | Endpoint(s) | Fase | Adaptación móvil |
|---|---|---|---|---|
| FE-CP-09 | Revisar contrato/hoja resumen, **aceptar con OTP** antes del desembolso, descargar PDF firmado | `GET /api/client/loans/{id}/documents/contract.pdf` | MVP | Visor PDF nativo; botón "Aceptar" habilitado al llegar al final; OTP de conformidad |

### 2.4 Préstamos y cronograma

| ID | Requerimiento | Endpoint(s) | Fase | Adaptación móvil |
|---|---|---|---|---|
| FE-CP-10 | "Mis préstamos": próximo vencimiento, días restantes/atraso, saldo insoluto, mora | `GET /api/client/loans`, `GET /api/client/loans/{id}` | MVP | Tarjetas; pull-to-refresh |
| FE-CP-11 | Cronograma con colores: verde `PAID`, amarillo `PENDING`, rojo `OVERDUE`, azul `PARTIAL` | `GET /api/client/loans/{id}/schedule` | MVP | Reusa catálogo de tonos de `@prestamos/ui/status.ts` |
| FE-CP-15 | Historial de pagos + estado de cuenta descargable | `GET /api/client/loans/{id}/statement.pdf` | MVP | Compartir/guardar PDF nativo |
| FE-CP-16 | Cotizar y pagar cancelación anticipada **total** (prepago) | (prepago sobre `/api/client/payments`) | MVP | Idempotente (`MOV-06`) |
| FE-CP-17 | Abono extraordinario (prepago **parcial**) reduciendo cuota o plazo | (prepago sobre `/api/client/payments`) | F2 | Ley 29571 art. 85 |

### 2.5 Pagos

| ID | Requerimiento | Endpoint(s) | Fase | Adaptación móvil |
|---|---|---|---|---|
| FE-CP-12 | Pagar cuota: QR dinámico (Yape/Plin), pasarela (tarjeta tokenizada), reporte de transferencia con voucher | `POST /api/client/payments` (con `Idempotency-Key`) | MVP | Datos de tarjeta solo en checkout alojado; deep link de retorno (`MOV-04`) |
| FE-CP-13 | Pago con voucher → estado "En validación" (nunca como pagado hasta conciliación) | `POST /api/client/payments` | MVP | El pago nace `PENDING_CONFIRMATION` |
| FE-CP-14 | Confirmación en tiempo real por webhook + recibo PDF con QR de verificación | `GET /api/client/payments/{id}/receipt.pdf` | MVP | Polling o push (`MOV-01`) hasta confirmar |

### 2.6 Perfil, notificaciones y reclamos

| ID | Requerimiento | Endpoint(s) | Fase | Adaptación móvil |
|---|---|---|---|---|
| FE-CP-18 | Cambiar cuenta de desembolso con **OTP nuevo** + aviso de espera **24–48 h** | `PUT /api/client/bank-accounts/{id}/set-primary` | MVP | Verificación adicional; durante la espera se desembolsa a la cuenta anterior (anti SIM-swap) |
| FE-CP-19 | Solicitar devolución de saldo a favor a cuenta verificada | (sobre `bank-accounts` + pagos) | F2 | — |
| FE-CP-20 | Centro de notificaciones: vencimientos, pagos confirmados, desembolsos | — | F2 | Integrado con push (`MOV-01`) |
| FE-CP-21 | Libro de Reclamaciones virtual | — | MVP | Formulario nativo; normativa de protección al consumidor |

---

## 3. Requerimientos nativos nuevos (`MOV-NN`)

Lo que no existe en la web y es propio de una app móvil.

| ID | Requerimiento | Librería | Fase | Detalle |
|---|---|---|---|---|
| **MOV-01** | **Notificaciones push**: vencimiento próximo, pago confirmado, desembolso realizado | `expo-notifications` | MVP | Registrar device token contra el backend; respeta las preferencias de FE-CP-20 |
| **MOV-02** | **Almacenamiento seguro** del refresh token (Keychain / Keystore) | `expo-secure-store` | MVP | Equivalente móvil de la cookie HttpOnly (FE-NF-08). Ya implementado en `src/lib/api.ts` |
| **MOV-03** | **Desbloqueo biométrico** opcional (Face ID / huella) para reingreso y operaciones sensibles | `expo-local-authentication` | F2 | Alternativa al OTP en reingreso; no sustituye el OTP del backend para firmar/cambiar cuenta |
| **MOV-04** | **Deep links / universal links** (scheme `prestamos`) | expo-router + `expo-linking` | MVP | Retorno desde la pasarela de pago y apertura desde notificaciones |
| **MOV-05** | **Permisos de cámara** para KYC + manejo de denegación | `expo-camera` | MVP | Soporta FE-CP-03; mensaje claro y ruta a Ajustes si se deniega |
| **MOV-06** | **Resiliencia de red**: pérdida de conexión y reintentos idempotentes | `@prestamos/api-client` + `@react-native-community/netinfo` | MVP | Reutiliza `Idempotency-Key` para evitar doble cobro al reintentar |
| **MOV-07** | **Distribución** en App Store / Google Play + OTA | EAS Build + `expo-updates` | MVP | Versionado y parches de JS sin review |
| **MOV-08** | **Protección de pantallas sensibles** (ocultar en app-switcher, bloquear captura) | `expo-screen-capture` | F2 | Oculta montos/datos en multitarea y screenshots |

---

## 4. Requerimientos no funcionales

### 4.1 Mapeados desde `FE-NF-*`

| ID | Requisito | Aplicación en móvil |
|---|---|---|
| FE-NF-03 | Tipos generados desde OpenAPI (prohibido DTOs a mano) | `pnpm gen:api` → `@prestamos/api-client` consumido por el móvil |
| FE-NF-04 | Idempotencia en operaciones de dinero | `createIdempotentAction` / `Idempotency-Key` reutilizados |
| FE-NF-05 | Errores normalizados RFC 7807 → `ApiError` | `toApiError` + toast nativo + `toFormErrors` a React Hook Form |
| FE-NF-06 | Formato local es-PE (`S/ 1,234.56`, `dd/mm/aaaa`, zona Lima) | `formatMoney`, `formatBusinessDate` de `@prestamos/utils` |
| FE-NF-08 | Access token en memoria; refresh en almacenamiento seguro | Memoria + `expo-secure-store` (ver `MOV-02`) |
| FE-NF-09 | Datos sensibles enmascarados (`**** 4821`) | `maskTail` de `@prestamos/utils` en cuentas/documentos |

### 4.2 Reglas de seguridad del portal (`ARQUITECTURA_GENERAL.md` §9)

| Regla | Aplicación en móvil |
|---|---|
| Aislamiento por `customer_id` | El cliente solo ve/edita recursos propios; backend responde `403`/`404` |
| OTP (SMS/WhatsApp) para operaciones sensibles | Firmar contrato, cambiar cuenta, ver datos sensibles |
| Espera 24–48 h en cambio de cuenta | Mitiga SIM-swap; desembolso a la cuenta previa durante la espera |
| Sesión 15 min de inactividad (RNF-19) | Cierre automático vía `AppState` + temporizador |
| Diseño mobile-first (RNF-18) | La app es nativa; usable desde 360 px, cargas ligeras |
| Documentos en Object Storage con URLs firmadas (RNF-04) | Descargas por URL temporal; nada sensible en disco plano |

---

## 5. Trazabilidad (requerimiento → documento fuente)

| Requerimiento | Documento fuente |
|---|---|
| FE-CP-01..05 (auth/onboarding/KYC) | [`docs/09-portal-del-cliente.md`](../../../../docs/09-portal-del-cliente.md), [`docs/02-gestion-de-clientes-y-kyc.md`](../../../../docs/02-gestion-de-clientes-y-kyc.md) |
| FE-CP-06..08 (simulación/solicitud) | [`docs/04-originacion-y-evaluacion.md`](../../../../docs/04-originacion-y-evaluacion.md), `docs/09-portal-del-cliente.md` |
| FE-CP-09 (contrato), FE-CP-16/17 (prepago) | [`docs/05-desembolso-y-cronogramas.md`](../../../../docs/05-desembolso-y-cronogramas.md), `docs/09-portal-del-cliente.md` |
| FE-CP-10..15 (préstamos/cronograma/pagos) | `docs/09-portal-del-cliente.md`, [`docs/06-pagos-y-cobros.md`](../../../../docs/06-pagos-y-cobros.md), [`docs/07-mora-y-cobranzas.md`](../../../../docs/07-mora-y-cobranzas.md) |
| FE-CP-18..21 (perfil/notif./reclamos) | `ARQUITECTURA_GENERAL.md` §9, `docs/06-pagos-y-cobros.md` |
| FE-NF-* y reglas de seguridad | [`docs/REQUERIMIENTOS_FRONTEND.md`](../../../../docs/REQUERIMIENTOS_FRONTEND.md), [`ARQUITECTURA_GENERAL.md`](../../../../ARQUITECTURA_GENERAL.md) §9 |
| MOV-01..08 | Propios del móvil (esta arquitectura: [`ARQUITECTURA_MOBILE.md`](./ARQUITECTURA_MOBILE.md)) |

---

## Referencias

- Arquitectura del móvil: [`ARQUITECTURA_MOBILE.md`](./ARQUITECTURA_MOBILE.md)
- Requerimientos del frontend web: [`docs/REQUERIMIENTOS_FRONTEND.md`](../../../../docs/REQUERIMIENTOS_FRONTEND.md)
- Dominio del portal cliente: [`docs/09-portal-del-cliente.md`](../../../../docs/09-portal-del-cliente.md)
