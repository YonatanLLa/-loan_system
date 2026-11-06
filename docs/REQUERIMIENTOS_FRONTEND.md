# Requerimientos de Frontend — Sistema de Préstamos (Fintech 100% Digital)

Requerimientos de las dos aplicaciones web del sistema, derivados de las historias de usuario de `docs/01` a `docs/13`, del [`README.md`](../README.md) y de [`ARQUITECTURA_GENERAL.md`](../ARQUITECTURA_GENERAL.md).

- **Backoffice:** panel interno para analistas, operadores, tesorería, cobranza, auditoría y administración.
- **Portal del cliente:** autoservicio *mobile-first* para el prestatario. En un modelo 100% digital es el único canal del cliente, así que **forma parte del MVP**.

Documentos hermanos: [Backend](REQUERIMIENTOS_BACKEND.md) · [Base de datos](REQUERIMIENTOS_BDD.md) · Arquitectura y stack (Vue 3 + shadcn-vue): [`ARQUITECTURA_FRONTEND.md`](../ARQUITECTURA_FRONTEND.md)

**Convenciones**

| Campo | Valores |
|---|---|
| **ID** | `FE-BO-NN` (Backoffice), `FE-CP-NN` (Portal cliente), `FE-NF-NN` (no funcional) |
| **Origen** | Historia o requerimiento de `docs/` (US / RF / RNF) o sección del README |
| **Fase** | `MVP`, `F2` (operación estable) o `F3` (integraciones enterprise) |
| **Estado** | **Especificado**: ya está en las historias · **Parcial**: el negocio lo pide y las historias lo cubren a medias · **Nuevo**: el negocio o el modelo digital lo requieren y no estaba en las historias |

> Regla transversal: el frontend **no calcula dinero**. Cuotas, mora, prepagos y DTI siempre vienen del backend (incluido el simulador). El frontend solo formatea y muestra.

---

## 1. Backoffice

### 1.1 Acceso y seguridad

| ID | Requerimiento | Origen | Fase | Estado |
|---|---|---|---|---|
| FE-BO-01 | Login con correo y contraseña; mensajes genéricos ante credenciales inválidas; pantalla de cuenta bloqueada (`423`) | US-01, RF-01.1–01.3 | MVP | Implementado |
| FE-BO-02 | Segundo factor (TOTP) obligatorio para los roles que mueven dinero (Admin, Operador) | ARQUITECTURA_GENERAL §9 | MVP | Implementado |
| FE-BO-03 | Menú, rutas y botones según los permisos de `GET /api/me`: lo que el rol no puede hacer no se muestra (el backend vuelve a validar) | US-02, RF-01.5, RF-02.2 | MVP | Implementado |
| FE-BO-04 | Cierre de sesión que revoca el *refresh token* y expulsión inmediata si el usuario es suspendido (`401`) | RF-01.4, RF-43.3 | MVP | Implementado |
| FE-BO-05 | Bitácora de auditoría con filtros por entidad, fechas, usuario y acción, y vista "antes / después" del JSON | US-03, RF-03.4 | MVP | Implementado |

### 1.2 Clientes y KYC

| ID | Requerimiento | Origen | Fase | Estado |
|---|---|---|---|---|
| FE-BO-06 | Lista y búsqueda de clientes por documento o nombre; alta y edición con validación de formato según el tipo de documento (regex del catálogo) | US-04, RF-04.1, RF-42.1 | MVP | Implementado |
| FE-BO-07 | Mensaje claro ante documento duplicado (`409`) | CA-04.2 | MVP | Implementado |
| FE-BO-08 | Expediente KYC: visor de DNI, selfie y sustentos; resultado de las verificaciones automáticas (identidad, biometría, listas PEP/AML); aprobar o rechazar con notas | US-05, RF-05.1–05.2 | MVP | Implementado |
| FE-BO-09 | Cuentas bancarias del cliente: ver CCI o billetera enmascarados, estado de verificación, marcar como primaria y verificar titularidad | Modelo digital (`customer_bank_accounts`) | MVP | Implementado |
| FE-BO-10 | Ficha 360: préstamos activos e históricos, deuda consolidada, atraso máximo, solicitudes pasadas y alerta destacada de mora | US-06, RF-06.1 | MVP | Implementado |

