# Requerimientos de Backend — Sistema de Préstamos (Fintech 100% Digital)

Requerimientos de la API y de los procesos en segundo plano (.NET 10, monolito modular). Se derivan de las historias de `docs/01` a `docs/13`, del [`README.md`](../README.md) y de [`ARQUITECTURA_GENERAL.md`](../ARQUITECTURA_GENERAL.md).

Documentos hermanos: [Frontend](REQUERIMIENTOS_FRONTEND.md) · [Base de datos](REQUERIMIENTOS_BDD.md)

**Convenciones**

| Campo | Valores |
|---|---|
| **ID** | `BE-<MÓDULO>-NN` y `BE-NF-NN` (no funcional) |
| **Origen** | Historia o requerimiento de `docs/` (US / RF / RNF) o sección del README |
| **Fase** | `MVP`, `F2` (operación estable) o `F3` (integraciones enterprise) |
| **Estado** | **Especificado**: ya está en las historias · **Parcial**: cubierto a medias · **Nuevo**: lo requieren el negocio o el modelo digital y no estaba en las historias |

> Reglas transversales (válidas para todos los módulos): montos en `decimal` con redondeo `MidpointRounding.AwayFromZero` a 2 decimales; prelación mora → interés → comisiones → capital; nada financiero se edita ni se borra (contra-asientos); toda operación que mueve dinero es idempotente y corre en una transacción con bloqueo del préstamo.

---

## 1. Security

| ID | Requerimiento | Origen | Fase | Estado |
|---|---|---|---|---|
| BE-SEC-01 | Autenticación con hash Argon2id/BCrypt, JWT corto (15–60 min) y *refresh token* rotativo y revocable | RF-01.1, RF-01.2 | MVP | Especificado |
| BE-SEC-02 | Bloqueo tras 5 intentos fallidos en 15 min, con IP registrada; respuesta `423` | RF-01.3, CA-01.3 | MVP | Especificado |
| BE-SEC-03 | MFA (TOTP) obligatorio para `ROLE_ADMIN` y `ROLE_OPERATOR` | ARQUITECTURA_GENERAL §9 | MVP | Nuevo |
| BE-SEC-04 | `POST /api/auth/logout`, `GET /api/me` con permisos efectivos | RF-01.4, RF-01.5 | MVP | Especificado |
| BE-SEC-05 | Autorización por política en **cada** endpoint; `403` sin ejecutar nada | US-02, RF-02.2–02.3 | MVP | Especificado |
| BE-SEC-06 | Suspender un usuario invalida sus tokens al instante | RF-02.4, RF-43.3 | MVP | Especificado |
| BE-SEC-07 | Interceptor de auditoría que registra entidad, acción, antes/después, usuario, IP y **user-agent** en la misma transacción | US-03, RF-03.1–03.2, RNF-02 | MVP | Especificado (falta `user_agent` en la BD) |
| BE-SEC-08 | `GET /api/audit-logs` con filtros, solo para Auditor y Admin | RF-03.4 | MVP | Especificado |
| BE-SEC-09 | **Cuatro ojos**: reversos, castigos, ajustes manuales, cambios de tasa y cambios de rol requieren la aprobación de un segundo usuario distinto del solicitante | RNF-26, RF-22.2, RNF-20 | F2 | Parcial (sin modelo de aprobaciones) |
| BE-SEC-10 | JWT firmado con RS256 y claves rotadas; secretos solo en el gestor de secretos | RNF-01, RNF-03 | MVP | Especificado |

## 2. Customers (clientes, KYC y cuentas)

