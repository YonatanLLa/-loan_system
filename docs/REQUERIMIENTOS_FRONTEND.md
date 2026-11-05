# Requerimientos de Frontend — Sistema de Préstamos (Fintech 100% Digital)

Requerimientos de las dos aplicaciones web del sistema, derivados de las historias de usuario de `docs/01` a `docs/13`, del [`README.md`](../README.md) y de [`ARQUITECTURA_GENERAL.md`](../ARQUITECTURA_GENERAL.md).

- **Backoffice:** panel interno para analistas, operadores, tesorería, cobranza, auditoría y administración.
- **Portal del cliente:** autoservicio *mobile-first* para el prestatario. En un modelo 100% digital es el único canal del cliente, así que **forma parte del MVP**.

Documentos hermanos: [Backend](REQUERIMIENTOS_BACKEND.md) · [Base de datos](REQUERIMIENTOS_BDD.md) · Arquitectura y stack (Vue 3 + PrimeVue): [`ARQUITECTURA_FRONTEND.md`](../ARQUITECTURA_FRONTEND.md)

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
| FE-BO-01 | Login con correo y contraseña; mensajes genéricos ante credenciales inválidas; pantalla de cuenta bloqueada (`423`) | US-01, RF-01.1–01.3 | MVP | Especificado |
| FE-BO-02 | Segundo factor (TOTP) obligatorio para los roles que mueven dinero (Admin, Operador) | ARQUITECTURA_GENERAL §9 | MVP | Nuevo |
| FE-BO-03 | Menú, rutas y botones según los permisos de `GET /api/me`: lo que el rol no puede hacer no se muestra (el backend vuelve a validar) | US-02, RF-01.5, RF-02.2 | MVP | Especificado |
| FE-BO-04 | Cierre de sesión que revoca el *refresh token* y expulsión inmediata si el usuario es suspendido (`401`) | RF-01.4, RF-43.3 | MVP | Especificado |
| FE-BO-05 | Bitácora de auditoría con filtros por entidad, fechas, usuario y acción, y vista "antes / después" del JSON | US-03, RF-03.4 | MVP | Especificado |

### 1.2 Clientes y KYC

| ID | Requerimiento | Origen | Fase | Estado |
|---|---|---|---|---|
| FE-BO-06 | Lista y búsqueda de clientes por documento o nombre; alta y edición con validación de formato según el tipo de documento (regex del catálogo) | US-04, RF-04.1, RF-42.1 | MVP | Especificado |
| FE-BO-07 | Mensaje claro ante documento duplicado (`409`) | CA-04.2 | MVP | Especificado |
| FE-BO-08 | Expediente KYC: visor de DNI, selfie y sustentos; resultado de las verificaciones automáticas (identidad, biometría, listas PEP/AML); aprobar o rechazar con notas | US-05, RF-05.1–05.2 | MVP | Parcial (las verificaciones automáticas no están especificadas) |
| FE-BO-09 | Cuentas bancarias del cliente: ver CCI o billetera enmascarados, estado de verificación, marcar como primaria y verificar titularidad | Modelo digital (`customer_bank_accounts`) | MVP | Nuevo |
| FE-BO-10 | Ficha 360: préstamos activos e históricos, deuda consolidada, atraso máximo, solicitudes pasadas y alerta destacada de mora | US-06, RF-06.1 | MVP | Especificado |

### 1.3 Productos, simulación y originación

