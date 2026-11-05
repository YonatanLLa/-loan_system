# Matriz de Relaciones y Claves Foráneas (Foreign Keys)

> **Modelo:** Fintech 100% Digital — Sin sucursales, sin cajas de efectivo, sin zonas de cobranza física.

Este documento detalla todas las relaciones de integridad referencial del sistema, su cardinalidad, sus reglas de borrado/actualización y la justificación de negocio detrás de cada enlace.

---

## 1. Matriz Completa de Claves Foráneas

| # | Tabla Origen | Columna FK | Tabla Destino | Columna PK | Cardinalidad | Regla ON DELETE | Justificación de Negocio |
|---|---|---|---|---|:---:|---|---|
| 1 | `company` | `default_currency_id` | `currencies` | `id` | N : 1 | `RESTRICT` | La Fintech debe tener una moneda funcional por defecto (US-39). |
| 2 | `company_bank_accounts` | `currency_id` | `currencies` | `id` | N : 1 | `RESTRICT` | Cada cuenta bancaria de la Fintech opera en una divisa fija (US-37). |
| 3 | `legal_templates` | `created_by` | `users` | `id` | N : 1 | `RESTRICT` | Trazabilidad del usuario que redactó la versión contractual (US-42). |
| 4 | `refresh_tokens` | `user_id` | `users` | `id` | N : 1 | `CASCADE` | Si se elimina el usuario, se invalidan sus sesiones activas (US-01). |
| 5 | `audit_logs` | `user_id` | `users` | `id` | N : 1 | `RESTRICT` | Auditoría de qué colaborador ejecutó la mutación financiera (US-03). |
| 6 | `customers` | `kyc_verified_by` | `users` | `id` | N : 1 | `RESTRICT` | Analista que emitió el dictamen KYC digital (US-05). |
| 7 | `customers` | `portal_user_id` | `users` | `id` | 1 : 1 | `RESTRICT` | Enlace con la cuenta web de autoservicio del cliente (US-26). |
| 8 | `customers` | `created_by` | `users` | `id` | N : 1 | `RESTRICT` | Colaborador que registró la ficha del cliente (US-04). |
| 9 | `customer_bank_accounts` | `customer_id` | `customers` | `id` | N : 1 | `CASCADE` | Una cuenta bancaria pertenece a un cliente. Si se elimina el cliente, se remueven sus cuentas (US-04). |
| 10 | `customer_bank_accounts` | `verified_by` | `users` | `id` | N : 1 | `RESTRICT` | Analista que verificó la titularidad de la cuenta (RF-04.3). |
| 11 | `customer_documents` | `customer_id` | `customers` | `id` | N : 1 | `CASCADE` | Si se suprime el cliente, se remueven sus referencias documentales (US-05). |
| 12 | `customer_documents` | `uploaded_by` | `users` | `id` | N : 1 | `RESTRICT` | Trazabilidad de quién cargó el archivo al repositorio (US-05). |
| 13 | `otp_verifications` | `customer_id` | `customers` | `id` | N : 1 | `CASCADE` | Códigos OTP asociados al cliente durante su onboarding (US-26). |
| 14 | `loan_products` | `currency_id` | `currencies` | `id` | N : 1 | `RESTRICT` | Todo producto de crédito opera en una divisa fija pactada (US-07). |
| 15 | `loan_products` | `created_by` | `users` | `id` | N : 1 | `RESTRICT` | Usuario administrativo que creó o versionó el producto (US-07). |
| 16 | `loan_applications` | `customer_id` | `customers` | `id` | N : 1 | `RESTRICT` | Un cliente puede registrar múltiples solicitudes (US-10). |
| 17 | `loan_applications` | `product_id` | `loan_products` | `id` | N : 1 | `RESTRICT` | La solicitud toma como base las reglas del producto elegido (US-10). |
| 18 | `loan_applications` | `decision_by` | `users` | `id` | N : 1 | `RESTRICT` | Analista o comité que aprobó o rechazó el expediente (US-12). |
| 19 | `loan_applications` | `created_by` | `users` | `id` | N : 1 | `RESTRICT` | Usuario que capturó la solicitud en el sistema (US-10). |
| 20 | `loans` | `application_id` | `loan_applications` | `id` | 1 : 1 | `RESTRICT` | Una solicitud aprobada se convierte en exactamente un préstamo (US-13). |
| 21 | `loans` | `customer_id` | `customers` | `id` | N : 1 | `RESTRICT` | Un cliente puede tener varios préstamos activos concurrentes (US-06). |
| 22 | `loans` | `product_id` | `loan_products` | `id` | N : 1 | `RESTRICT` | Referencia al catálogo de origen para reportes y clasificación (US-07). |
| 23 | `loans` | `refinanced_from_loan_id` | `loans` | `id` | N : 1 | `RESTRICT` | **Recursiva:** Préstamo que fue cancelado por este nuevo crédito (US-32). |
| 24 | `loans` | `disbursement_bank_account_id` | `company_bank_accounts` | `id` | N : 1 | `RESTRICT` | Cuenta de la Fintech desde donde se realizó la transferencia al cliente (US-13). |
| 25 | `loans` | `customer_bank_account_id` | `customer_bank_accounts` | `id` | N : 1 | `RESTRICT` | Cuenta o tarjeta del cliente que recibió el depósito digital (US-13). |
| 26 | `loans` | `disbursed_by` | `users` | `id` | N : 1 | `RESTRICT` | Operador que procesó la aprobación de la transferencia digital (US-13). |
| 27 | `disbursements` | `loan_id` | `loans` | `id` | 1 : 1 | `RESTRICT` | Registro único del estado de la transferencia bancaria (US-13). |
| 28 | `disbursements` | `company_bank_account_id` | `company_bank_accounts` | `id` | N : 1 | `RESTRICT` | Cuenta de la Fintech de origen del desembolso (US-13). |
| 29 | `disbursements` | `customer_bank_account_id` | `customer_bank_accounts` | `id` | N : 1 | `RESTRICT` | Cuenta del cliente que recibió la transferencia (US-13). |
| 30 | `disbursements` | `currency_id` | `currencies` | `id` | N : 1 | `RESTRICT` | Moneda de la transferencia bancaria (US-13). |
| 31 | `disbursements` | `initiated_by` | `users` | `id` | N : 1 | `RESTRICT` | Operador que inició el proceso de desembolso (US-13). |
| 32 | `schedules` | `loan_id` | `loans` | `id` | N : 1 | `RESTRICT` | Versiones del cronograma (v1 original, v2+ reestructuradas) (US-14, US-31). |
| 33 | `installments` | `schedule_id` | `schedules` | `id` | N : 1 | `CASCADE` | La cuota pertenece a una versión específica de cronograma (US-14). |
| 34 | `installments` | `loan_id` | `loans` | `id` | N : 1 | `RESTRICT` | Denormalización controlada para optimizar consultas directas de cuotas (US-14). |
| 35 | `prepayment_quotes` | `loan_id` | `loans` | `id` | N : 1 | `RESTRICT` | Cotización de liquidación anticipada emitida para ese préstamo (US-30). |
| 36 | `prepayment_quotes` | `generated_by` | `users` | `id` | N : 1 | `RESTRICT` | Operador que emitió la cotización de cancelación (US-30). |
| 37 | `payments` | `loan_id` | `loans` | `id` | N : 1 | `RESTRICT` | Préstamo sobre el cual se aplica el abono digital (US-16). |
| 38 | `payments` | `payment_method_id` | `payment_methods` | `id` | N : 1 | `RESTRICT` | Canal digital de cobro utilizado (Yape, Transferencia, etc.) (US-16). |
| 39 | `payments` | `customer_bank_account_id` | `customer_bank_accounts` | `id` | N : 1 | `RESTRICT` | Cuenta del cliente desde donde se originó el pago digital (US-16). |
| 40 | `payments` | `company_bank_account_id` | `company_bank_accounts` | `id` | N : 1 | `RESTRICT` | Cuenta de la Fintech donde se acreditó el pago (US-16). |
| 41 | `payments` | `reversal_of_id` | `payments` | `id` | 1 : 1 | `RESTRICT` | **Recursiva:** Apunta al pago que este registro anula vía contra-asiento (US-18). |
| 42 | `payments` | `registered_by` | `users` | `id` | N : 1 | `RESTRICT` | Operador o proceso automático que registró el cobro (US-16). |
| 43 | `payment_allocations` | `payment_id` | `payments` | `id` | N : 1 | `CASCADE` | Desglose de distribución de cada centavo del pago (US-16). |
| 44 | `payment_allocations` | `installment_id` | `installments` | `id` | N : 1 | `RESTRICT` | Cuota que recibió amortización a mora, interés o capital (US-16). |
| 45 | `transactions` | `loan_id` | `loans` | `id` | N : 1 | `RESTRICT` | Asiento contable vinculado a una operación crediticia (US-13, US-16). |
| 46 | `transactions` | `payment_id` | `payments` | `id` | N : 1 | `RESTRICT` | Asiento generado por un cobro o reverso de pago (US-16, US-18). |
| 47 | `transactions` | `disbursement_id` | `disbursements` | `id` | N : 1 | `RESTRICT` | Asiento generado por un desembolso bancario (US-13). |
| 48 | `transactions` | `company_bank_account_id` | `company_bank_accounts` | `id` | N : 1 | `RESTRICT` | Cuenta de la Fintech involucrada en el movimiento (reemplaza cashbox_id). |
| 49 | `transactions` | `currency_id` | `currencies` | `id` | N : 1 | `RESTRICT` | Denominación monetaria del asiento (US-13). |
| 50 | `transactions` | `created_by` | `users` | `id` | N : 1 | `RESTRICT` | Usuario responsable del evento contable (US-03). |
| 51 | `bank_statements` | `company_bank_account_id` | `company_bank_accounts` | `id` | N : 1 | `RESTRICT` | Cuenta bancaria de la Fintech de donde proviene el extracto (US-37). |
| 52 | `bank_statements` | `currency_id` | `currencies` | `id` | N : 1 | `RESTRICT` | Moneda del movimiento en el extracto (US-37). |
| 53 | `bank_statements` | `reconciled_payment_id` | `payments` | `id` | N : 1 | `RESTRICT` | Pago interno emparejado automáticamente con el extracto (US-37). |
| 54 | `bank_statements` | `reconciled_disbursement_id` | `disbursements` | `id` | N : 1 | `RESTRICT` | Desembolso interno emparejado con el extracto (US-37). |
| 55 | `bank_statements` | `reconciled_by` | `users` | `id` | N : 1 | `RESTRICT` | Analista que validó manualmente la conciliación (US-37). |
| 56 | `bank_statements` | `imported_by` | `users` | `id` | N : 1 | `RESTRICT` | Operador que importó el extracto al sistema (US-37). |
| 57 | `collection_actions` | `loan_id` | `loans` | `id` | N : 1 | `RESTRICT` | Crédito sujeto a gestión de cobro digital (US-21). |
| 58 | `collection_actions` | `installment_id` | `installments` | `id` | N : 1 | `RESTRICT` | Cuota vencida objeto del contacto digital (US-21). |
| 59 | `collection_actions` | `created_by` | `users` | `id` | N : 1 | `RESTRICT` | Gestor de cobranza digital que efectuó la acción (US-21). |
| 60 | `notifications` | `customer_id` | `customers` | `id` | N : 1 | `CASCADE` | Cliente destinatario de la alerta digital (US-19, US-21). |
| 61 | `integration_events` | *(sin FK directa)* | — | — | — | — | Tabla de eventos crudos de webhooks bancarios. Se asocia luego con pagos/desembolsos por código de referencia (US-36). |

