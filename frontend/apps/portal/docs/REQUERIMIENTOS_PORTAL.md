# Requerimientos — Portal del Cliente (`apps/portal`)

App de autoservicio **mobile-first** para el prestatario. Canal 100% digital, parte del MVP.

**Convenciones de ID:** `FE-CP-NN`
**Fases:** `MVP` · `F2` (operación estable) · `F3` (integraciones enterprise)
**Estados:** ✅ Implementado · 🔲 Especificado (pendiente)

> **Regla transversal:** el portal **no calcula dinero**. Cuotas, mora, prepagos y DTI
> siempre vienen del backend (incluido el simulador). El frontend solo formatea y muestra.

---

## 1. Acceso y Registro

| ID | Requerimiento | Fase | Estado | Archivo |
|---|---|---|---|---|
| FE-CP-01 | Registro con documento, nombres, correo, celular y contraseña; verificación por OTP (SMS o WhatsApp); countdown de reenvío | MVP | ✅ | `features/onboarding/pages/RegisterPage.vue` |
| FE-CP-02 | Login del cliente y cierre de sesión automático tras 15 minutos de inactividad | MVP | ✅ | `features/onboarding/pages/LoginPage.vue` · `composables/useClientAuth.ts` |

---

## 2. Onboarding KYC y Configuración Inicial

| ID | Requerimiento | Fase | Estado | Archivo |
|---|---|---|---|---|
| FE-CP-03 | Onboarding KYC con la cámara del celular: DNI anverso y reverso + selfie con prueba de vida; estado del KYC visible | MVP | ✅ | `features/onboarding/pages/OnboardingPage.vue` (pasos 1 y 2) |
| FE-CP-04 | Aceptación de consentimientos separados por finalidad (tratamiento de datos, consulta a centrales de riesgo, marketing), con enlace al texto vigente — Ley N° 29733 | MVP | ✅ | `features/onboarding/pages/OnboardingPage.vue` (paso 3) |
| FE-CP-05 | Registrar la cuenta de desembolso (CCI de 20 dígitos o billetera Yape/Plin) y ver si está verificada | MVP | ✅ | `features/onboarding/pages/OnboardingPage.vue` (paso 4) |

---

## 3. Simulador y Solicitud de Préstamo

| ID | Requerimiento | Fase | Estado | Archivo |
|---|---|---|---|---|
| FE-CP-06 | Simulador interactivo con sliders de monto y plazo; el cálculo lo hace el backend; muestra TEA, TCEA, cuota mensual y costo total; cronograma proyectado desplegable | MVP | ✅ | `features/applications/pages/ApplyPage.vue` · `composables/useSimulator.ts` |
| FE-CP-07 | Solicitud de préstamo con destino del crédito, datos laborales (empleador, ocupación, ingreso neto) y adjunto de sustento de ingresos (boleta o RxH) | MVP | ✅ | `features/applications/pages/ApplyPage.vue` · `composables/useLoanApplication.ts` |
| FE-CP-08 | Línea de tiempo de la solicitud: `SOLICITADO` → `EN_EVALUACION` → `APROBADO` → `DESEMBOLSADO`; dots animados por estado activo; acceso directo a firma cuando está aprobada | MVP | ✅ | `features/applications/pages/ApplicationsPage.vue` · `composables/useApplicationsTimeline.ts` |

---

## 4. Contrato y Desembolso

| ID | Requerimiento | Fase | Estado | Archivo |
|---|---|---|---|---|
| FE-CP-09 | Revisión completa del contrato y la hoja resumen SBS (con TEA y TCEA congeladas); aceptación con OTP **antes** del desembolso; sello de evidencia legal (fecha, IP, OTP); descarga del PDF firmado | MVP | ✅ | `features/contract/pages/ContractPage.vue` · `composables/useContractSigning.ts` |

---

## 5. Mis Préstamos y Cronograma