| ID | Requerimiento | Origen | Fase | Estado |
|---|---|---|---|---|
| FE-BO-11 | Alta y edición de productos: moneda, montos, plazos, frecuencias, método de amortización, TEA, comisiones (fija o %, deductiva o financiada), cargos periódicos, gracia, mora (tasa y base), anatocismo, penalidad de prepago, elegibilidad y ajuste por feriados; validación de rangos antes de enviar | US-07, US-08, US-09 | MVP | Especificado |
| FE-BO-12 | Configurar el **día de pago** del producto y el **DTI máximo** permitido (hoy fijo en 35 % / 50 %) | README §3.1 y §3.10 | MVP | Nuevo |
| FE-BO-13 | Simulador: monto, plazo y frecuencia → resumen (TEA, TEM, total de intereses, comisiones, costo total) y cronograma proyectado | US-11, RF-11.2–11.3 | MVP | Especificado |
| FE-BO-14 | Mostrar la **TCEA** (tasa de costo efectivo anual) en el simulador y en la hoja resumen | Transparencia al consumidor (validar con asesoría legal) | MVP | Nuevo |
| FE-BO-15 | Registro de solicitud con destino del crédito; errores de admisión por rango de monto, plazo o edad mostrados en el campo correspondiente | US-10, RF-10.1–10.2 | MVP | Especificado |
| FE-BO-16 | Bandeja de solicitudes pendientes, ordenada por antigüedad | US-10, RF-10.3 | MVP | Especificado |
| FE-BO-17 | Pantalla de evaluación: DTI, recomendación del motor, ficha 360 resumida; dictamen con **notas obligatorias** y monto aprobado si difiere | US-12, RF-12.1–12.3, CA-12.2 | MVP | Especificado |

### 1.4 Préstamos, desembolso y pagos

| ID | Requerimiento | Origen | Fase | Estado |
|---|---|---|---|---|
| FE-BO-18 | Detalle del préstamo: condiciones congeladas, cronograma por versión, cuotas con estado y color, pagos e imputaciones | US-14, US-28 | MVP | Especificado |
| FE-BO-19 | Iniciar desembolso con `Idempotency-Key` y botón protegido contra doble clic; ver su estado (`PENDING`, `PROCESSING`, `COMPLETED`, `FAILED`) y el motivo del rechazo | US-13, US-25 | MVP | Especificado |
| FE-BO-20 | Descargar contrato, pagaré y hoja resumen en PDF; ver la evidencia de aceptación electrónica (fecha, OTP, IP) | US-15, RF-27.4 | MVP | Parcial (la evidencia de firma no está modelada) |
| FE-BO-21 | Bandeja de **pagos pendientes de confirmación** (vouchers reportados por clientes) con el movimiento del extracto sugerido para emparejar | RF-16.2.1, RF-29.2 | MVP | Especificado |
| FE-BO-22 | Registrar un pago identificado en el extracto: canal, cuenta de ingreso, referencia y fecha efectiva | US-16, RF-16.2 | MVP | Especificado |
| FE-BO-23 | Reverso de pago con motivo obligatorio, solo para el permiso `payments.reverse`, y mensaje ante doble reverso (`409`) | US-18, CA-18.2 | MVP | Especificado |
| FE-BO-24 | Ver el saldo a favor del cliente y aplicarlo a otro préstamo o solicitar su devolución | US-17, RF-17.3 | F2 | Parcial (la devolución no está modelada) |
| FE-BO-25 | Cotización de prepago total con vigencia visible; registrar el pago cancelatorio | US-30 | MVP | Especificado |
| FE-BO-26 | Reestructuración (nuevo plazo, tasa o gracia, versión v2 del cronograma) y refinanciamiento o consolidación de varios préstamos | US-31, US-32 | F2 | Especificado |

### 1.5 Tesorería, conciliación e integraciones

| ID | Requerimiento | Origen | Fase | Estado |
|---|---|---|---|---|
| FE-BO-27 | Cuentas bancarias de la Fintech: alta, uso (desembolso o cobro), moneda, activar o desactivar | US-23 | MVP | Especificado |
| FE-BO-28 | Bandeja de desembolsos en tránsito y fallidos, con alertas por tiempo excedido | US-25, RF-25.3 | MVP | Especificado |
| FE-BO-29 | Importar extractos (CSV, Excel, TXT); ver el resultado del match automático; conciliación manual de abonos no identificados | US-37, RF-37.1–37.5 | MVP | Especificado |
| FE-BO-30 | Cuadre diario por cuenta (`CUADRADO` / `DESCUADRADO`) con el detalle de diferencias y exportación | US-24 | F2 | Especificado |
| FE-BO-31 | Panel de eventos de integración en DLQ con payload, error y botón de reintento | US-38, RF-38.3 | MVP | Especificado |

