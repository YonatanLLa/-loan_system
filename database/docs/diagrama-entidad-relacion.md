# Diagramas de Entidad - Relación (ERD)

> **Modelo:** Fintech 100% Digital — Sin sucursales físicas, sin cajas de efectivo, sin cobradores de campo.
> Todo el flujo de dinero ocurre por transferencias bancarias, Yape, Plin o pasarelas digitales.

Este documento detalla la topología de la base de datos a través de modelos conceptuales y físicos mediante diagramas Mermaid.

---

## 1. Diagrama General de la Arquitectura (Core del Sistema)

El siguiente diagrama presenta la interacción central entre los prestatarios, las cuentas bancarias digitales, los créditos formalizados, el cronograma de amortización, los abonos y la conciliación bancaria.

```mermaid
erDiagram
    company ||--o{ company_bank_accounts : "posee cuentas"

    users ||--o{ refresh_tokens : "posee"
    users ||--o{ audit_logs : "genera accion"
    users ||--o{ loan_applications : "evalua"
    users ||--o{ loans : "desembolsa"
    users ||--o{ payments : "registra"

    customers ||--o{ customer_documents : "adjunta expediente"
    customers ||--o{ otp_verifications : "solicita verificacion"
    customers ||--o{ loan_applications : "solicita credito"
    customers ||--o{ loans : "titular de"
    customers ||--o{ notifications : "recibe"
    customers ||--o{ customer_bank_accounts : "registra cuenta"

    currencies ||--o{ loan_products : "moneda de"
    currencies ||--o{ company_bank_accounts : "moneda de"
    currencies ||--o{ transactions : "moneda de"

    loan_products ||--o{ loan_applications : "parametriza"
    loan_products ||--o{ loans : "define terminos"

    loan_applications ||--o| loans : "se formaliza en"

    customer_bank_accounts ||--o{ loans : "destino desembolso"
    company_bank_accounts  ||--o{ loans : "origen desembolso"
    company_bank_accounts  ||--o{ disbursements : "emite transferencia"
    customer_bank_accounts ||--o{ disbursements : "recibe transferencia"

    loans ||--o{ schedules : "versiona"
    loans ||--o{ installments : "compromete"
    loans ||--o{ payments : "recibe abonos"
    loans ||--o{ transactions : "registra en ledger"
    loans ||--o{ collection_actions : "gestiona mora"
    loans ||--o{ prepayment_quotes : "cotiza liquidacion"
    loans ||--o| disbursements : "tiene desembolso"

    schedules ||--o{ installments : "contiene cuotas"

    payments ||--o{ payment_allocations : "desglosa prelacion"
    installments ||--o{ payment_allocations : "amortiza"
    payments ||--o{ transactions : "asienta en ledger"

    company_bank_accounts ||--o{ bank_statements : "extractos de"
    bank_statements ||--o| payments : "concilia pago"
    bank_statements ||--o| disbursements : "concilia desembolso"
```

---

## 2. Subsistemas Detallados

### 2.1 Subsistema de Seguridad y Usuarios (Fintech Digital)
Gestiona las credenciales de acceso, la autenticación por JWT rotativo y el registro de bitácora inmutable.
Sin sucursales ni zonas geográficas: operación centralizada 100% digital.

```mermaid
erDiagram
    users {
        UUID id PK
        varchar email UK
        varchar password_hash
        varchar full_name
        user_role role
        user_status_type status
        smallint failed_login_count
        timestamptz locked_until
        timestamptz last_login_at
    }
    refresh_tokens {
        UUID id PK
        UUID user_id FK
        varchar token_hash UK
        timestamptz expires_at
        timestamptz revoked_at
        inet user_ip
        text user_agent
    }
    audit_logs {
        bigserial id PK
        varchar entity_name
        varchar entity_id
        varchar action
        jsonb state_before
        jsonb state_after
        UUID user_id FK
        inet user_ip
        timestamptz created_at
    }

    users ||--o{ refresh_tokens : "emite sesion por dispositivo"
    users ||--o{ audit_logs : "genera huella de auditoria"
```

---

### 2.2 Subsistema de Clientes, KYC y Cuentas Bancarias
Administra el perfil integral del prestatario, el expediente documental y las cuentas bancarias/tarjetas a las que se deposita el dinero.