### 1.3 Productos, simulación y originación

| ID | Requerimiento | Origen | Fase | Estado |
|---|---|---|---|---|
| FE-BO-11 | Alta y edición de productos: moneda, montos, plazos, frecuencias, método de amortización, TEA, comisiones (fija o %, deductiva o financiada), cargos periódicos, gracia, mora (tasa y base), anatocismo, penalidad de prepago, elegibilidad y ajuste por feriados; validación de rangos antes de enviar | US-07, US-08, US-09 | MVP | Implementado |
| FE-BO-12 | Configurar el **día de pago** del producto y el **DTI máximo** permitido (hoy fijo en 35 % / 50 %) | README §3.1 y §3.10 | MVP | Implementado |
| FE-BO-13 | Simulador: monto, plazo y frecuencia → resumen (TEA, TEM, total de intereses, comisiones, costo total) y cronograma proyectado | US-11, RF-11.2–11.3 | MVP | Implementado |
| FE-BO-14 | Mostrar la **TCEA** (tasa de costo efectivo anual) en el simulador y en la hoja resumen | Transparencia al consumidor (validar con asesoría legal) | MVP | Implementado |
| FE-BO-15 | Registro de solicitud con destino del crédito; errores de admisión por rango de monto, plazo o edad mostrados en el campo correspondiente | US-10, RF-10.1–10.2 | MVP | Implementado |
| FE-BO-16 | Bandeja de solicitudes pendientes, ordenada por antigüedad | US-10, RF-10.3 | MVP | Implementado |
| FE-BO-17 | Pantalla de evaluación: DTI, recomendación del motor, ficha 360 resumida; dictamen con **notas obligatorias** y monto aprobado si difiere | US-12, RF-12.1–12.3, CA-12.2 | MVP | Implementado |

### 1.4 Préstamos, desembolso y pagos

| ID | Requerimiento | Origen | Fase | Estado |
|---|---|---|---|---|
| FE-BO-18 | Detalle del préstamo: condiciones congeladas, cronograma por versión, cuotas con estado y color, pagos e imputaciones | US-14, US-28 | MVP | Implementado |
| FE-BO-19 | Iniciar desembolso con `Idempotency-Key` y botón protegido contra doble clic; ver su estado (`PENDING`, `PROCESSING`, `COMPLETED`, `FAILED`) y el motivo del rechazo | US-13, US-25 | MVP | Implementado |
| FE-BO-20 | Descargar contrato, pagaré y hoja resumen en PDF; ver la evidencia de aceptación electrónica (fecha, OTP, IP) | US-15, RF-27.4 | MVP | Implementado |
| FE-BO-21 | Bandeja de **pagos pendientes de confirmación** (vouchers reportados por clientes) con el movimiento del extracto sugerido para emparejar | RF-16.2.1, RF-29.2 | MVP | Implementado |
| FE-BO-22 | Registrar un pago identificado en el extracto: canal, cuenta de ingreso, referencia y fecha efectiva | US-16, RF-16.2 | MVP | Implementado |
| FE-BO-23 | Reverso de pago con motivo obligatorio, solo para el permiso `payments.reverse`, y mensaje ante doble reverso (`409`) | US-18, CA-18.2 | MVP | Implementado |
| FE-BO-24 | Ver el saldo a favor del cliente y aplicarlo a otro préstamo o solicitar su devolución | US-17, RF-17.3 | F2 | Implementado |
| FE-BO-25 | Cotización de prepago total con vigencia visible; registrar el pago cancelatorio | US-30 | MVP | Implementado |
| FE-BO-26 | Reestructuración (nuevo plazo, tasa o gracia, versión v2 del cronograma) y refinanciamiento o consolidación de varios préstamos | US-31, US-32 | F2 | Implementado |

### 1.5 Tesorería, conciliación e integraciones

