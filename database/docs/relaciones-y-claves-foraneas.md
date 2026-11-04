# Matriz de Relaciones y Claves Foráneas (Foreign Keys)

Este documento detalla todas las relaciones de integridad referencial del sistema, su cardinalidad, sus reglas de borrado/actualización y la justificación de negocio detrás de cada enlace.

---

## 1. Matriz Completa de Claves Foráneas

| # | Tabla Origen | Columna FK | Tabla Destino | Columna PK | Cardinalidad | Regla ON DELETE | Justificación de Negocio |
|---|---|---|---|---|:---:|---|---|
| 1 | `company` | `default_currency_id` | `currencies` | `id` | N : 1 | `RESTRICT` | La empresa debe tener una moneda funcional por defecto (US-39). |
| 2 | `branches` | *(N/A)* | — | — | — | — | Entidad independiente raíz para sucursales físicas (US-40). |
| 3 | `zones` | `branch_id` | `branches` | `id` | N : 1 | `RESTRICT` | Una zona de cobranza depende operativamente de una sucursal (US-40). |
| 4 | `holidays` | `branch_id` | `branches` | `id` | N : 1 | `RESTRICT` | Un feriado local aplica a una sucursal; si es NULL aplica a nivel nacional (US-41). |
| 5 | `legal_templates` | `created_by` | `users` | `id` | N : 1 | `RESTRICT` | Trazabilidad del usuario que redactó la versión contractual (US-42). |
| 6 | `users` | `branch_id` | `branches` | `id` | N : 1 | `RESTRICT` | Un usuario de ventanilla o analista opera desde una sucursal (US-43). |
| 7 | `refresh_tokens` | `user_id` | `users` | `id` | N : 1 | `CASCADE` | Si se elimina el usuario, se invalidan de inmediato sus sesiones activas (US-01). |
| 8 | `user_zones` | `user_id` | `users` | `id` | N : M | `CASCADE` | Asociación de gestores con sus zonas de trabajo (US-40). |
| 9 | `user_zones` | `zone_id` | `zones` | `id` | N : M | `CASCADE` | Si se remueve una zona, se desvinculan las asignaciones (US-40). |
| 10 | `audit_logs` | `user_id` | `users` | `id` | N : 1 | `RESTRICT` | Auditoría de qué colaborador ejecutó la mutación financiera (US-03). |
| 11 | `customers` | `kyc_verified_by` | `users` | `id` | N : 1 | `RESTRICT` | Identificación del analista que emitió el dictamen KYC (US-05). |
| 12 | `customers` | `portal_user_id` | `users` | `id` | 1 : 1 | `RESTRICT` | Enlace opcional de la ficha con la cuenta web de autoservicio (US-26). |
| 13 | `customers` | `created_by` | `users` | `id` | N : 1 | `RESTRICT` | Colaborador que dio de alta la ficha del cliente (US-04). |
| 14 | `customer_documents` | `customer_id` | `customers` | `id` | N : 1 | `CASCADE` | Si se suprime el cliente, se remueven sus referencias documentales (US-05). |
| 15 | `customer_documents` | `uploaded_by` | `users` | `id` | N : 1 | `RESTRICT` | Trazabilidad de quién cargó el archivo al repositorio (US-05). |
| 16 | `otp_verifications` | `customer_id` | `customers` | `id` | N : 1 | `CASCADE` | Códigos OTP asociados al cliente durante su onboarding (US-26). |
| 17 | `loan_products` | `currency_id` | `currencies` | `id` | N : 1 | `RESTRICT` | Todo producto de crédito opera en una divisa fija pactada (US-07). |
| 18 | `loan_products` | `created_by` | `users` | `id` | N : 1 | `RESTRICT` | Usuario administrativo que creó o versionó el producto (US-07). |
| 19 | `loan_applications` | `customer_id` | `customers` | `id` | N : 1 | `RESTRICT` | Un cliente puede registrar múltiples solicitudes a lo largo del tiempo (US-10). |
| 20 | `loan_applications` | `product_id` | `loan_products` | `id` | N : 1 | `RESTRICT` | La solicitud toma como base las reglas del producto elegido (US-10). |
| 21 | `loan_applications` | `decision_by` | `users` | `id` | N : 1 | `RESTRICT` | Analista o comité que aprobó o rechazó el expediente (US-12). |
| 22 | `loan_applications` | `created_by` | `users` | `id` | N : 1 | `RESTRICT` | Usuario que capturó la solicitud en el sistema (US-10). |
| 23 | `loans` | `application_id` | `loan_applications` | `id` | 1 : 1 | `RESTRICT` | Una solicitud aprobada se convierte en exactamente un préstamo (US-13). |
| 24 | `loans` | `customer_id` | `customers` | `id` | N : 1 | `RESTRICT` | Un cliente puede tener varios préstamos activos concurrentes (US-06, RF-06.2). |
| 25 | `loans` | `product_id` | `loan_products` | `id` | N : 1 | `RESTRICT` | Referencia al catálogo de origen para reportes y clasificación (US-07). |
| 26 | `loans` | `refinanced_from_loan_id` | `loans` | `id` | N : 1 | `RESTRICT` | **Recursiva:** Préstamo que fue cancelado por este nuevo crédito (US-32). |
| 27 | `loans` | `cashbox_id` | `cash_boxes` | `id` | N : 1 | `RESTRICT` | Caja física desde donde se entregaron los fondos en efectivo (US-13). |
| 28 | `loans` | `disbursed_by` | `users` | `id` | N : 1 | `RESTRICT` | Cajero u operador que procesó la entrega material de dinero (US-13). |
| 29 | `schedules` | `loan_id` | `loans` | `id` | N : 1 | `RESTRICT` | Versiones del cronograma (v1 original, v2+ reestructuradas) (US-14, US-31). |
| 30 | `installments` | `schedule_id` | `schedules` | `id` | N : 1 | `CASCADE` | La cuota pertenece a una versión específica de cronograma (US-14). |
| 31 | `installments` | `loan_id` | `loans` | `id` | N : 1 | `RESTRICT` | Denormalización controlada para optimizar consultas directas de cuotas (US-14). |
| 32 | `prepayment_quotes` | `loan_id` | `loans` | `id` | N : 1 | `RESTRICT` | Cotización de liquidación anticipada emitida para ese préstamo (US-30). |
| 33 | `prepayment_quotes` | `generated_by` | `users` | `id` | N : 1 | `RESTRICT` | Operador que emitió la cotización de cancelación (US-30). |
| 34 | `payments` | `loan_id` | `loans` | `id` | N : 1 | `RESTRICT` | Préstamo sobre el cual se aplica el abono (US-16). |
| 35 | `payments` | `payment_method_id` | `payment_methods` | `id` | N : 1 | `RESTRICT` | Medio de cobro utilizado (efectivo, transferencia, Yape, etc.) (US-16). |
| 36 | `payments` | `reversal_of_id` | `payments` | `id` | 1 : 1 | `RESTRICT` | **Recursiva:** Apunta al pago que este registro anula vía contra-asiento (US-18). |
| 37 | `payments` | `cashbox_id` | `cash_boxes` | `id` | N : 1 | `RESTRICT` | Caja receptora del efectivo (si el abono fue presencial) (US-16). |
| 38 | `payments` | `registered_by` | `users` | `id` | N : 1 | `RESTRICT` | Cajero que ingresó el cobro o proceso automático (US-16). |
| 39 | `payment_allocations` | `payment_id` | `payments` | `id` | N : 1 | `CASCADE` | Desglose de distribución de cada centavo del pago (US-16). |
| 40 | `payment_allocations` | `installment_id` | `installments` | `id` | N : 1 | `RESTRICT` | Cuota que recibió amortización a mora, interés o capital (US-16). |
| 41 | `transactions` | `loan_id` | `loans` | `id` | N : 1 | `RESTRICT` | Asiento contable vinculado a una operación crediticia (US-13, US-16). |
| 42 | `transactions` | `payment_id` | `payments` | `id` | N : 1 | `RESTRICT` | Asiento generado por un cobro o reverso de pago (US-16, US-18). |
| 43 | `transactions` | `cashbox_id` | `cash_boxes` | `id` | N : 1 | `RESTRICT` | Registro del impacto en la caja física (US-24). |
| 44 | `transactions` | `currency_id` | `currencies` | `id` | N : 1 | `RESTRICT` | Denominación monetaria del asiento (US-13). |
| 45 | `transactions` | `created_by` | `users` | `id` | N : 1 | `RESTRICT` | Usuario responsable del evento contable (US-03). |
| 46 | `cash_boxes` | `branch_id` | `branches` | `id` | N : 1 | `RESTRICT` | Sucursal física donde radica la caja (US-23). |
| 47 | `cash_boxes` | `currency_id` | `currencies` | `id` | N : 1 | `RESTRICT` | Divisa de operación de la caja (US-23). |
| 48 | `cash_boxes` | `responsible_user_id`| `users` | `id` | N : 1 | `RESTRICT` | Cajero responsable asignado a la ventanilla (US-23). |
| 49 | `cash_sessions` | `cashbox_id` | `cash_boxes` | `id` | N : 1 | `RESTRICT` | Caja operada durante la jornada (US-23). |
| 50 | `cash_sessions` | `opened_by` | `users` | `id` | N : 1 | `RESTRICT` | Cajero que dio apertura con saldo inicial (US-23). |
| 51 | `cash_sessions` | `closed_by` | `users` | `id` | N : 1 | `RESTRICT` | Cajero o supervisor que ejecutó el arqueo de cierre (US-23). |
| 52 | `cash_movements` | `cashbox_id` | `cash_boxes` | `id` | N : 1 | `RESTRICT` | Caja donde se produjo el ingreso o egreso misceláneo (US-24). |
| 53 | `cash_movements` | `session_id` | `cash_sessions` | `id` | N : 1 | `RESTRICT` | Sesión operativa en la que ocurrió el movimiento (US-24). |
| 54 | `cash_movements` | `created_by` | `users` | `id` | N : 1 | `RESTRICT` | Cajero que sustentó el comprobante del gasto o ingreso (US-24). |
| 55 | `cash_transfers` | `source_cashbox_id` | `cash_boxes` | `id` | N : 1 | `RESTRICT` | Caja remitente de los fondos (US-25). |
| 56 | `cash_transfers` | `destination_cashbox_id` | `cash_boxes` | `id` | N : 1 | `RESTRICT` | Caja receptora de los fondos (US-25). |
| 57 | `cash_transfers` | `initiated_by` | `users` | `id` | N : 1 | `RESTRICT` | Colaborador que envió la remesa (US-25). |
| 58 | `cash_transfers` | `accepted_by` | `users` | `id` | N : 1 | `RESTRICT` | Colaborador que recibió y aceptó el efectivo (US-25). |
| 59 | `bank_accounts` | `currency_id` | `currencies` | `id` | N : 1 | `RESTRICT` | Moneda de la cuenta corriente o de ahorros bancaria (US-37). |
| 60 | `collection_actions` | `loan_id` | `loans` | `id` | N : 1 | `RESTRICT` | Crédito sujeto a gestión de cobro prejudicial (US-21). |
| 61 | `collection_actions` | `installment_id` | `installments` | `id` | N : 1 | `RESTRICT` | Cuota vencida puntual objeto de la llamada o visita (US-21). |
| 62 | `collection_actions` | `created_by` | `users` | `id` | N : 1 | `RESTRICT` | Gestor de cobranza que efectuó la acción (US-21). |
| 63 | `reconciliations` | `bank_account_id` | `bank_accounts` | `id` | N : 1 | `RESTRICT` | Cuenta bancaria de donde procede el extracto (US-37). |
| 64 | `reconciliations` | `loan_id` | `loans` | `id` | N : 1 | `RESTRICT` | Préstamo asociado tras el matching (US-37). |
| 65 | `reconciliations` | `payment_id` | `payments` | `id` | N : 1 | `RESTRICT` | Pago registrado internamente que concilia con el banco (US-37). |
| 66 | `reconciliations` | `reconciled_by` | `users` | `id` | N : 1 | `RESTRICT` | Analista contable que validó la conciliación (US-37). |
| 67 | `notifications` | `customer_id` | `customers` | `id` | N : 1 | `CASCADE` | Cliente destinatario de la alerta (US-19, US-21). |