| ID | Requerimiento | Origen | Fase | Estado |
|---|---|---|---|---|
| BE-CUS-01 | `POST /api/customers` y `GET /api/customers/{id}`: unicidad por tipo y número de documento (`409`) y validación con el regex del catálogo | US-04, RF-04.1–04.4 | MVP | Especificado |
| BE-CUS-02 | Historial auditado de cambios en ingresos, dirección y teléfono | RF-04.3 | MVP | Especificado |
| BE-CUS-03 | Carga de documentos KYC a Object Storage (solo URI y SHA-256 en BD); URLs firmadas y temporales para verlos | RF-05.1, RNF-04 | MVP | Especificado |
| BE-CUS-04 | Estados KYC `PENDING`, `VERIFIED`, `REJECTED` y `EXPIRED`, con vencimiento (`kyc_expires_at`) | RF-05.2 | MVP | Especificado |
| BE-CUS-05 | Verificación automática de identidad (consulta de DNI), biometría con prueba de vida y listas PEP/AML vía un puerto `IIdentityVerifier`; resultados guardados y revisables | US-05, RF-26.4, README §3.11 | MVP | Parcial (no hay proveedor ni registro de resultados) |
| BE-CUS-06 | Bloquear todo desembolso si el KYC no está `VERIFIED` (`422`) | RF-05.3, CA-05.2 | MVP | Especificado |
| BE-CUS-07 | Consentimientos por finalidad y versión del texto, con fecha e IP; poder revocar el de marketing | RF-05.4, Ley 29733 | MVP | Parcial (hoy un solo consentimiento) |
| BE-CUS-08 | Ficha 360: préstamos activos e históricos, deuda consolidada, atraso máximo actual e histórico, solicitudes y alerta de mora | US-06, RF-06.1–06.2 | MVP | Especificado |
| BE-CUS-09 | Cuentas del cliente: registrar CCI (20 dígitos) o billetera, verificar titularidad a su nombre, una sola primaria activa | Modelo digital | MVP | Nuevo |
| BE-CUS-10 | Cambiar la cuenta de desembolso exige OTP nuevo y bloquea desembolsos hacia la cuenta nueva durante un periodo configurable (24–48 h) | ARQUITECTURA_GENERAL §9 | MVP | Nuevo |
| BE-CUS-11 | OTP con propósito (login, contrato, cambio de cuenta), expiración, máximo de intentos y bloqueo por abuso | RF-26.2 | MVP | Parcial (sin propósito ni intentos) |

## 3. Products

| ID | Requerimiento | Origen | Fase | Estado |
|---|---|---|---|---|
| BE-PRO-01 | `POST/GET /api/loan-products` con validación de rangos (`min ≤ max`) | US-07, CA-07.2 | MVP | Especificado |
| BE-PRO-02 | Motor de amortización Francés, Flat, Alemán y Americano, con conversión TEA → tasa del periodo según la frecuencia | US-08, RF-08.1–08.2 | MVP | Especificado |
| BE-PRO-03 | Comisión de apertura fija o %, deductiva o financiada; cargos periódicos por cuota | RF-09.1 | MVP | Especificado |
| BE-PRO-04 | Separar **comisión de administración** y **seguro** (que puede ser un % del saldo, como el desgravamen) | README §3.1 | F2 | Parcial (hoy un único cargo periódico fijo) |
| BE-PRO-05 | Gracia, tasa de mora diaria, base de mora y anatocismo desactivado por defecto | RF-09.2–09.4 | MVP | Especificado |
| BE-PRO-06 | **Día de pago** fijo del producto para calcular los vencimientos | README §3.1 | MVP | Nuevo |
| BE-PRO-07 | Elegibilidad: edad, ingreso mínimo y **antigüedad laboral mínima** | RF-07.1, README §3.1 | MVP | Parcial (falta la antigüedad) |
| BE-PRO-08 | Validar que la TEA y la tasa de mora no superen el tope legal vigente (BCRP) al crear o editar un producto | Normativa (validar) | MVP | Nuevo |
| BE-PRO-09 | Cambiar un producto no afecta a los préstamos ya desembolsados (snapshot) | RNF-06 | MVP | Especificado |

## 4. Origination

| ID | Requerimiento | Origen | Fase | Estado |
|---|---|---|---|---|
| BE-ORI-01 | `POST /api/loan-applications` con admisión automática por monto, plazo y edad (`400` descriptivo) | US-10, RF-10.1–10.2 | MVP | Especificado |
| BE-ORI-02 | Admisión también por ingreso mínimo, antigüedad laboral y KYC completo | README §3.10 | MVP | Parcial |
| BE-ORI-03 | Simulación pura en memoria (`POST /api/loan-applications/simulate`) en menos de 50 ms, sin escribir en la BD | US-11, RF-11.1–11.4 | MVP | Especificado |
| BE-ORI-04 | Calcular y devolver la **TCEA** en simulación, solicitud y hoja resumen | Transparencia (validar) | MVP | Nuevo |
| BE-ORI-05 | DTI = (cuota proyectada + otras cuotas activas) / ingreso neto; recomendación automática | RF-12.1–12.2 | MVP | Especificado |
| BE-ORI-06 | Umbrales de DTI configurables por producto (hoy fijos en 35 % y 50 %; el README habla de 30–40 %) | README §3.10 | MVP | Nuevo |
| BE-ORI-07 | Dictamen con notas obligatorias (`422` sin notas) y monto aprobado; registro de quién y cuándo | RF-12.3–12.4 | MVP | Especificado |
| BE-ORI-08 | Una solicitud dictaminada no cambia de estado sin reapertura autorizada por un administrador | RNF-08 | MVP | Especificado |
| BE-ORI-09 | Congelar en la solicitud los **insumos** del cálculo (cuota, otras cuotas, ingreso, versión de reglas), no solo el resultado | RNF-09 | MVP | Parcial (hoy solo `dti_ratio` y `score`) |
| BE-ORI-10 | La solicitud pasa a `DESEMBOLSADO` cuando el desembolso se confirma | RF-13.4, RF-27.3 | MVP | Especificado (el estado falta en la BD) |