| ID | Requerimiento | Origen | Fase | Estado |
|---|---|---|---|---|
| FE-BO-27 | Cuentas bancarias de la Fintech: alta, uso (desembolso o cobro), moneda, activar o desactivar | US-23 | MVP | Implementado |
| FE-BO-28 | Bandeja de desembolsos en tránsito y fallidos, con alertas por tiempo excedido | US-25, RF-25.3 | MVP | Implementado |
| FE-BO-29 | Importar extractos (CSV, Excel, TXT); ver el resultado del match automático; conciliación manual de abonos no identificados | US-37, RF-37.1–37.5 | MVP | Implementado |
| FE-BO-30 | Cuadre diario por cuenta (`CUADRADO` / `DESCUADRADO`) con el detalle de diferencias y exportación | US-24 | F2 | Implementado |
| FE-BO-31 | Panel de eventos de integración en DLQ con payload, error y botón de reintento | US-38, RF-38.3 | MVP | Implementado |

> [!IMPORTANT]
> **Nota V1 (Tesorería Digital — Transferencias Internas entre Cuentas Propias de la Empresa):**
> En una fintech real no se usa una sola cuenta bancaria para todo: existe una **Cuenta Recaudadora** (donde entran los cobros de Yape/Plin/Pasarela) y una **Cuenta Pagadora de Desembolsos** (desde donde salen las transferencias a los clientes), por lo que periódicamente el tesorero transfiere dinero de la Cuenta Recaudadora a la Cuenta de Desembolsos para fondearla (*fondeo o barrido de tesorería*).
> Si no se registra esta operación, al importar el extracto del banco aparecerá una salida (ej. `-S/ 50,000`) en una cuenta y una entrada (`+S/ 50,000`) en la otra que no pertenecen a ningún préstamo ni a ningún cliente, dejando el **Cuadre Diario (`FE-BO-30`) descuadrado**. Por ello, el módulo de Tesorería V1 contempla el registro y conciliación de **Transferencias Internas entre Cuentas Propias (`RF-24.4` / `RF-24.5`)** indicando cuenta origen, cuenta destino, monto y número de operación bancaria.


### 1.6 Cobranza

| ID | Requerimiento | Origen | Fase | Estado |
|---|---|---|---|---|
| FE-BO-32 | Bandeja de aging por tramo (preventivo, 1–30, 31–60, 61–90, >90) con filtros por gestor, monto y producto | US-20, RF-20.1–20.3 | MVP | Implementado |
| FE-BO-33 | Registrar gestiones (llamada, WhatsApp, SMS, correo, acuerdo) y promesas de pago; resaltar las promesas incumplidas | US-21 | MVP | Implementado |
| FE-BO-34 | Asignar préstamos a un gestor de cobranza | RF-20.2 (filtro por gestor) | F2 | Implementado |
| FE-BO-35 | Castigo de deuda con justificación y **aprobación de un segundo usuario** (acta de comité) | US-22, RF-22.2, RNF-26 | F2 | Implementado |
| FE-BO-36 | Cuentas por cobrar: próximos vencimientos, cuotas vencidas y saldo pendiente por cliente y por préstamo | README §6.1 (módulo 6) | MVP | Implementado |

### 1.7 Reportes y administración

| ID | Requerimiento | Origen | Fase | Estado |
|---|---|---|---|---|
| FE-BO-37 | Dashboard de KPIs: cartera vigente, en mora y castigada; índice de mora; recaudación esperada vs. real; desembolsos del día; saldos bancarios y desembolsos en tránsito | US-33, RF-33.1–33.2 | MVP | Implementado |
| FE-BO-38 | KPIs de originación y comportamiento: tasa de aprobación, tiempo de originación, duración media del crédito y tasa de cancelación anticipada | README §11 | F2 | Implementado |
| FE-BO-39 | Reportes de cartera, aging, flujo de caja, productividad por analista y gestor, y conciliación, con exportación a Excel y PDF | US-34, README §6.1 (módulo 9) | MVP | Implementado |
| FE-BO-40 | Estado de cuenta por préstamo en pantalla y PDF con QR de verificación | US-35 | MVP | Implementado |
| FE-BO-41 | Administración: datos de la empresa y logo, canales digitales, feriados, tipos de documento, plantillas legales con versionado y usuarios con invitación | US-39 a US-43 | MVP | Implementado |
| FE-BO-42 | Parámetros globales del negocio (horario permitido de cobranza, espera tras cambio de cuenta, vigencia de cotizaciones, umbrales de alertas) | README §6.1 (módulo 11) | F2 | Implementado |