```mermaid
erDiagram
    users ||--o| customers : "vincula cuenta portal"

    customers {
        UUID id PK
        person_type person_type
        document_type doc_type
        varchar doc_number UK
        varchar full_name
        varchar email
        varchar phone_primary
        numeric monthly_net_income
        kyc_status_type kyc_status
        numeric credit_balance
        UUID portal_user_id FK
        timestamptz consent_given_at
    }
    customer_bank_accounts {
        UUID id PK
        UUID customer_id FK
        varchar bank_name
        bank_account_type account_type
        varchar account_number_masked
        varchar cci
        varchar phone_wallet
        boolean is_verified
        boolean is_primary
        bank_account_status status
    }
    customer_documents {
        UUID id PK
        UUID customer_id FK
        varchar doc_category
        varchar file_hash
        text storage_uri
        UUID uploaded_by FK
    }
    otp_verifications {
        UUID id PK
        UUID customer_id FK
        varchar target
        notification_channel channel
        varchar otp_hash
        timestamptz expires_at
        timestamptz verified_at
    }
    notifications {
        UUID id PK
        UUID customer_id FK
        notification_channel channel
        varchar template_code
        jsonb payload
        notification_status status
    }

    customers ||--o{ customer_bank_accounts : "registra cuenta o tarjeta"
    customers ||--o{ customer_documents : "expediente digital KYC"
    customers ||--o{ otp_verifications : "codigos OTP"
    customers ||--o{ notifications : "envio de alertas"
```

---

### 2.3 Subsistema de Originación, Préstamos y Cronogramas (Core Financiero)
Gestiona el ciclo de vida de la solicitud, el desembolso digital con idempotencia y el cronograma congelado en snapshots.

```mermaid
erDiagram
    loan_products {
        UUID id PK
        varchar name
        UUID currency_id FK
        numeric min_amount
        numeric max_amount
        smallint min_term
        smallint max_term
        amortization_method amortization_method
        numeric annual_rate
        numeric mora_rate_per_day
        smallint grace_days
    }
    loan_applications {
        UUID id PK
        UUID customer_id FK
        UUID product_id FK
        numeric requested_amount
        numeric approved_amount
        smallint term
        payment_frequency frequency
        application_status status
        numeric dti_ratio
        credit_decision system_recommendation
        UUID decision_by FK
    }
    loans {
        UUID id PK
        UUID application_id FK
        UUID customer_id FK
        UUID product_id FK
        numeric principal
        numeric disbursed_amount
        numeric annual_rate
        amortization_method amortization_method
        payment_frequency frequency
        smallint term
        loan_status status
        date disbursement_date
        date first_due_date
        UUID disbursement_bank_account_id FK
        UUID customer_bank_account_id FK
        UUID disbursed_by FK
        varchar idempotency_key UK
    }
    disbursements {
        UUID id PK
        UUID loan_id FK
        UUID company_bank_account_id FK
        UUID customer_bank_account_id FK
        numeric amount
        disbursement_channel channel
        disbursement_status status
        varchar operation_number
        varchar external_reference
        timestamptz confirmed_at
    }
    schedules {
        UUID id PK
        UUID loan_id FK
        smallint snapshot_version
        timestamptz generated_at
        boolean is_active
    }
    installments {
        UUID id PK
        UUID schedule_id FK
        UUID loan_id FK
        smallint installment_number
        date due_date
        numeric principal
        numeric interest
        numeric fee
        numeric total_due
        numeric paid_principal
        numeric paid_interest
        numeric paid_fee
        numeric late_fee
        numeric paid_late_fee
        installment_status status
        smallint days_late
    }
    prepayment_quotes {
        UUID id PK
        UUID loan_id FK
        date as_of_date
        numeric outstanding_principal
        numeric accrued_interest
        numeric accrued_mora
        numeric total_to_pay
        date quote_valid_until
        boolean is_applied
    }

    loan_products ||--o{ loan_applications : "plantilla de"
    customers ||--o{ loan_applications : "solicita"
    loan_applications ||--o| loans : "formaliza a"
    customers ||--o{ loans : "deudor"
    loan_products ||--o{ loans : "condiciones"
    customer_bank_accounts ||--o{ loans : "cuenta destino del cliente"
    company_bank_accounts ||--o{ loans : "cuenta origen fintech"
    loans ||--o| disbursements : "tiene transferencia bancaria"
    loans ||--o{ schedules : "versiones del cronograma"
    schedules ||--o{ installments : "lista de cuotas"
    loans ||--o{ installments : "pertenece"
    loans ||--o{ prepayment_quotes : "cotiza prepago"
```