---

## 2. Dependencias Cruzadas Resueltas (Claves Foráneas Diferidas)

Durante la ejecución secuencial de migraciones, algunas entidades presentan dependencias mutuas naturales del negocio. En el diseño se aplicaron `ALTER TABLE` controlados para asegurar idempotencia y carga sin errores:

1. **`company.default_currency_id` $\to$ `currencies.id`:**  
   Se resuelve inmediatamente tras crear `currencies`, evitando dependencias cíclicas con tablas de configuración.
2. **`legal_templates.created_by` $\to$ `users.id`:**  
   La tabla `legal_templates` se crea en la migración `01_admin_config.sql`, y la restricción FK se agrega en `02_security_users.sql` una vez que la tabla `users` ha sido creada.
3. **`loans.cashbox_id` $\to$ `cash_boxes.id`:**  
   El desembolso puede referenciar una caja física de entrega de efectivo. La FK se formaliza en `08_treasury_cashbox.sql`.
4. **`payments.cashbox_id` $\to$ `cash_boxes.id`:**  
   Los pagos presenciales impactan la caja del cajero. La FK se formaliza en `08_treasury_cashbox.sql`.
5. **`transactions.cashbox_id` $\to$ `cash_boxes.id`:**  
   Los movimientos contables de ventanilla referencian la caja en `08_treasury_cashbox.sql`.