## 5. Loans (préstamo, contrato, cronograma, prepago y reestructuración)

| ID | Requerimiento | Origen | Fase | Estado |
|---|---|---|---|---|
| BE-LOA-01 | `POST /api/loans` con `Idempotency-Key` (misma respuesta si se repite); valida solicitud aprobada, KYC, contrato aceptado y cuentas verificadas | US-13, RF-13.1–13.3, CA-13.2 | MVP | Especificado |
| BE-LOA-02 | Crear préstamo y desembolso `PENDING` + mensaje Outbox en una transacción; activar el préstamo y generar el cronograma solo al confirmarse el desembolso | RF-13.4, RNF-10 | MVP | Especificado (falta el estado previo en la BD) |
| BE-LOA-03 | Inicio de intereses = fecha de confirmación bancaria | RF-13.4.1 | MVP | Especificado |
| BE-LOA-04 | Cronograma: redondeo a 2 decimales por componente, residuo en la última cuota (`Σ capital = principal`), desplazamiento por feriados y fines de semana | US-14, RF-14.1–14.4 | MVP | Especificado |
| BE-LOA-05 | Cronograma inmutable; los pagos solo acumulan en `paid_*` | RF-14.4, RNF-11 | MVP | Especificado |
| BE-LOA-06 | PDF de contrato, pagaré y hoja resumen desde la plantilla vigente, con los mismos valores del cronograma | US-15, CA-15.1 | MVP | Especificado |
| BE-LOA-07 | **Evidencia de aceptación electrónica**: versión de plantilla, hash SHA-256 del PDF, OTP usado, IP, user-agent y fecha | RF-27.4 | MVP | Parcial (no está modelada) |
| BE-LOA-08 | Cotización de prepago total a una fecha, con vigencia; al pagarla, cuotas `CANCELLED_BY_PREPAYMENT` y préstamo `PAGADO` | US-30 | MVP | Especificado |
| BE-LOA-09 | Prepago **sin penalidad** y con reducción de intereses para personas naturales; la política flat de "cobrar todo el interés" y la penalidad deben revisarse | Ley 29571 art. 85 (validar), RF-30.2 | MVP | Nuevo |
| BE-LOA-10 | Prepago parcial (abono extraordinario) con recálculo de cuota o de plazo, como nueva versión del cronograma | Ley 29571 art. 85 (validar) | F2 | Nuevo |
| BE-LOA-11 | Cancelación total el mismo día del desembolso (interés devengado = 0) como caso de prueba explícito | README §3.13 | MVP | Parcial |
| BE-LOA-12 | Reestructuración con nueva versión del cronograma, límite de días de mora y pago inicial configurables | US-31 | F2 | Especificado |
| BE-LOA-13 | Refinanciamiento: los préstamos anteriores se liquidan y el nuevo se crea **en una transacción**; el monto adicional se transfiere después vía Outbox (no puede formar parte de esa transacción) | US-32, RNF-21 | F2 | Parcial (RNF-21 pide todo atómico, incluso el desembolso) |
| BE-LOA-14 | Dictamen de reestructuración o refinanciamiento aprobado por el comité o el jefe de créditos | RNF-20 | F2 | Parcial (sin aprobaciones modeladas) |

## 6. Payments

