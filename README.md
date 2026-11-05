# Sistema de Préstamos — Plan y Lógica de Negocio

Documento base para el diseño e implementación de una plataforma de gestión de créditos/préstamos.
Esta primera versión **prioriza la lógica de negocio** (el "por qué" y las reglas), antes de la arquitectura técnica.

---

## 1. Visión y alcance

**Objetivo:** gestionar el ciclo de vida completo de un préstamo: solicitud → evaluación → desembolso → cobranza → liquidación, con cálculo financiero correcto y trazabilidad total (auditoría).

**Modelo operativo: Fintech 100% digital.**
- **Desembolsos:** solo por transferencia (CCI) o billetera digital a una cuenta verificada a nombre del cliente.
- **Pagos:** solo por canales digitales: transferencia bancaria, Yape/Plin, tarjeta de débito y pasarela de pago. La tarjeta de crédito queda sujeta a decisión de negocio.
- **Sin** sucursales, cajas, ventanillas, bóvedas, arqueos ni efectivo. El dinero vive en las cuentas bancarias de la empresa y se controla con conciliación contra el extracto.

**En alcance (Fase 1):**
- Registro de clientes y KYC básico.
- Productos de crédito configurables.
- Simulación y originación de préstamos.
- Cronograma de pagos (plan de amortización).
- Registro de pagos, mora y cancelación anticipada.
- Reportes y estados de cuenta.

**Canales / interfaces esperadas:**
- **Panel administrativo web** para analistas, operadores, cobranza, tesorería, auditoría y administración.
- **Portal del cliente web responsive** para solicitud, seguimiento, consulta de cronograma, estado de cuenta y pagos.
- **App móvil del cliente** como fase posterior, reutilizando la misma API de negocio.

**En alcance desde la Fase 1 por ser 100% digital:** al menos un canal de cobro (pasarela o banco con webhook), desembolso por transferencia y conciliación básica con el extracto bancario. Sin ellos no hay forma de cobrar ni desembolsar.

**Fuera de alcance (fases posteriores):** scoring con buró de crédito, app móvil del cliente, canales de pago adicionales y débito automático, cobranza automatizada, contabilidad/IFRS completa.

---

## 2. Conceptos y entidades

| Entidad | Descripción |
|---|---|
| **Cliente (Borrower)** | Persona natural o jurídica que solicita el crédito. |
| **Producto de Crédito** | Plantilla con condiciones: tasa, plazo, frecuencia, métodos, comisiones. |
| **Solicitud (Loan Application)** | Petición de crédito a evaluar. |
| **Préstamo (Loan)** | Crédito aprobado y desembolsado. |
| **Cronograma (Schedule)** | Lista de cuotas con capital, interés, saldo y fechas. |
| **Cuota (Installment)** | Obligación de pago en una fecha: monto esperado vs. pagado. |
| **Pago (Payment)** | Abono del cliente, se distribuye entre mora/interés/capital. |
| **Transacción** | Movimiento financiero (desembolso, pago, ajuste, condonación). |

---

## 3. Lógica de negocio (NÚCLEO)

### 3.1 Productos de crédito

Cada producto define:

- **Tasa de interés** y su tipo: nominal / efectiva; anual (TEA) / mensual (TEM).
- **Método de cálculo:** ver 3.2.
- **Plazo** (nº de cuotas) y **frecuencia de pago:** diaria, semanal, quincenal, mensual.
- **Día de pago** y **días de gracia**.
- **Comisiones:** apertura/desembolso, administración, seguro.
- **Mora:** tasa y base de cálculo (ver 3.5).
- **Monto mínimo / máximo**.
- **Reglas de elegibilidad** (ingresos, antigüedad, edad).
- **Días de gracia** antes de clasificar como mora.

### 3.2 Métodos de cálculo de interés

**a) Interés simple / Flat (cuota fija):**
Ideal para microcréditos. El interés se calcula sobre el capital original y no sobre el saldo.