---

## 3. Relaciones Especiales y Recursivas

### 3.1 Refinanciamiento y Reestructuración de Créditos (`loans.refinanced_from_loan_id`)
Permite modelar consolidación de deudas (US-32):
- Un nuevo préstamo `Loan C` puede tener en `refinanced_from_loan_id` el identificador de `Loan A`.
- En la misma transacción atómica, `Loan A` pasa a estado `PAGADO` con motivo `REFINANCIAMIENTO` y se crea el nuevo cronograma para `Loan C`.

### 3.2 Contra-asientos de Pago (`payments.reversal_of_id`)
Garantiza el principio contable de inmutabilidad (US-18, RF-18.1):
- Los registros en `payments` **nunca se borran ni editan**.
- Para anular un cobro erróneo o cheque devuelto, se inserta una nueva fila con:
  - `payment_kind = 'REVERSAL'`
  - `reversal_of_id = ID_PAGO_ORIGINAL`
  - `reversal_reason = 'Motivo justificado'`
  - Un asiento espejo en `transactions` con `direction = 'OUTFLOW'`.

### 3.3 Versiones de Cronograma (`loans` $\leftrightarrow$ `schedules` $\leftrightarrow$ `installments`)
- Relación **1 a N** entre `loans` y `schedules`.
- Cada préstamo nace con `snapshot_version = 1`.
- Si el crédito se reestructura (US-31), se inserta `schedules` con `snapshot_version = 2`.
- El trigger `fn_enforce_single_active_schedule()` garantiza que solo exista un cronograma con `is_active = TRUE` a la vez por préstamo.
- Las cuotas `installments` están atadas tanto a `schedule_id` (para aislamiento de versión) como a `loan_id` (para indexación y búsquedas analíticas de alta velocidad).