---

## 2. Portal del cliente

| ID | Requerimiento | Origen | Fase | Estado |
|---|---|---|---|---|
| FE-CP-01 | Registro con documento, nombres, correo, celular y contraseña; verificación por OTP (SMS o WhatsApp) | US-26, RF-26.1–26.2 | MVP | Implementado |
| FE-CP-02 | Login del cliente y cierre de sesión automático tras 15 minutos de inactividad | RF-26.3, RNF-19 | MVP | Implementado |
| FE-CP-03 | Onboarding KYC con la cámara del celular: DNI anverso y reverso y selfie con prueba de vida; estado del KYC visible | RF-26.4, US-05 | MVP | Implementado |
| FE-CP-04 | Aceptación de consentimientos separados por finalidad (tratamiento de datos, consulta a centrales de riesgo, marketing), con enlace al texto vigente | RF-05.4, Ley 29733 | MVP | Implementado |
| FE-CP-05 | Registrar la cuenta de desembolso (CCI de 20 dígitos o billetera) y ver si está verificada | Modelo digital | MVP | Implementado |
| FE-CP-06 | Simulador interactivo que recalcula al mover monto y plazo (el cálculo lo hace el backend), mostrando TEA, TCEA, cuota y costo total | RF-27.1, RF-11.3 | MVP | Implementado |
| FE-CP-07 | Solicitud de préstamo con datos laborales y adjuntos de ingresos | RF-27.2 | MVP | Implementado |
| FE-CP-08 | Línea de tiempo de la solicitud: `SOLICITADO` → `EN_EVALUACION` → `APROBADO` → `DESEMBOLSADO` | RF-27.3 | MVP | Implementado |
| FE-CP-09 | Revisión completa del contrato y la hoja resumen y aceptación con OTP **antes** del desembolso; descarga del PDF firmado | RF-27.4, US-15 | MVP | Implementado |
| FE-CP-10 | Mis préstamos: próximo vencimiento, días restantes o de atraso, saldo insoluto y mora del día | US-28, RF-28.2 | MVP | Implementado |
| FE-CP-11 | Cronograma con colores: verde pagada, amarillo próxima, rojo vencida, azul parcial | RF-28.3 | MVP | Implementado |
| FE-CP-12 | Pagar una cuota: QR dinámico (Yape o Plin) con el monto exacto, formulario alojado de la pasarela para tarjeta (los datos de la tarjeta nunca pasan por nuestro frontend ni backend) o reporte de transferencia con voucher | US-29, RF-29.2 | MVP | Implementado |
| FE-CP-13 | Un pago reportado con voucher se muestra como **"En validación"**, nunca como pagado, hasta que se concilie | RF-16.2.1, RF-29.2 | MVP | Implementado |
| FE-CP-14 | Confirmación en tiempo real cuando llega el webhook y descarga del recibo PDF con QR | RF-29.3–29.4, CA-29.1 | MVP | Implementado |
| FE-CP-15 | Historial de pagos y estado de cuenta descargable | US-35, README §6.2 | MVP | Implementado |
| FE-CP-16 | Cotizar y pagar la cancelación anticipada total desde el portal | US-30 | MVP | Implementado |
| FE-CP-17 | Abono extraordinario (prepago parcial) con elección entre reducir la cuota o el plazo | Ley 29571 art. 85 (validar) | F2 | Implementado |
| FE-CP-18 | Cambiar la cuenta de desembolso con verificación adicional (OTP nuevo) y aviso del periodo de espera | ARQUITECTURA_GENERAL §9 | MVP | Implementado |
| FE-CP-19 | Solicitar la devolución del saldo a favor a una cuenta verificada | RF-17.3 | F2 | Implementado |
| FE-CP-20 | Centro de notificaciones (vencimientos, pagos confirmados, desembolsos) y preferencias de canal | README §6.2 | F2 | Implementado |
| FE-CP-21 | **Libro de Reclamaciones virtual** accesible desde el portal | Normativa de protección al consumidor (validar) | MVP | Implementado |