| ID | Requerimiento | Origen | Fase | Estado |
|---|---|---|---|---|
| BE-PAY-01 | `POST /api/loans/{id}/payments` con `Idempotency-Key`, `SELECT ... FOR UPDATE` del préstamo dentro de una transacción | US-16, RF-16.1–16.3, RNF-12 | MVP | Especificado |
| BE-PAY-02 | Un pago nace `PENDING_CONFIRMATION` y solo se aplica al confirmarse por webhook o extracto; un voucher nunca basta | RF-16.2.1 | MVP | Especificado (la BD aún usa `DEFAULT 'APPLIED'`) |
| BE-PAY-03 | **Intención de pago**: crear la orden o el QR (monto, vencimiento, préstamo) antes de que el cliente pague, para enlazar el webhook | RF-29.1–29.2 | MVP | Nuevo |
| BE-PAY-04 | Prelación en cascada por cuota más antigua y detalle de imputación por cuota | RF-16.4–16.5 | MVP | Especificado |
| BE-PAY-05 | Estados de cuota `PAID`/`PARTIAL`; préstamo `PAGADO` al cubrir todo | RF-16.6–16.7 | MVP | Especificado |
| BE-PAY-06 | Excedente a la cuota siguiente; si no hay más deuda, saldo a favor y transacción `CUSTOMER_CREDIT_BALANCE` | US-17 | MVP | Especificado |
| BE-PAY-07 | Usar el saldo a favor en otro préstamo del cliente o **devolverlo** por transferencia a una cuenta verificada | RF-17.3 | F2 | Parcial (falta `REFUND`) |
| BE-PAY-08 | Pago de un cliente con varios préstamos: regla de distribución configurable (primero el de mayor mora) | README §3.13 | F2 | Nuevo |
| BE-PAY-09 | Reverso con contra-asiento, motivo obligatorio y permiso `payments.reverse`; doble reverso → `409` | US-18 | MVP | Especificado |
| BE-PAY-10 | Contracargos de tarjeta y anulaciones de la pasarela como reversos automáticos con su propio tipo | Modelo digital | F2 | Nuevo |
| BE-PAY-11 | Recibo digital correlativo en PDF, enviado por notificación | RNF-13, RF-29.4 | MVP | Especificado |
| BE-PAY-12 | Comprobante electrónico SUNAT por intereses y comisiones si la empresa no está supervisada por la SBS | Normativa tributaria (validar) | MVP | Nuevo |
| BE-PAY-13 | La mora se calcula con la fecha efectiva de la operación (`paid_at`), no con la hora de llegada de la confirmación | ARQUITECTURA_GENERAL §6.3 | MVP | Nuevo |

## 7. Treasury

| ID | Requerimiento | Origen | Fase | Estado |
|---|---|---|---|---|
| BE-TRE-01 | Gestión de cuentas propias con uso de desembolso o cobro; una cuenta inactiva no se usa | US-23 | MVP | Especificado |
| BE-TRE-02 | Envío del desembolso al banco desde el Outbox, con clave idempotente propia; ciclo `PENDING → PROCESSING → COMPLETED / FAILED / REVERSED` | US-25, RF-25.1 | MVP | Especificado |
| BE-TRE-03 | Validar antes de enviar: cuenta de origen activa, cuenta destino del mismo cliente y verificada, misma moneda y límite del canal | RF-25.5, RNF-16 | MVP | Especificado |
| BE-TRE-04 | Reintentar un desembolso `FAILED` tras corregir la cuenta | RF-25.3 | MVP | Parcial (la BD lo impide hoy) |
| BE-TRE-05 | Alertas por desembolsos en `PROCESSING` por más de N horas; consulta de estado al banco | RF-25.3 | MVP | Especificado |
| BE-TRE-06 | Importar extractos (archivo, API o webhook) sin duplicados; match automático y bandeja manual | US-37 | MVP | Especificado |
| BE-TRE-07 | Liquidaciones en lote de pasarelas: 1 depósito ↔ N pagos, con registro de la comisión de la pasarela | Modelo digital | F2 | Nuevo |
| BE-TRE-08 | Cuadre diario por cuenta (libro mayor vs. extracto) e inmutabilidad del cuadre cerrado | US-24, RNF-17 | F2 | Especificado |

> [!IMPORTANT]
> **Nota V1 (Transferencias Internas entre Cuentas Propias de la Empresa — Fondeo / Barrido de Tesorería):**
> En una fintech real no se usa una sola cuenta bancaria para todo:
> - Existe una **Cuenta Recaudadora** (ej. donde entran todos los cobros de Yape/Plin/Pasarela).
> - Y existe una **Cuenta Pagadora de Desembolsos** (desde donde salen las transferencias a los clientes).
> - Todas las semanas el tesorero transfiere dinero de la Cuenta Recaudadora a la Cuenta de Desembolsos para fondearla (*fondeo o barrido de tesorería*).
> - **Qué pasa si no se contempla:** Al importar el extracto del banco, se verá una salida de `-S/ 50,000` en una cuenta y una entrada de `+S/ 50,000` en la otra que no pertenecen a ningún préstamo ni a ningún cliente, y el **Cuadre Diario (`BE-TRE-08`) quedará descuadrado**. Por ello, en V1 se incluyen los asientos simétricos (`INTERNAL_TRANSFER_OUT` / `INTERNAL_TRANSFER_IN`) y endpoints `GET/POST /api/treasury/internal-transfers` (`RF-24.4` a `RF-24.6`) para conciliar estos movimientos internos sin afectar préstamos.


## 8. Collections