```
Interés total = Capital × i × n
Cuota fija    = (Capital + Interés total) / n
```

**b) Amortización Francesa (cuota nivelada):** la más usada en banca. Cuota constante, interés decreciente, capital creciente.

```
Cuota (R) = P × i / (1 − (1 + i)^(−n))
  P = capital      i = tasa del período      n = nº de cuotas
Interés del período = Saldo × i
Capital del período = R − Interés
Saldo nuevo         = Saldo − Capital
```

**c) Amortización Alemana:** capital constante, cuota decreciente (interés sobre saldo).

```
Capital del período = P / n
Interés del período = Saldo × i
Cuota               = Capital + Interés
```

**d) Americano (bullet):** solo se pagan intereses cada período y el capital al final.

> Regla: **toda tasa debe declararse como tasa efectiva** de referencia (ej. TEA) para comparar productos. Guardar la tasa como decimal de alta precisión (no entero).

### 3.3 Generación del cronograma

1. Calcular cuota base según método y frecuencia.
2. Por cada período generar: `nº cuota`, `fecha vencimiento`, `capital`, `interés`, `monto cuota`, `saldo capital`, `estado`.
3. Ajustar **redondeo** en la última cuota para que `Σ capital = capital desembolsado` (evitar descuadres de centavos).
4. Guardar el cronograma **inmutable** al desembolsar (snapshot); los recálculos usan el snapshot.

Campos mínimos de una cuota:

```
installment_number | due_date | principal | interest | fee | total_due
paid_principal | paid_interest | paid_fee | late_fee
status (PENDING | PARTIAL | PAID | OVERDUE) | days_late
```

### 3.4 Desembolso

- Al aprobar y desembolsar: registrar `disbursement_amount`, `disbursement_date`, comisión de apertura (si aplica) y generar el cronograma.
- El desembolso es una **transferencia digital asíncrona** (`PENDING → PROCESSING → COMPLETED | FAILED`) hacia la cuenta verificada del cliente; el cronograma y el asiento contable se confirman cuando el banco confirma la transferencia.
- **Fecha de desembolso = fecha de inicio** del cómputo de intereses = fecha de confirmación bancaria de la transferencia.
- Si el producto cobra comisión de apertura, descontarla del desembolso o registrarla como cargo separado (configurable).

### 3.5 Pagos y orden de aplicación

Un pago **nunca** se aplica primero a capital. Orden obligatorio:

1. **Mora / penalidades** acumuladas.
2. **Interés** del período vencido (y luego corriente).
3. **Comisiones** vencidas.
4. **Capital**.

Reglas:
- Si el pago no cubre la cuota completa → cuota en estado `PARTIAL` y el resto sigue pendiente.
- Sobrepago → a la siguiente cuota; si ya no hay cuotas, genera **saldo a favor** del cliente.
- Los pagos son **transacciones inmutables**; las correcciones se hacen con contra-asientos (no se edita el pasado).

### 3.6 Mora y penalidades

- Base de cálculo: **saldo de capital vencido** o **cuota vencida** (configurable por producto).
- Fórmula típica:

```
Mora = Monto vencido × tasa_mora_por_día × días_de_atraso
```

- `días_de_atraso` = días desde `due_date` hasta la fecha efectiva (o de pago).
- **Días de gracia:** no genera mora dentro de ese rango.
- Clasificación de mora (aging): 1–30, 31–60, 61–90, >90 días.
- Mora **no capitaliza** salvo que el producto lo permita explícitamente (anatocismo).

### 3.7 Cancelación anticipada (prepago)

- El cliente paga el 100 % del saldo. Se recalcula el cronograma restante.
- Definir política: ¿se cobra todo el interés pactado (flat) o solo el interés devengado (rebaja)?
  - **Flat/precomputed:** normalmente se cobra el interés total (no hay ahorro), salvo penalidad por prepago.
  - **Francés/alemán:** se cobra solo interés devengado + posible **penalidad por prepago** (% del capital).