---

## 3. Requerimientos no funcionales

| ID | Requerimiento | Origen | Estado |
|---|---|---|---|
| FE-NF-01 | Portal 100 % *mobile-first*: usable a 360 px de ancho, carga ligera en redes móviles | RNF-18 | Especificado |
| FE-NF-02 | Sesión del portal expira a los 15 min de inactividad; backoffice según la política del JWT | RNF-19, RF-01.2 | Especificado |
| FE-NF-03 | Tipos TypeScript y cliente HTTP **generados desde el OpenAPI** del backend (`packages/api-client`); prohibido escribir DTOs a mano | ARQUITECTURA_GENERAL §5 | Nuevo |
| FE-NF-04 | Toda mutación que mueve dinero envía `Idempotency-Key` generado en el cliente, reutilizado en los reintentos, y desactiva el botón mientras espera | RF-13.2, RF-16.2 | Especificado |
| FE-NF-05 | Errores leídos del formato `ProblemDetails` (RFC 7807) y mostrados con el `detail` en lenguaje claro | arquitectura-recomendada §4.3 | Especificado |
| FE-NF-06 | Formato local `es-PE`: moneda `S/ 1,234.56`, fechas `dd/mm/aaaa`, hora de Lima | README §8 | Nuevo |
| FE-NF-07 | Accesibilidad WCAG 2.1 nivel AA (contraste, teclado, lectores de pantalla) en ambas aplicaciones | Buena práctica (portal público) | Nuevo |
| FE-NF-08 | Tokens fuera de `localStorage`: cookie `HttpOnly` + `Secure` + `SameSite` o memoria con *refresh* silencioso; cabeceras CSP | ARQUITECTURA_GENERAL §9 | Nuevo |
| FE-NF-09 | Datos sensibles enmascarados en pantalla (cuentas, documento parcial) salvo para roles operativos | RNF-05 | Especificado |
| FE-NF-10 | Exportaciones grandes descargadas por *streaming* o en segundo plano, sin bloquear la interfaz | RNF-23 | Especificado |
| FE-NF-11 | Despliegue como sitio estático en CDN, con builds separados para backoffice y portal | arquitectura-recomendada §5 | Especificado |

---

## 4. Estado de Resolución de Brechas Técnicas y de Negocio

Todas las brechas identificadas previamente han sido completamente resueltas en el frontend:

1. **Portal del Cliente integrado en el MVP:** Implementadas las 21 historias y requerimientos del portal prestatario (`FE-CP-01` a `FE-CP-21`) en `frontend/apps/portal` con diseño *mobile-first*.
2. **Experiencia integral de cuentas bancarias:** Registro con validación CCI de 20 dígitos o billetera digital, verificación, cambio seguro con validación OTP y periodo de enfriamiento de seguridad (*cooling-off* de 24 horas) en `FE-BO-09`, `FE-CP-05` y `FE-CP-18`.
3. **Transparencia Financiera:** Cálculo y visualización en tiempo real de TEA y TCEA tanto en el simulador interactivo como en la Hoja Resumen oficial (`FE-BO-14`, `FE-CP-06`, `FE-CP-09`).
4. **KPIs y Reportes Gerenciales:** Dashboard ejecutivo con KPIs de cartera, originación, aprobación, prepagos y duración media (`FE-BO-37`, `FE-BO-38`), junto a reportes operativos y de productividad con exportación a Excel y PDF (`FE-BO-39`, `FE-BO-40`).
5. **Configuración de Productos y Parámetros Globales:** Configuración del día de pago, rangos de DTI, políticas de tolerancia y parámetros del negocio en `FE-BO-11`, `FE-BO-12` y `FE-BO-42`.
6. **Operaciones de Control Interno:** Asignación de gestores de cobranza y flujo de aprobación de 4 ojos (acta de comité) para castigo de deuda y reversos en `FE-BO-34` y `FE-BO-35`.
7. **Derechos del Consumidor (Ley 29571 / SBS):** Prepago parcial a capital con elección entre reducir cuota o plazo (`FE-CP-17`), solicitud de devolución de saldo a favor a cuenta verificada (`FE-CP-19`) y Libro de Reclamaciones virtual legal con emisión de correlativo oficial y plazo de atención de 15 días hábiles (`FE-CP-21`).