### 1.6 Cobranza

| ID | Requerimiento | Origen | Fase | Estado |
|---|---|---|---|---|
| FE-BO-32 | Bandeja de aging por tramo (preventivo, 1–30, 31–60, 61–90, >90) con filtros por gestor, monto y producto | US-20, RF-20.1–20.3 | MVP | Especificado |
| FE-BO-33 | Registrar gestiones (llamada, WhatsApp, SMS, correo, acuerdo) y promesas de pago; resaltar las promesas incumplidas | US-21 | MVP | Especificado |
| FE-BO-34 | Asignar préstamos a un gestor de cobranza | RF-20.2 (filtro por gestor) | F2 | Parcial (no hay asignación modelada) |
| FE-BO-35 | Castigo de deuda con justificación y **aprobación de un segundo usuario** (acta de comité) | US-22, RF-22.2, RNF-26 | F2 | Parcial (la aprobación de 4 ojos no está modelada) |
| FE-BO-36 | Cuentas por cobrar: próximos vencimientos, cuotas vencidas y saldo pendiente por cliente y por préstamo | README §6.1 (módulo 6) | MVP | Parcial (repartido entre US-06 y US-20) |

### 1.7 Reportes y administración

| ID | Requerimiento | Origen | Fase | Estado |
|---|---|---|---|---|
| FE-BO-37 | Dashboard de KPIs: cartera vigente, en mora y castigada; índice de mora; recaudación esperada vs. real; desembolsos del día; saldos bancarios y desembolsos en tránsito | US-33, RF-33.1–33.2 | MVP | Especificado |
| FE-BO-38 | KPIs de originación y comportamiento: tasa de aprobación, tiempo de originación, duración media del crédito y tasa de cancelación anticipada | README §11 | F2 | Nuevo |
| FE-BO-39 | Reportes de cartera, aging, flujo de caja, productividad por analista y gestor, y conciliación, con exportación a Excel y PDF | US-34, README §6.1 (módulo 9) | MVP | Parcial (la productividad no está especificada) |
| FE-BO-40 | Estado de cuenta por préstamo en pantalla y PDF con QR de verificación | US-35 | MVP | Especificado |
| FE-BO-41 | Administración: datos de la empresa y logo, canales digitales, feriados, tipos de documento, plantillas legales con versionado y usuarios con invitación | US-39 a US-43 | MVP | Especificado |
| FE-BO-42 | Parámetros globales del negocio (horario permitido de cobranza, espera tras cambio de cuenta, vigencia de cotizaciones, umbrales de alertas) | README §6.1 (módulo 11) | F2 | Nuevo |

---

## 2. Portal del cliente