- Generar "liquidación/prepago" con el monto exacto a la fecha.

### 3.8 Refinanciamiento / reestructuración

- **Refinanciar:** solicitar un nuevo préstamo para pagar el anterior (consolidación).
- **Reestructurar:** modificar plazo/tasa del préstamo vigente. Se genera **nuevo cronograma** y se conserva el histórico.
- Reglas: no permitir reestructurar si hay mora > N días (configurable), o exigir un pago inicial.

### 3.9 Estados del préstamo

```
SOLICITADO → EN_EVALUACION → APROBADO → DESEMBOLSADO
                                           ↓
                              VIGENTE ←→ EN_MORA
                                 ↓          ↓
                              PAGADO     CASTIGADO (write-off)
                                 ↑
                        CANCELADO / ANULADO (antes de desembolso)
```

- `VIGENTE`: al día.
- `EN_MORA`: existe al menos una cuota vencida sin pagar.
- `PAGADO`: todas las cuotas liquidadas.
- `CASTIGADO`: irrecuperable, pasa a pérdida contable (no se elimina).
- Los estados se calculan por **job programado** (diario), no de forma manual.

### 3.10 Evaluación de crédito (approval)

Reglas de decisión (motor de reglas configurable):

- **Elegibilidad:** edad (18–70), ingresos mínimos, antigüedad laboral, KYC completo.
- **Capacidad de pago:** `cuota ≤ % del ingreso neto` (ej. ≤ 30–40 %).
- **Scoring:** puntaje por variables (ingresos, historial interno, garantías).
- **Decisiones:** `APROBADO`, `APROBADO CON CONDICIONES`, `RECHAZADO`, `REVISIÓN MANUAL`.
- Todo cambio de decisión debe quedar **auditado** (quién, cuándo, por qué).

### 3.11 KYC / Cumplimiento

- Verificación de identidad (documento), listas de riesgo (AML/PEP), consentimiento de datos.
- Guardar evidencias y fecha de verificación. Bloquear desembolso si KYC incompleto.

### 3.12 Cobranza (collections)

- Recordatorios automáticos por vencimiento (email/SMS/WhatsApp).
- Bandeja por **aging** (días de atraso) y priorización.
- Acciones: aviso, llamada, acuerdo de pago, refinanciamiento, castigo.

### 3.13 Casos borde a cubrir

- Redondeo y descuadre de centavos en la última cuota.
- Pagos antes de la primera cuota / pagos el mismo día del vencimiento.
- Pago mayor al total adeudado (saldo a favor).
- Ajuste de fechas por feriados/fines de semana (regla: desplazar al día hábil).
- Cliente con múltiples préstamos activos (aplicación del pago por prioridad de mora).
- Corrección de pagos (reversión con contra-asiento).
- Cancelación total el mismo día del desembolso.

---

## 4. Roles y permisos

| Rol | Permisos |
|---|---|
| **Administrador** | Configuración total, productos, usuarios, tasas. |
| **Analista de crédito** | Evaluar y aprobar/rechazar solicitudes. |
| **Operador** | Seguir desembolsos digitales, validar pagos pendientes de confirmación y conciliar. |
| **Gestor de cobranza** | Cobranza remota: ver mora, registrar acuerdos, llamadas y mensajes. |
| **Auditor** | Solo lectura de todo, incluida la bitácora. |
| **Cliente** | Ver su(s) préstamo(s), estado de cuenta, pagos. |

---

## 5. Modelo de datos (borrador)