| ID | Requerimiento | Origen | Fase | Estado |
|---|---|---|---|---|
| BE-COL-01 | Job de mora 00:05 (hora de Lima), idempotente y por lotes de 500, para 100 000 préstamos en menos de 10 min | US-19, RNF-14 | MVP | Especificado |
| BE-COL-02 | El job ignora las cuotas `PAID` **y** `CANCELLED_BY_PREPAYMENT` | RF-19.2 | MVP | Parcial (RF-19.2 solo excluye `PAID`) |
| BE-COL-03 | Gracia: dentro del plazo no hay mora; al superarlo se cobra por todos los días de atraso | RF-19.3, CA-09.1 | MVP | Especificado |
| BE-COL-04 | Transición `VIGENTE ↔ EN_MORA` automática | RF-19.4 | MVP | Especificado |
| BE-COL-05 | Aging por tramos con filtros por gestor, monto y producto | US-20 | MVP | Especificado |
| BE-COL-06 | Asignación de préstamos a gestores (manual o automática por tramo) | RF-20.2 | F2 | Parcial |
| BE-COL-07 | Gestiones y promesas; marcar `PROMESA_INCUMPLIDA` | US-21 | MVP | Especificado |
| BE-COL-08 | Castigo con acta de comité y aprobación de 4 ojos; el préstamo `CASTIGADO` sigue aceptando pagos de recuperación | US-22 | F2 | Parcial |
| BE-COL-09 | Recordatorios automáticos preventivos y de mora por SMS, WhatsApp y correo, respetando horarios y frecuencia permitidos | README §3.12, RNF-15 | MVP | Parcial (sin reglas de horario ni plantillas) |

## 9. Integrations y Notifications

| ID | Requerimiento | Origen | Fase | Estado |
|---|---|---|---|---|
| BE-INT-01 | `POST /api/integrations/webhooks/{provider}`: HMAC-SHA256 sobre el **cuerpo crudo**, *timestamp* con ventana de ±5 min y `401` con alerta si falla | US-36, RF-36.1–36.2 | MVP | Especificado |
| BE-INT-02 | Guardar el evento, deduplicar por `(provider, external_id)` y responder `200` en menos de 300 ms; procesar en el worker | RF-36.3–36.5, RNF-24 | MVP | Especificado |
| BE-INT-03 | Reintentos 30 s, 2 min, 10 min y 30 min; luego DLQ con reintento manual | US-38 | MVP | Especificado |
| BE-INT-04 | TLS y lista de IPs permitidas por proveedor | RNF-25 | MVP | Especificado |
| BE-INT-05 | Adaptadores detrás de puertos comunes (`IBankTransferGateway`, `IPaymentGateway`, `IIdentityVerifier`, `IMessageSender`) con ambiente *sandbox* en staging | ARQUITECTURA_GENERAL §8 | MVP | Nuevo |
| BE-INT-06 | Débito automático o cargo recurrente con tarjeta tokenizada | README §1 (fases posteriores) | F3 | Nuevo |
| BE-NOT-01 | Notificaciones por cola (Outbox) con plantillas versionadas por código, canal y estado de envío | RNF-15 | MVP | Parcial (no hay catálogo de plantillas) |
| BE-NOT-02 | Respetar las preferencias y el consentimiento del cliente por canal | Ley 29733 | MVP | Nuevo |

## 10. Reports y Administration

| ID | Requerimiento | Origen | Fase | Estado |
|---|---|---|---|---|
| BE-REP-01 | KPIs del dashboard con cache de 60 s | US-33 | MVP | Especificado |
| BE-REP-02 | KPIs de tasa de aprobación, tiempo de originación, duración media y tasa de cancelación anticipada | README §11 | F2 | Nuevo |
| BE-REP-03 | Reportes de cartera, aging (con provisión), flujo de caja y productividad, con exportación Excel por *streaming* y PDF | US-34, RNF-23, README §6.1 | MVP | Parcial (falta productividad) |
| BE-REP-04 | Estado de cuenta matemáticamente consistente y PDF con QR | US-35, CA-35.1 | MVP | Especificado |
| BE-REP-05 | Los reportes leen de una réplica o de vistas materializadas | RNF-22 | F2 | Especificado |
| BE-ADM-01 | Empresa, canales digitales, feriados, catálogos, plantillas versionadas y usuarios | US-39 a US-43 | MVP | Especificado |
| BE-ADM-02 | Catálogos y parámetros en cache (Redis) con invalidación inmediata | RNF-27 | MVP | Especificado |
| BE-ADM-03 | Parámetros globales configurables (horario de cobranza, espera por cambio de cuenta, vigencia de cotizaciones, umbrales de alertas) | README §6.1 (módulo 11) | F2 | Nuevo |
| BE-ADM-04 | **Libro de Reclamaciones virtual**: registro, número correlativo, plazo de respuesta y constancia al cliente | Normativa de consumo (validar) | MVP | Nuevo |
| BE-ADM-05 | Reportes PLAFT para el oficial de cumplimiento (operaciones inusuales, PEP) | Normativa SBS/UIF (validar) | F2 | Nuevo |