| ID | Requerimiento | Fase | Estado | Archivo |
|---|---|---|---|---|
| FE-CP-10 | Vista resumen del préstamo activo: monto inicial, saldo insoluto, TEA aplicada, plazo, **días restantes o de atraso** y **mora acumulada del día** | MVP | ✅ | `features/loans/pages/LoansPage.vue` · `composables/useClientLoans.ts` · `composables/useLoanSchedule.ts` |
| FE-CP-11 | Cronograma oficial semaforizado por colores: 🟢 verde pagada · 🟡 amarillo próxima · 🔴 rojo vencida · 🔵 azul pago parcial; leyenda visible | MVP | ✅ | `features/loans/pages/LoansPage.vue` · `composables/useLoanSchedule.ts` |

---

## 6. Pagos

| ID | Requerimiento | Fase | Estado | Archivo |
|---|---|---|---|---|
| FE-CP-12 | Pagar una cuota por tres canales: QR dinámico Yape/Plin con el monto exacto · formulario alojado PCI-DSS para tarjeta (datos nunca tocan el backend) · reporte de transferencia con voucher adjunto | MVP | ✅ | `features/payments/pages/PayPage.vue` · `composables/usePaymentGateway.ts` |
| FE-CP-13 | Pago reportado con voucher se muestra como **"En validación"** hasta que se concilie; nunca se marca como pagado de forma anticipada | MVP | ✅ | `features/payments/pages/PayPage.vue` (step `IN_VALIDATION`) |
| FE-CP-14 | Confirmación en tiempo real vía webhook cuando se acredita el pago; descarga del recibo oficial PDF con QR de verificación | MVP | ✅ | `features/payments/pages/PayPage.vue` (step `CONFIRMED`) |
| FE-CP-15 | Historial de pagos con filtros por estado (Todos · En Validación · Aplicados) y descarga de comprobante PDF por pago | MVP | ✅ | `features/payments/pages/PayPage.vue` (pestaña `HISTORY`) · `composables/usePaymentHistory.ts` |
| FE-CP-16 | Cotizar la cancelación anticipada total con desglose (capital, interés devengado, mora, penalidad 0%) y vigencia de cotización; proceder al pago desde el resumen | MVP | ✅ | `features/payments/pages/PayPage.vue` (pestaña `PREPAY_TOTAL`) · `composables/usePrepayment.ts` |
| FE-CP-17 | Abono extraordinario a capital con elección entre **reducir la cuota** o **reducir el plazo**; comparativo de impacto con ahorro proyectado (Ley 29571 art. 85) | F2 | ✅ | `features/payments/pages/PayPage.vue` (pestaña `PREPAY_PARTIAL`) |

---

## 7. Perfil y Cuentas Bancarias

| ID | Requerimiento | Fase | Estado | Archivo |
|---|---|---|---|---|
| FE-CP-18 | Cambiar la cuenta de desembolso (existente o nueva CCI/billetera) con verificación OTP obligatoria y aviso del periodo de espera de 24 horas (cooling-off) antes de que se active para desembolsos | MVP | ✅ | `features/profile/pages/ProfilePage.vue` · `composables/useProfile.ts` |
| FE-CP-19 | Solicitar la devolución del saldo a favor a una cuenta verificada; historial de solicitudes de devolución con estado | F2 | ✅ | `features/profile/pages/ProfilePage.vue` |

---

## 8. Notificaciones

| ID | Requerimiento | Fase | Estado | Archivo |
|---|---|---|---|---|
| FE-CP-20 | Centro de notificaciones (vencimientos, pagos confirmados, desembolsos) con bandeja filtrable (Todas / No leídas), marcar como leído y preferencias de canal (WhatsApp, Email, SMS) con días de anticipación configurables | F2 | ✅ | `features/notifications/pages/NotificationsPage.vue` · `composables/useNotifications.ts` |

---

## 9. Libro de Reclamaciones