```
Customer        (id, type, name, doc_type, doc_number, email, phone, income, kyc_status, created_at)
LoanProduct     (id, name, rate_type, rate_value, term, frequency, method, fee_config, mora_config, ...)
LoanApplication (id, customer_id, product_id, amount, term, status, score, decision_by, decided_at)
Loan            (id, application_id, customer_id, product_id, principal, rate, term, frequency,
                 disbursement_date, first_due_date, status, total_interest, total_paid, ...)
Schedule        (id, loan_id, snapshot_version, generated_at)
Installment     (id, loan_id, number, due_date, principal, interest, fee, total_due,
                 paid_principal, paid_interest, paid_fee, late_fee, status, days_late)
Payment         (id, loan_id, amount, method, paid_at, reference, status)
PaymentAllocation (id, payment_id, installment_id, to_mora, to_interest, to_fee, to_principal)
Transaction     (id, loan_id, type, amount, direction, created_by, created_at)   -- libro mayor
AuditLog        (id, entity, entity_id, action, before, after, user_id, created_at)
CompanyBankAccount (id, bank_name, account_number_masked, cci, currency_id, is_disbursement, is_collection, active)
CustomerBankAccount(id, customer_id, bank_name, account_type, account_number_masked, cci, phone_wallet,
                    is_verified, is_primary, status)
Disbursement    (id, loan_id, company_bank_account_id, customer_bank_account_id, amount, channel, status,
                 operation_number, initiated_at, confirmed_at, failed_at)
PaymentMethod   (id, name, type, provider_code, webhook_enabled, active)
CollectionAction(id, loan_id, installment_id, action_type, note, next_action_at, created_by, created_at)
Notification    (id, customer_id, channel, template_code, payload, status, sent_at)
BankStatement   (id, company_bank_account_id, operation_number, value_date, amount, reconciliation_status,
                 reconciled_payment_id, reconciled_disbursement_id)        -- base de la conciliación
IntegrationEvent(id, provider, event_type, external_id, payload, status, processed_at)
```

---

## 6. Módulos del sistema

1. **Seguridad** — usuarios, roles, autenticación, bitácora.
2. **Clientes** — registro, KYC, historial.
3. **Productos** — parametrización de créditos.
4. **Originación** — solicitud, simulación, evaluación, aprobación.
5. **Desembolso y Cronograma** — generación del plan de pagos.
6. **Pagos** — registro, aplicación, reversos.
7. **Mora y Cobranza** — aging, penalidades, acciones.
8. **Tesorería digital** — cuentas bancarias propias, seguimiento de desembolsos, cuadre diario contra el extracto.
9. **Reportes** — cartera, mora, flujo de caja, estado de cuenta.
10. **Configuración** — tasas, feriados, parámetros globales.
11. **Portal del Cliente** — registro, login, solicitud de préstamo, seguimiento, pagos, estado de cuenta.
12. **Integraciones y Conciliación** — bancos, billeteras, webhooks, archivos, conciliación y reintentos.

### 6.1 Mapa funcional sugerido del panel administrativo

Tomando como referencia la app analizada (`prestamos.devstackcode.com`) y ampliándola para el alcance real del proyecto, el panel administrativo debería organizarse así:

1. **Tablero**
   - KPIs: cartera vigente/vencida, mora, recaudación esperada vs. real, desembolsos del día, pagos del día.
   - Widgets: cuentas por cobrar, próximos vencimientos, tareas de cobranza, saldos bancarios y desembolsos en tránsito, alertas de integración.
   - Exportación a PDF / Excel.
2. **Clientes**
   - Lista de clientes, detalle, KYC, documentos, historial, múltiples préstamos activos.
3. **Solicitudes / Originación**
   - Nueva solicitud, simulación, evaluación, decisión, observaciones, condiciones.
4. **Préstamos**
   - Desembolso, cronograma, detalle de cuotas, contrato, prepago, reestructuración.
5. **Cobros**
   - Registro de pagos, aplicación automática, reversos, pagos parciales, sobrepagos, saldo a favor.
6. **Cuentas por cobrar**
   - Próximos pagos, cuotas vencidas, aging, balance pendiente por cliente y por préstamo.
7. **Cobranza**
   - Bandeja por aging, acuerdos, llamadas, promesas de pago, refinanciación, castigo.
8. **Tesorería digital**
   - Cuentas bancarias propias, bandeja de desembolsos (pendientes, en proceso, fallidos), cuadre diario contra el extracto.