---

## 11. Requerimientos no funcionales

| ID | Requerimiento | Origen | Estado |
|---|---|---|---|
| BE-NF-01 | Simulación < 50 ms; webhook < 300 ms; escritura de auditoría sin elevar la respuesta por encima de 200 ms | CA-11.1, RNF-24, RNF-02 | Especificado |
| BE-NF-02 | Idempotencia en tres niveles: HTTP (Redis 24 h + `UNIQUE` en BD), webhooks (`UNIQUE(provider, external_id)`) y jobs | RF-13.2, RF-36.4, RF-19.5 | Especificado |
| BE-NF-03 | Outbox transaccional para transferencias, notificaciones y eventos entre módulos; nunca llamar al banco dentro de una transacción de BD | RNF-10, ARQUITECTURA_GENERAL §7 | Especificado |
| BE-NF-04 | Errores en `ProblemDetails` (RFC 7807) con códigos `400`, `401`, `403`, `404`, `409`, `422` y `500` | backend-net §5 | Especificado |
| BE-NF-05 | Contrato OpenAPI publicado y versionado como fuente única para el frontend y los proveedores | ARQUITECTURA_GENERAL §11 | Nuevo |
| BE-NF-06 | Observabilidad con OpenTelemetry: trazas con id de correlación y `external_reference`, métricas y alertas por integración degradada | README §8, LINEA §14 | Nuevo |
| BE-NF-07 | Pruebas: motor financiero contra un Excel patrón, 10 pagos concurrentes sobre un mismo préstamo e integración con PostgreSQL real (Testcontainers) | backend-net §7 | Especificado |
| BE-NF-08 | Prueba de arquitectura que impida que un módulo use el interior de otro | ARQUITECTURA_GENERAL §5 | Nuevo |
| BE-NF-09 | Jobs con bloqueo distribuido (*clustering* de Quartz.NET) para que dos instancias no los ejecuten a la vez | ARQUITECTURA_GENERAL §10 | Nuevo |
| BE-NF-10 | Fechas de negocio y cron en `America/Lima`; almacenamiento en UTC | README §8 | Especificado |

---

## 12. Brechas frente al negocio (backend)

1. **Cumplimiento en Perú (validar con asesoría):**
   - Prepago sin penalidad y con reducción de intereses (BE-LOA-09 y BE-LOA-10).
   - Tope de tasas del BCRP (BE-PRO-08).
   - TCEA (BE-ORI-04).
   - Comprobantes SUNAT (BE-PAY-12).
   - Libro de Reclamaciones (BE-ADM-04).
   - PLAFT (BE-ADM-05).
2. **Flujo digital incompleto:**
   - Intención de pago (BE-PAY-03).
   - Reintento de desembolsos (BE-TRE-04).
   - Devoluciones y contracargos (BE-PAY-07 y BE-PAY-10).
   - Liquidaciones en lote (BE-TRE-07).
   - Verificaciones KYC automáticas (BE-CUS-05).
   - Cuentas del cliente y su cambio seguro (BE-CUS-09 y BE-CUS-10).
3. **Reglas de negocio del README sin historia:**
   - Día de pago (BE-PRO-06).
   - Antigüedad laboral (BE-PRO-07).
   - DTI configurable (BE-ORI-06).
   - Comisión de administración separada del seguro (BE-PRO-04).
   - Pago a varios préstamos (BE-PAY-08).
   - KPIs de originación (BE-REP-02).
   - Parámetros globales (BE-ADM-03).
4. **Controles internos:**
   - Aprobación de 4 ojos (BE-SEC-09).
   - Asignación de gestores (BE-COL-06).
   - Insumos del scoring congelados (BE-ORI-09).
5. **Inconsistencias entre historias que hay que corregir:**
   - RF-19.2 no excluye las cuotas canceladas por prepago (BE-COL-02).
   - RNF-21 exige incluir el desembolso del refinanciamiento en la misma transacción, lo que es imposible con una transferencia bancaria (BE-LOA-13).
   - La solicitud debe pasar a `DESEMBOLSADO`, pero ese estado no existe en la BD (BE-ORI-10).

---

## 13. Evolución V2 — Requerimientos de Lógica de Negocio Avanzada (Backend, Jobs y Datos)

Esta sección detalla los requerimientos técnicos de Backend para la **Versión 2.0 (V2)** especificados en [`14-modelo-negocio-v2.md`](14-modelo-negocio-v2.md), organizados por módulo del Monolito Modular (.NET).

### 13.1 Clientes, Buró de Crédito y Fidelización (`Customers` & `Origination` V2)