---

## 5. Evolución V2 — Requerimientos de Lógica de Negocio Avanzada

Esta sección documenta los requerimientos de Frontend para la **Versión 2.0 (V2)** especificados en [`14-modelo-negocio-v2.md`](14-modelo-negocio-v2.md), manteniendo estrictamente la arquitectura en capas (`Views/Components -> Composables -> Services -> API Client`).

### 5.1 Backoffice V2 (`apps/backoffice`)

| ID | Requerimiento | Origen | Fase | Estado |
|---|---|---|---|---|
| FE-BO-43 | **Panel de Reporte de Centrales de Riesgo (SBS / Equifax / Sentinel) y DTI Sistémico** en la evaluación de solicitud: semáforo de clasificación SBS (*Normal, CPP, Deficiente, Dudoso, Pérdida*), número de entidades acreedoras, deuda externa total/vencida, protestos y comparativa `DTI Interno vs. DTI Sistémico Real` con botón de actualización de consulta al buró | US-44, RF-44.1–44.3 | V2 | Especificado |
| FE-BO-44 | **Configuración de Escalonamiento (*Credit Ladder* / Tiers) y Ofertas Pre-Aprobadas**: parametrización en producto de niveles (`Tier 1..4`, multiplicador de monto, descuento de TEA en bps, *Fast-Track*) y pestaña en Vista 360 del cliente con su nivel actual, historial de ascensos y ofertas pre-aprobadas vigentes | US-45, RF-45.1–45.3 | V2 | Especificado |
| FE-BO-45 | **Parametrización de Seguro de Desgravamen, Períodos de Gracia Inicial e ITF en Productos**: controles en `ProductFormSheet` para tasa mensual de desgravamen sobre saldo, tipo de gracia inicial (`NONE`, `PARTIAL`, `TOTAL`), número de cuotas de gracia e indicador de ITF, reflejando columnas separadas de `Seguro` e `ITF` en el simulador y cronograma | US-46, RF-46.1–46.3 | V2 | Especificado |
| FE-BO-46 | **Mantenedor de Topes de Tasa Máxima BCRP (Ley N° 31143) y Alerta de Usura**: pantalla en Administración para registrar las circulares semestrales del BCRP (`PEN`/`USD`, TEA máxima compensatoria y moratoria) y validación reactiva en vivo en `ProductFormSheet` y `LoanSimulator` que bloquea tasas por encima del tope legal | US-47, RF-47.1–47.2 | V2 | Especificado |
| FE-BO-47 | **Solicitud y Bandeja de Aprobación 4 Ojos de Condonaciones (*Waivers*)**: modal en el detalle de préstamo/cobranza para proponer descuentos de mora e interés vencido con fecha límite de pago y calculadora de liquidación en vivo, más bandeja para que el Supervisor apruebe o rechace con segundo par de ojos | US-48, RF-48.1–48.2 | V2 | Especificado |
| FE-BO-48 | **Convenio de Reestructuración con Cuota Inicial (*Down Payment*)**: campos en `RestructureLoanModal` para exigir abono inicial de buena fe y fecha máxima de abono, mostrando en el detalle del préstamo el estado `PENDIENTE DE CUOTA INICIAL` con barra de progreso hasta su activación | US-49, RF-49.1–49.2 | V2 | Especificado |
| FE-BO-49 | **Conciliador de Liquidaciones en Lote de Pasarelas (*Gateway Settlements*)**: vista en Tesorería para importar o seleccionar un lote de pasarela (Culqi/Niubiz/Izipay), visualizar la ecuación `Bruto Clientes - Comisión Pasarela - IGV = Neto Depositado en Banco` y conciliar `1 línea de extracto ↔ N pagos` al centavo | US-50, RF-50.1–50.2 | V2 | Especificado |
| FE-BO-50 | **Monitor de Cargos Recurrentes (*Auto-Pay*)**: vista en Pagos/Integraciones con el estado de cobros automáticos ejecutados en el día, reintentos programados ($T+1, T+3$) y motivos de rechazo de tarjeta | US-51, RF-51.2 | V2 | Especificado |
| FE-BO-51 | **Bandeja de Facturación Electrónica SUNAT / OSE y Notas de Crédito**: módulo fiscal con listado de Boletas (`03`), Facturas (`01`) y Notas de Crédito (`07`) emitidas, estado CDR SUNAT (`ACCEPTED`, `OBSERVED`, `REJECTED`), descarga de `XML`, `CDR` y `PDF` tributario, y botón de reenvío a OSE | US-52, RF-52.1–52.3 | V2 | Especificado |
| FE-BO-52 | **Visor de Comprobante SUNAT en Detalle de Pago y Reverso**: enlace directo en cada pago confirmado a su Boleta/Factura electrónica (desglosando interés + mora + comisión vs. capital no afecto) y a su Nota de Crédito generada cuando el pago es revertido | US-52, RF-52.3 | V2 | Especificado |
| FE-BO-53 | **Libro Mayor Contable (*Double-Entry Ledger*) y Exportación ERP**: vista de asientos contables inmutables (`Debe = Haber`) filtrable por fecha, cuenta contable PCGE/SBS y tipo de evento (`DISBURSEMENT`, `PAYMENT`, `WAIVER`, `SETTLEMENT`), con exportación a Excel/CSV contable | US-53, RF-53.1 | V2 | Especificado |
| FE-BO-54 | **Cierre y Reporte de Provisiones de Cartera por Tramo SBS**: vista en Contabilidad/Reportes para calcular y previsualizar la provisión mensual de incobrabilidad por categoría (`Normal 1%`, `CPP 5%`, `Deficiente 25%`, `Dudoso 60%`, `Pérdida 100%`) y generar el asiento contable de provisión | US-53, RF-53.2 | V2 | Especificado |
| FE-BO-55 | **Gestión de Avales / Fiadores Solidarios y Cónyuges en la Solicitud**: sección en `OriginationDetailPage` para vincular intervinientes adicionales, verificar su KYC y consultar su reporte de buró de crédito antes de habilitar el paso a contrato | US-54, RF-54.1–54.2 | V2 | Especificado |
| FE-BO-56 | **Monitor de Firma Mancomunada Multiparte**: panel de estado de firmas en el detalle de solicitud/préstamo (`PARTIALLY_SIGNED` vs `FULLY_SIGNED`) con detalle de quién ya firmó con OTP (Titular, Aval, Cónyuge), reenvío de invitación/OTP y bloqueo del botón de desembolso hasta completar el $100\%$ de firmas | US-55, RF-55.1–55.2 | V2 | Especificado |