| ID | Requerimiento | Fase | Estado | Archivo |
|---|---|---|---|---|
| FE-CP-21 | **Libro de Reclamaciones Virtual** accesible desde el portal; formulario oficial con tipo (RECLAMO / QUEJA), identificación del consumidor, bien contratado, detalle y pedido; emisión de correlativo oficial; plazo de atención 15 días hábiles; consulta del estado por correlativo | MVP | ✅ | `features/complaints/pages/ComplaintsPage.vue` · `composables/useComplaints.ts` |

---

## 10. Requerimientos No Funcionales (Portal)

| ID | Requerimiento | Estado | Notas |
|---|---|---|---|
| FE-NF-01 | Portal 100% mobile-first: usable a 360 px de ancho, carga ligera en redes móviles | ✅ | Tailwind responsive, bottom nav en mobile |
| FE-NF-01b | Modo PC (≥ 1024 px): header con nav horizontal, contenido hasta `max-w-6xl` | ✅ | `AppLayout.vue` con breakpoint `lg` |
| FE-NF-02 | Sesión expira a los 15 min de inactividad | ✅ | `composables/useClientAuth.ts` |
| FE-NF-04 | Toda mutación que mueve dinero envía `Idempotency-Key`; botón deshabilitado mientras espera | ✅ | `usePaymentGateway` · `useContractSigning` |
| FE-NF-05 | Errores leídos del formato `ProblemDetails` (RFC 7807) mostrados con `detail` en lenguaje claro | ✅ | `toApiError()` en `@prestamos/api-client` |
| FE-NF-06 | Formato local `es-PE`: moneda `S/ 1,234.56`, fechas `dd/mm/aaaa`, hora de Lima | ✅ | `MoneyText` · `formatBusinessDate` · `formatInstant` de `@prestamos/utils` |
| FE-NF-09 | Datos sensibles enmascarados en pantalla (CCI parcial, documento parcial) | ✅ | Cuentas mostradas con `accountNumberMasked` |

---

## 11. Versión 2 — Requerimientos Especificados (pendientes de implementar)

| ID | Requerimiento | Fase | Estado |
|---|---|---|---|
| FE-CP-22 | Banner y flujo 1-Click de oferta pre-aprobada (Credit Ladder / Tier) | V2 | 🔲 Especificado |
| FE-CP-23 | Simulador con período de gracia y desglose de desgravamen / ITF en cronograma | V2 | 🔲 Especificado |
| FE-CP-24 | Banner de oferta de liquidación / descuento de mora (Waiver activo) con cuenta regresiva | V2 | 🔲 Especificado |
| FE-CP-25 | Pago de cuota inicial para activar convenio de reestructuración (`PENDING_DOWN_PAYMENT`) | V2 | 🔲 Especificado |
| FE-CP-26 | Afiliación y gestión de tarjetas tokenizadas para débito automático (Auto-Pay) + mandato OTP | V2 | 🔲 Especificado |
| FE-CP-27 | Descarga de comprobantes electrónicos SUNAT (Boleta / Factura / Nota de Crédito) por pago | V2 | 🔲 Especificado |
| FE-CP-28 | Registro de datos de aval / cónyuge desde el portal con envío de enlace de verificación | V2 | 🔲 Especificado |
| FE-CP-29 | Portal de firma electrónica OTP para interviniente (Aval / Cónyuge) por enlace seguro | V2 | 🔲 Especificado |

---

## Resumen de Cobertura

| Categoría | Total | Implementados | Pendientes (V2) |
|---|---|---|---|
| MVP | 15 | 15 | 0 |
| F2 | 6 | 6 | 0 |
| V2 | 8 | 0 | 8 |
| **Total** | **29** | **21** | **8** |

> Los 21 requerimientos MVP + F2 están implementados en la rama `frontend-vue-arquitectura`.
> Los 8 requerimientos V2 están especificados en `docs/REQUERIMIENTOS_FRONTEND.md §5.2`
> y pendientes de desarrollo en la siguiente iteración.