| ID | Requerimiento | Origen | Fase | Estado |
|---|---|---|---|---|
| BE-CUS-11 | **Integración con Centrales de Riesgo (`ICreditBureauGateway`)**: adaptador para Equifax/Infocorp, Sentinel y reporte RCC SBS con caché configurable (15 días por DNI/RUC) y persistencia del snapshot inmutable en `credit_bureau_reports` | US-44, RF-44.1 | V2 | Especificado |
| BE-CUS-12 | **Motor de Escalonamiento (*Credit Ladder*) y Ofertas Pre-Aprobadas**: cálculo automático del nivel del cliente (`Tier 1..4`) al liquidar préstamos o superar el $80\%$ de cuotas pagadas con atraso máximo histórico $\le 3$ días; generación de `pre_approved_offers` con fecha de expiración | US-45, RF-45.1–45.3 | V2 | Especificado |
| BE-ORI-12 | **Cálculo de DTI Sistémico Real y *Knock-out Rules* de Buró**: incluir la cuota mensual externa reportada en el RCC (`external_monthly_installment`) en el numerador del DTI y rechazar automáticamente clasificaciones SBS `DEFICIENTE`, `DUDOSO` o `PERDIDA` o exceso de entidades acreedoras (*loan stacking*) | US-44, RF-44.2–44.3 | V2 | Especificado |
| BE-ORI-13 | **Aceptación 1-Click de Oferta Pre-Aprobada (*Fast-Track*)**: endpoint `POST /api/portal/me/pre-approved-offers/{offerId}/accept` que origina y aprueba automáticamente la solicitud si el buró vigente sigue en `NORMAL` y el DTI está dentro del umbral, pasando directo a generación de contrato | US-45, RF-45.3 | V2 | Especificado |
| BE-ORI-14 | **Gestión y Validación de Intervinientes Adicionales (Aval / Fiador Solidario y Cónyuge)**: registro en `application_co_debtors`, validación obligatoria de su KYC y buró `NORMAL`, y bloqueo del paso a contrato si el producto o dictamen exige aval y este no está validado | US-54, RF-54.1–54.2 | V2 | Especificado |

### 13.2 Ingeniería de Producto y Topes Regulatorios (`Products` & `FinancialEngine` V2)

| ID | Requerimiento | Origen | Fase | Estado |
|---|---|---|---|---|
| BE-PRO-10 | **Cálculo de Seguro de Desgravamen sobre Saldo Insoluto**: soporte en el motor financiero para tasa mensual/diaria de desgravamen aplicada sobre el saldo capital de cada período ($S_{k-1}$), integrando la tasa de seguro en la fórmula de anualidad del método Francés para conservar cuotas totales constantes y $\sum \text{Capital}_k = \text{Principal}$ | US-46, RF-46.1 | V2 | Especificado |
| BE-PRO-11 | **Períodos de Gracia Inicial (`PARTIAL` y `TOTAL`) e ITF ($0.005\%$)**: generación matemática del cronograma con $g$ cuotas iniciales de gracia parcial (pago solo de interés + seguro, $\text{Capital}_k = 0$) o gracia total (diferimiento/capitalización de intereses de los primeros $g$ períodos en las $n - g$ cuotas restantes), más desglose de ITF | US-46, RF-46.2–46.3 | V2 | Especificado |
| BE-PRO-12 | **Validador Automático de Tasa Máxima BCRP (Ley N° 31143 contra la Usura)**: tabla `regulatory_rate_caps` por moneda y vigencia semestral; interceptor de dominio que rechaza con `422 ERR_USURY_CAP_EXCEEDED` cualquier creación/edición de producto, simulación o contrato cuya TEA compensatoria o moratoria supere el tope legal del BCRP | US-47, RF-47.1–47.2 | V2 | Especificado |

### 13.3 Préstamos, Firma Multiparte, Condonaciones y Convenios (`Loans` & `Collections` V2)

| ID | Requerimiento | Origen | Fase | Estado |
|---|---|---|---|---|
| BE-LOA-14 | **Firma Electrónica Mancomunada Multiparte con OTP Independiente**: entidad `contract_signers` por cada interviniente (`PRIMARY_BORROWER`, `SOLIDARY_GUARANTOR`, `SPOUSE`); estado `PARTIALLY_SIGNED` hasta que todos los firmantes validen su OTP individual (`FULLY_SIGNED`), bloqueando la orden de desembolso mientras falte alguna firma | US-55, RF-55.1–55.2 | V2 | Especificado |
| BE-LOA-15 | **Reestructuración Condicionada a Cuota Inicial (*Down Payment*)**: creación de convenio en estado `PENDING_DOWN_PAYMENT` con `required_down_payment_amount` y `down_payment_due_date`; listener de eventos de pago que activa atómicamente la versión `v+1` del cronograma recién cuando se confirma el abono inicial | US-49, RF-49.1–49.2 | V2 | Especificado |
| BE-COL-10 | **Motor de Condonaciones (*Waivers*) con Aprobación de 4 Ojos y Expiración**: registro en `loan_waivers` de descuentos propuestos sobre mora, penalidades e interés vencido (nunca sobre capital), aprobación por supervisor distinto al solicitante y aplicación atómica del asiento `WAIVER_CREDIT` al confirmarse el pago de liquidación dentro de `valid_until` | US-48, RF-48.1–48.2 | V2 | Especificado |
| BE-COL-11 | **Job de Caducidad de Condonaciones y Convenios Impagos (`AgreementExpirationJob`)**: job programado que marca como `EXPIRED` los *waivers* y convenios `PENDING_DOWN_PAYMENT` cuya fecha límite venció sin pago confirmado | US-48, US-49 | V2 | Especificado |