---

### 2.4 Subsistema de Pagos Digitales, Cascada de Prelación y Libro Mayor
Gobernado por la jerarquía estricta de imputación: Mora → Intereses → Comisiones → Capital.
Sin efectivo: todos los pagos llegan por transferencia, Yape, Plin o pasarela de pago con confirmación por webhook.

```mermaid
erDiagram
    loans ||--o{ payments : "recibe pago digital"
    payment_methods ||--o{ payments : "canal de abono"
    customer_bank_accounts ||--o| payments : "cuenta origen del cliente"
    company_bank_accounts ||--o| payments : "cuenta destino fintech"

    payments {
        UUID id PK
        UUID loan_id FK
        payment_kind payment_kind
        numeric amount
        UUID payment_method_id FK
        UUID customer_bank_account_id FK
        UUID company_bank_account_id FK
        varchar gateway_reference
        varchar external_reference
        timestamptz paid_at
        payment_status status
        UUID reversal_of_id FK
        text reversal_reason
        varchar receipt_number UK
        text receipt_url
        varchar idempotency_key UK
    }

    payment_allocations {
        UUID id PK
        UUID payment_id FK
        UUID installment_id FK
        numeric to_mora
        numeric to_interest
        numeric to_fee
        numeric to_principal
    }

    transactions {
        bigserial id PK
        UUID loan_id FK
        UUID payment_id FK
        UUID disbursement_id FK
        UUID company_bank_account_id FK
        transaction_type transaction_type
        transaction_dir direction
        numeric amount
        UUID currency_id FK
        varchar reference
    }

    payments ||--o{ payment_allocations : "imputa por prelacion"
    installments ||--o{ payment_allocations : "es amortizada"
    payments ||--o{ transactions : "movimiento en ledger"
    loans ||--o{ transactions : "desembolso o ajuste"
    payments ||--o| payments : "reverso de"
```

---

### 2.5 Subsistema de Tesorería Digital y Conciliación Bancaria
Reemplaza las cajas físicas y el arqueo ciego por extractos bancarios digitales y conciliación automática por referencia/webhook.

```mermaid
erDiagram
    company_bank_accounts {
        UUID id PK
        varchar bank_name
        varchar account_number_masked
        varchar cci
        UUID currency_id FK
        boolean is_disbursement
        boolean is_collection
        boolean is_active
    }

    bank_statements {
        UUID id PK
        UUID company_bank_account_id FK
        date statement_date
        varchar operation_number UK
        date value_date
        numeric amount
        UUID currency_id FK
        text description
        varchar external_reference
        reconciliation_status reconciliation_status
        UUID reconciled_payment_id FK
        UUID reconciled_disbursement_id FK
        timestamptz reconciled_at
        varchar import_source
    }

    company_bank_accounts ||--o{ bank_statements : "extractos importados"
    bank_statements ||--o| payments : "concilia con pago"
    bank_statements ||--o| disbursements : "concilia con desembolso"
```

---

### 2.6 Subsistema de Cobranza Digital e Integraciones
Motor de recuperación digital de cartera vencida: notificaciones, recordatorios y acuerdos de pago online.
Sin visitas domiciliarias ni cobradores de campo.

```mermaid
erDiagram
    loans ||--o{ collection_actions : "gestiones de cobranza"
    installments ||--o{ collection_actions : "cuota intervenida"

    collection_actions {
        UUID id PK
        UUID loan_id FK
        UUID installment_id FK
        collection_action_type action_type
        text note
        collection_action_status status
        timestamptz next_action_at
        numeric promise_amount
        date promise_date
        UUID created_by FK
    }

    integration_events {
        UUID id PK
        varchar provider
        varchar event_type
        varchar external_id
        jsonb payload
        integration_event_status status
        text error_detail
        smallint retry_count
        timestamptz next_retry_at
    }
```