9. **Reportes**
   - Cartera, aging, flujo de caja, productividad, auditoría, conciliación.
10. **Administración**
   - Usuarios, roles, monedas, canales digitales de pago y desembolso, tipos de documentos, mensajes legales, empresa.
11. **Configuración de negocio**
   - Productos de crédito, tasas, comisiones, mora, feriados, parámetros globales.
12. **Integraciones**
   - Bancos, billeteras, credenciales técnicas, webhooks, colas fallidas, conciliación.

### 6.2 Portal del cliente (separado del backoffice)

El cliente **no** debe operar dentro del panel administrativo. Debe existir un portal separado, conectado al mismo backend, con al menos:

- Registro / login.
- Perfil y validación KYC.
- Solicitud de préstamo.
- Seguimiento del estado de solicitud.
- Aceptación de condiciones / contrato.
- Consulta de cronograma y estado de cuenta.
- Pago de cuotas.
- Historial de pagos y constancias.
- Notificaciones y recordatorios.

---

## 7. Contratos de API (borrador)

```
POST   /api/customers
GET    /api/customers/{id}
POST   /api/loan-products
GET    /api/loan-products
POST   /api/loan-applications
POST   /api/loan-applications/{id}/simulate        → cronograma simulado (sin guardar)
POST   /api/loan-applications/{id}/decision        → aprobar / rechazar
POST   /api/loans                                  → desembolso (genera cronograma)
GET    /api/loans/{id}
GET    /api/loans/{id}/schedule
POST   /api/loans/{id}/payments                    → registrar pago
POST   /api/payments/{id}/reverse                  → reverso con contra-asiento
POST   /api/loans/{id}/quote-prepayment            → liquidación por cancelación anticipada
POST   /api/loans/{id}/restructure
GET    /api/collections/aging
POST   /api/collections/actions
GET    /api/treasury/bank-accounts
GET    /api/treasury/disbursements                 → bandeja de desembolsos (PENDING / PROCESSING / FAILED)
GET    /api/disbursements/{id}
GET    /api/treasury/daily-balance                 → cuadre diario por cuenta vs. extracto
GET    /api/reconciliation
POST   /api/reconciliation/import
POST   /api/integrations/webhooks/{provider}
GET    /api/reports/portfolio
GET    /api/reports/aging
GET    /api/reports/cashflow
GET    /api/reports/account-statement/{loanId}

POST   /api/auth/login
POST   /api/auth/logout
GET    /api/me

POST   /api/client/register
POST   /api/client/login
GET    /api/client/loans
GET    /api/client/loans/{id}/schedule
POST   /api/client/loan-applications
POST   /api/client/payments
```

**Idempotencia:** los endpoints de pago/desembolso deben aceptar una `Idempotency-Key`.

**Observación:** si se integra con bancos / billeteras (`BCP`, `Yape`, `Plin`, `BBVA`, `Scotiabank`, etc.), cada integración debe soportar:
- autenticación segura,
- reintentos,
- trazabilidad del `external_reference`,
- conciliación,
- manejo de webhooks duplicados,
- registro de errores y cola de reproceso.

---

## 8. Requisitos no funcionales

- **Precisión monetaria:** usar `decimal`/`numeric` (nunca `float`) y definir política de redondeo (half-up, 2 decimales).
- **Auditoría:** toda transacción financiera inmutable y trazable.
- **Concurrencia:** bloqueo optimista/pesimista por préstamo al registrar pagos (evitar doble aplicación).
- **Zona horaria y feriados** configurables.
- **Notificaciones** desacopladas (cola de mensajes).
- **Integraciones externas:** idempotencia, firma de webhooks, timeout, reintentos exponenciales y trazabilidad extremo a extremo.
- **Observabilidad:** logs estructurados, métricas, tracing y alertas.
- **Seguridad:** cifrado de datos sensibles, gestión de secretos, RBAC estricto y registro de accesos.
- **Escalabilidad:** partir de un monolito modular o modular monolith; extraer microservicios solo cuando la carga o integraciones lo justifiquen.
- **Conciliación:** proceso manual + automático para validar pagos externos contra pagos aplicados internamente.