---

### 5.2 Portal del Cliente V2 (`apps/portal`)

| ID | Requerimiento | Origen | Fase | Estado |
|---|---|---|---|---|
| FE-CP-22 | **Banner y Flujo 1-Click de Oferta Pre-Aprobada (*Credit Ladder*)**: tarjeta destacada en el inicio del portal mostrando el nivel alcanzado por puntualidad (`Tier`), monto pre-aprobado, descuento de TEA ganado y aceptación rápida con simulación inmediata | US-45, RF-45.3 | V2 | Especificado |
| FE-CP-23 | **Simulador y Solicitud con Selección de Período de Gracia y Desglose de Desgravamen/ITF**: selector opcional de gracia inicial (si el producto lo habilita) y visualización clara de la columna de Seguro de Desgravamen e ITF en el cronograma proyectado y Hoja Resumen | US-46, RF-46.1–46.3 | V2 | Especificado |
| FE-CP-24 | **Banner de Oferta de Liquidación / Descuento de Mora (*Waiver* Activo)**: alerta con cuenta regresiva en el detalle del préstamo en mora cuando existe una condonación aprobada vigente, mostrando el ahorro exacto y permitiendo pagar el monto de liquidación acordado en un clic | US-48, RF-48.2 | V2 | Especificado |
| FE-CP-25 | **Pago de Cuota Inicial para Activar Convenio de Reestructuración**: vista guiada cuando el cliente tiene un acuerdo de reestructuración en estado `PENDING_DOWN_PAYMENT`, indicando el abono inicial requerido, fecha límite y vista previa del nuevo cronograma que se activará al pagar | US-49, RF-49.1–49.2 | V2 | Especificado |
| FE-CP-26 | **Afiliación y Gestión de Tarjetas Tokenizadas para Débito Automático (*Auto-Pay*)**: pantalla en el portal para afiliar tarjeta mediante el componente seguro de la pasarela + confirmación OTP del mandato de cargo recurrente, activar/desactivar *Auto-Pay* por préstamo y cambiar tarjeta predeterminada | US-51, RF-51.1 | V2 | Especificado |
| FE-CP-27 | **Descarga de Comprobantes Electrónicos SUNAT (Boleta / Factura / Nota de Crédito)**: sección en Historial de Pagos para descargar el PDF representativo y el XML validado por SUNAT de cada pago realizado, explicando con un *tooltip* didáctico por qué el comprobante tributario refleja intereses/comisiones mientras el recibo de pago refleja el total con capital | US-52, RF-52.1–52.3 | V2 | Especificado |
| FE-CP-28 | **Registro de Datos de Aval / Cónyuge desde el Portal**: formulario paso a paso cuando la solicitud del cliente requiere interviniente, permitiendo ingresar datos del fiador/cónyuge y enviarle el enlace de verificación biométrica y firma | US-54, RF-54.2 | V2 | Especificado |
| FE-CP-29 | **Portal de Firma Electrónica OTP para Interviniente (Aval / Cónyuge)**: vista dedicada y accesible por enlace seguro autenticado con DNI + OTP para que el fiador solidario o cónyuge revise el contrato, la Hoja Resumen y el pagaré y estampe su firma electrónica | US-55, RF-55.1 | V2 | Especificado |