---

## 2. Dependencias Cruzadas Resueltas (Claves Foráneas Diferidas)

Durante la ejecución secuencial de migraciones, se aplicaron `ALTER TABLE` controlados para asegurar idempotencia y carga sin errores:

1. **`company.default_currency_id` → `currencies.id`:**
   Se resuelve inmediatamente tras crear `currencies`, evitando dependencias cíclicas.

2. **`legal_templates.created_by` → `users.id`:**
   La tabla `legal_templates` se crea en `01_admin_config.sql`, y la restricción FK se agrega en `02_security_users.sql` una vez que `users` ha sido creada.

> [!IMPORTANT] Las FK hacia `cash_boxes` que existían en `loans`, `payments` y `transactions` han sido **eliminadas completamente**. Ahora se referencian `company_bank_accounts` y `customer_bank_accounts` que modelan el flujo de fondos digital.

---

## 3. Relaciones Especiales y Recursivas

### 3.1 Refinanciamiento y Reestructuración de Créditos (`loans.refinanced_from_loan_id`)
Permite modelar consolidación de deudas (US-32):
- Un nuevo préstamo `Loan C` puede tener en `refinanced_from_loan_id` el identificador de `Loan A`.
- En la misma transacción atómica, `Loan A` pasa a estado `PAGADO` con motivo `REFINANCIAMIENTO` y se crea el nuevo cronograma para `Loan C`.