---

## 9. Stack sugerido (a validar)

- **Backend (opción enterprise recomendada):** `Java + Spring Boot` o `.NET + ASP.NET Core`.
- **Backend (si se prioriza velocidad de salida):** Laravel / NestJS / Django con capa de dominio bien separada.
- **BD:** PostgreSQL (transacciones, `numeric`, `jsonb` para snapshots).
- **Frontend:** React + TypeScript para panel administrativo y portal cliente web.
- **Jobs / procesamiento asíncrono:** scheduler + colas para mora, estados, notificaciones e integraciones.
- **Cache / colas:** Redis.
- **Mensajería/eventos:** RabbitMQ o Kafka si el volumen e integraciones lo requieren.
- **Infraestructura:** Docker, CI/CD, observabilidad y gestión de secretos.

> Recomendación arquitectónica: para este dominio, conviene iniciar con un **monolito modular** bien separado por dominios (`clientes`, `originacion`, `prestamos`, `pagos`, `cobranza`, `tesoreria`, `integraciones`, `auditoria`). Si más adelante el volumen o las integraciones lo exigen, extraer servicios alrededor del core.

> La app de referencia (`prestamos.devstackcode.com`) es útil como inspiración para el **panel administrativo**, pero no cubre por sí sola el nivel de trazabilidad, conciliación, productos configurables y flujos enterprise que este sistema requiere.

---

## 10. Roadmap por fases

| Fase | Entregable |
|---|---|
| **0. Descubrimiento** | Definir productos, tasas, métodos y reglas reales del negocio. |
| **1. Núcleo financiero** | Motor de cálculo (francés/flat/alemán) + generador de cronograma con tests. |
| **2. Clientes y originación** | KYC, solicitudes, evaluación, aprobación. |
| **3. Desembolso y pagos digitales** | Desembolso por transferencia, al menos un canal de cobro con webhook, aplicación de pagos, reversos y conciliación básica con el extracto. |
| **4. Tesorería digital** | Cuentas bancarias propias, bandeja de desembolsos, cuadre diario por cuenta. |
| **5. Mora y cobranza** | Aging, penalidades, acuerdos, notificaciones. |
| **6. Portal del cliente** | Solicitud, seguimiento, consulta de cronograma y pagos. |
| **7. Reportes y auditoría** | Cartera, mora, estado de cuenta, bitácora. |
| **8. Integraciones adicionales** | Más bancos y billeteras, débito automático, conciliación avanzada (liquidaciones en lote de pasarelas). |
| **9. Refinanciamiento** | Reestructuración, prepago, consolidación. |

---

## 11. KPIs a medir

- Cartera vigente / vencida; **índice de mora** (%).
- Aging por rangos (1–30, 31–60, 61–90, >90).
- Tasa de aprobación y tiempo de originación.
- **Recaudación esperada vs. real**.
- Duración media del crédito, tasa de cancelación anticipada.

---

## 12. Próximos pasos

1. Validar con negocio: métodos de interés, tasas, frecuencias y política de mora.
2. Definir el motor de cálculo y escribir **tests unitarios** con casos conocidos (validar contra Excel).
3. Diseñar el esquema de BD definitivo.
4. Diseñar el mapa funcional de interfaces: **panel administrativo** + **portal cliente**.
5. Prototipar simulación de crédito (entrada: monto, tasa, plazo → cronograma).
6. Definir estrategia de pagos externos e integraciones (API, webhook, archivo, conciliación).
7. Iterar según el roadmap.

> **Regla de oro:** el dinero se calcula una sola vez (al generar el cronograma) y se **congela**; los pagos solo lo consumen, nunca lo recalculan hacia atrás.