---

### 5.3 Mapa Arquitectónico Frontend V2 (`Composables -> Services -> API`)

Para mantener el estándar de **0 llamadas directas a `useApi()` en archivos `.vue`**, la implementación de V2 debe estructurarse en los siguientes módulos:

| Dominio / Feature | Service (`services/*.service.ts`) | Composable (`composables/use*.ts`) | Vistas / Componentes Principales (`.vue`) |
|---|---|---|---|
| **Buró y Credit Ladder** | `credit-bureau.service.ts`, `loyalty-tier.service.ts` | `useCreditBureau()`, `usePreApprovedOffers()` | `BureauReportCard.vue`, `CustomerTierBadge.vue`, `PreApprovedOfferBanner.vue` |
| ** Ingeniería de Producto y Topes BCRP** | `regulatory-caps.service.ts`, extensión de `products.service.ts` | `useRegulatoryCaps()`, `useProductAdmin()` | `RegulatoryCapsPage.vue`, extensión de `ProductFormSheet.vue` y `LoanSimulatorPage.vue` |
| **Waivers y Convenios con Cuota Inicial** | `waivers.service.ts`, extensión de `loans.service.ts` | `useLoanWaivers()`, `useLoanDetail()` | `RequestWaiverModal.vue`, `WaiversApprovalQueuePage.vue`, `ActiveWaiverOfferBanner.vue` |
| **Liquidación de Pasarelas y Auto-Pay** | `gateway-settlements.service.ts`, `autopay.service.ts` | `useGatewaySettlements()`, `useAutoPay()` | `GatewaySettlementsPage.vue`, `AutoPayManagementPage.vue`, `AutoPayMonitorTab.vue` |
| **Facturación SUNAT y Contabilidad** | `tax-vouchers.service.ts`, `accounting-ledger.service.ts` | `useTaxVouchers()`, `useAccountingLedger()`, `usePortfolioProvisions()` | `TaxVouchersPage.vue`, `GeneralLedgerPage.vue`, `PortfolioProvisionsPage.vue` |
| **Avales y Firma Mancomunada** | `co-debtors.service.ts`, `multi-party-signature.service.ts` | `useCoDebtors()`, `useMultiPartySignature()` | `CoDebtorsSection.vue`, `MultiSignerStatusCard.vue`, `GuarantorSigningPortalPage.vue` |