### 3.2 Contra-asientos de Pago (`payments.reversal_of_id`)
Garantiza el principio contable de inmutabilidad (US-18, RF-18.1):
- Los registros en `payments` **nunca se borran ni editan**.
- Para anular un cobro erróneo, se inserta una nueva fila con:
  - `payment_kind = 'REVERSAL'`
  - `reversal_of_id = ID_PAGO_ORIGINAL`
  - `reversal_reason = 'Motivo justificado'`
  - Un asiento espejo en `transactions` con `direction = 'OUTFLOW'`.

### 3.3 Versiones de Cronograma (`loans` ↔ `schedules` ↔ `installments`)
- Relación **1 a N** entre `loans` y `schedules`.
- Cada préstamo nace con `snapshot_version = 1`.
- Si el crédito se reestructura (US-31), se inserta `schedules` con `snapshot_version = 2`.
- El trigger `fn_enforce_single_active_schedule()` garantiza que solo exista un cronograma con `is_active = TRUE` a la vez por préstamo.
- Las cuotas `installments` están atadas tanto a `schedule_id` como a `loan_id` para indexación de alta velocidad.

### 3.4 Trazabilidad del Flujo de Fondos Digital
El flujo de dinero digital queda completamente trazado a través de tres capas:
1. **`disbursements`**: Estado en tiempo real de la transferencia bancaria (PENDING → COMPLETED).
2. **`transactions`**: Asiento contable inmutable en el libro mayor.
3. **`bank_statements`**: Confirmación del banco vía extracto o webhook para conciliación automática.