### 13.4 Tesorería, Pasarelas en Lote y Débito Automático (`Treasury`, `Payments` & `Integrations` V2)

| ID | Requerimiento | Origen | Fase | Estado |
|---|---|---|---|---|
| BE-TRE-09 | **Conciliador de Liquidaciones en Lote de Pasarelas (`gateway_settlements`)**: endpoint y procesador que vincula `1 línea de extracto bancario (Neto) ↔ 1 Lote de Liquidación ↔ N Pagos de clientes (Bruto)`, validando al centavo $\sum \text{Bruto} - \text{Comisión} - \text{IGV} = \text{Neto}$ y asentando el gasto financiero de pasarela | US-50, RF-50.1–50.2 | V2 | Especificado |
| BE-INT-07 | **Bóveda de Referencias de Tarjetas Tokenizadas y Mandatos (*Auto-Pay*)**: almacenamiento seguro en `customer_autopay_methods` exclusivamente del `gateway_card_token`, `last4`, marca, expiración y firma OTP del mandato (prohibido almacenar PAN o CVV) | US-51, RF-51.1 | V2 | Especificado |
| BE-INT-08 | **Job Diario de Cargo Recurrente (`AutoPayChargeJob` 07:00 AM Lima) y Reintentos**: ejecución idempotente de cobros automáticos el día de vencimiento de la cuota vía `IPaymentGateway.ChargeTokenAsync`, con programación de reintentos en $T+1$ y $T+3$ ante falta de fondos y desactivación automática ante rechazo definitivo de tarjeta | US-51, RF-51.2 | V2 | Especificado |

### 13.5 Facturación Electrónica Tributaria (SUNAT / OSE) y Contabilidad (`Accounting` & `Tax` V2)

| ID | Requerimiento | Origen | Fase | Estado |
|---|---|---|---|---|
| BE-TAX-01 | **Determinación Tributaria de Base Imponible en Pagos de Crédito**: separación estricta entre retorno de capital (`principal_paid`, no afecto a comprobante de venta) e ingresos financieros gravables/inafectos (`interest_paid + late_fee_paid + fee_paid`) para la emisión de Boleta (`03`) o Factura (`01`) | US-52, RF-52.1 | V2 | Especificado |
| BE-TAX-02 | **Worker Outbox de Facturación Electrónica UBL 2.1 (`IElectronicBillingGateway`)**: generación de serie/correlativo fiscal, envío asíncrono a OSE/PSE tras confirmar cada pago con ingresos $> 0$, recepción y almacenamiento de XML firmado, CDR de SUNAT y PDF representativo en `tax_vouchers` | US-52, RF-52.2 | V2 | Especificado |
| BE-TAX-03 | **Emisión Automática de Nota de Crédito Electrónica (`07`) en Reversos**: al ejecutar `POST /api/payments/{id}/reverse` sobre un pago que emitió Boleta o Factura, encolar automáticamente la emisión de la Nota de Crédito Electrónica por anulación referenciando el comprobante original | US-52, RF-52.3 | V2 | Especificado |
| BE-ACC-01 | **Libro Mayor Inmutable de Partida Doble (`accounting_journal_entries`)**: generación transaccional de asientos contables (`Debe = Haber`) por cada desembolso, cobro, condonación, reverso, liquidación de pasarela, devolución y castigo, con catálogo de cuentas PCGE/SBS configurable | US-53, RF-53.1 | V2 | Especificado |
| BE-ACC-02 | **Motor de Cálculo Mensual de Provisiones de Cartera por Tramo SBS**: proceso de cierre mensual que clasifica la cartera activa por días de mora (`0-8d: 1%`, `9-30d: 5%`, `31-60d: 25%`, `61-120d: 60%`, `>120d: 100%`), guarda la foto en `portfolio_provision_runs` y genera el asiento contable de ajuste de provisión | US-53, RF-53.2 | V2 | Especificado |