| ID | Requerimiento | Origen | Fase | Estado |
|---|---|---|---|---|
| FE-CP-01 | Registro con documento, nombres, correo, celular y contraseña; verificación por OTP (SMS o WhatsApp) | US-26, RF-26.1–26.2 | MVP | Especificado |
| FE-CP-02 | Login del cliente y cierre de sesión automático tras 15 minutos de inactividad | RF-26.3, RNF-19 | MVP | Especificado |
| FE-CP-03 | Onboarding KYC con la cámara del celular: DNI anverso y reverso y selfie con prueba de vida; estado del KYC visible | RF-26.4, US-05 | MVP | Especificado |
| FE-CP-04 | Aceptación de consentimientos separados por finalidad (tratamiento de datos, consulta a centrales de riesgo, marketing), con enlace al texto vigente | RF-05.4, Ley 29733 | MVP | Parcial (hoy hay un solo consentimiento) |
| FE-CP-05 | Registrar la cuenta de desembolso (CCI de 20 dígitos o billetera) y ver si está verificada | Modelo digital | MVP | Nuevo |
| FE-CP-06 | Simulador interactivo que recalcula al mover monto y plazo (el cálculo lo hace el backend), mostrando TEA, TCEA, cuota y costo total | RF-27.1, RF-11.3 | MVP | Parcial (la TCEA no está especificada) |
| FE-CP-07 | Solicitud de préstamo con datos laborales y adjuntos de ingresos | RF-27.2 | MVP | Especificado |
| FE-CP-08 | Línea de tiempo de la solicitud: `SOLICITADO` → `EN_EVALUACION` → `APROBADO` → `DESEMBOLSADO` | RF-27.3 | MVP | Especificado (el estado `DESEMBOLSADO` falta en la BD) |
| FE-CP-09 | Revisión completa del contrato y la hoja resumen y aceptación con OTP **antes** del desembolso; descarga del PDF firmado | RF-27.4, US-15 | MVP | Especificado |
| FE-CP-10 | Mis préstamos: próximo vencimiento, días restantes o de atraso, saldo insoluto y mora del día | US-28, RF-28.2 | MVP | Especificado |
| FE-CP-11 | Cronograma con colores: verde pagada, amarillo próxima, rojo vencida, azul parcial | RF-28.3 | MVP | Especificado |
| FE-CP-12 | Pagar una cuota: QR dinámico (Yape o Plin) con el monto exacto, formulario alojado de la pasarela para tarjeta (los datos de la tarjeta nunca pasan por nuestro frontend ni backend) o reporte de transferencia con voucher | US-29, RF-29.2 | MVP | Especificado |
| FE-CP-13 | Un pago reportado con voucher se muestra como **"En validación"**, nunca como pagado, hasta que se concilie | RF-16.2.1, RF-29.2 | MVP | Especificado |
| FE-CP-14 | Confirmación en tiempo real cuando llega el webhook y descarga del recibo PDF con QR | RF-29.3–29.4, CA-29.1 | MVP | Especificado |
| FE-CP-15 | Historial de pagos y estado de cuenta descargable | US-35, README §6.2 | MVP | Especificado |
| FE-CP-16 | Cotizar y pagar la cancelación anticipada total desde el portal | US-30 | MVP | Especificado |
| FE-CP-17 | Abono extraordinario (prepago parcial) con elección entre reducir la cuota o el plazo | Ley 29571 art. 85 (validar) | F2 | Nuevo |
| FE-CP-18 | Cambiar la cuenta de desembolso con verificación adicional (OTP nuevo) y aviso del periodo de espera | ARQUITECTURA_GENERAL §9 | MVP | Nuevo |
| FE-CP-19 | Solicitar la devolución del saldo a favor a una cuenta verificada | RF-17.3 | F2 | Nuevo |
| FE-CP-20 | Centro de notificaciones (vencimientos, pagos confirmados, desembolsos) y preferencias de canal | README §6.2 | F2 | Parcial (sin historia de usuario) |
| FE-CP-21 | **Libro de Reclamaciones virtual** accesible desde el portal | Normativa de protección al consumidor (validar) | MVP | Nuevo |

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

## 4. Brechas frente al negocio (frontend)

1. **El portal está en la Fase 6 del roadmap del README**, pero en un modelo 100% digital es el único canal del cliente: sin portal no hay onboarding, firma ni pagos. Debe moverse al MVP.
2. **Falta toda la experiencia de cuentas bancarias del cliente** (registrar, verificar, cambiar con seguridad): FE-BO-09, FE-CP-05 y FE-CP-18.
3. **Transparencia:** la TCEA no aparece en el simulador ni en la hoja resumen (FE-BO-14, FE-CP-06).
4. **KPIs del README sin pantalla:** tasa de aprobación, tiempo de originación, duración media y cancelación anticipada (FE-BO-38), además del reporte de productividad (FE-BO-39).
5. **Configuración incompleta de productos y parámetros:** día de pago, DTI máximo configurable y parámetros globales (FE-BO-12, FE-BO-42).
6. **Operaciones de control sin interfaz:** aprobación de 4 ojos para castigos y reversos, y asignación de gestores (FE-BO-34, FE-BO-35).
7. **Derechos del consumidor:** prepago parcial, devolución del saldo a favor y Libro de Reclamaciones virtual (FE-CP-17, FE-CP-19, FE-CP-21). Validar con asesoría legal.
