# Diagramas de Entidad - Relación (ERD)

Este documento detalla la topología de la base de datos a través de modelos conceptuales y físicos mediante diagramas Mermaid.

---

## 1. Diagrama General de la Arquitectura (Core del Sistema)

El siguiente diagrama presenta la interacción central entre los prestatarios, las solicitudes, los créditos formalizados, el cronograma de amortización, los abonos y la tesorería.

```mermaid
erDiagram
    branches ||--o{ users : "asigna a"
    branches ||--o{ cash_boxes : "posee"
    branches ||--o{ zones : "agrupa"

    users ||--o{ refresh_tokens : "posee"
    users ||--o{ audit_logs : "genera accion"
    users ||--o{ loan_applications : "evalua"
    users ||--o{ loans : "desembolsa"
    users ||--o{ payments : "registra"
    users ||--o{ cash_sessions : "opera"

    customers ||--o{ customer_documents : "adjunta expediente"
    customers ||--o{ otp_verifications : "solicita verificacion"
    customers ||--o{ loan_applications : "solicita credito"
    customers ||--o{ loans : "titular de"
    customers ||--o{ notifications : "recibe"

    currencies ||--o{ loan_products : "moneda de"
    currencies ||--o{ cash_boxes : "moneda de"
    currencies ||--o{ transactions : "moneda de"

    loan_products ||--o{ loan_applications : "parametriza"
    loan_products ||--o{ loans : "define terminos"

    loan_applications ||--o| loans : "se formaliza en"

    loans ||--o{ schedules : "versiona"
    loans ||--o{ installments : "compromete"
    loans ||--o{ payments : "recibe abonos"
    loans ||--o{ transactions : "registra en ledger"
    loans ||--o{ collection_actions : "gestiona mora"
    loans ||--o{ prepayment_quotes : "cotiza liquidacion"

    schedules ||--o{ installments : "contiene cuotas"

    payments ||--o{ payment_allocations : "desglosa prelacion"
    installments ||--o{ payment_allocations : "amortiza"
    payments ||--o{ transactions : "asienta en ledger"

    cash_boxes ||--o{ cash_sessions : "abre/cierra"
    cash_boxes ||--o{ cash_movements : "registra flujo"
    cash_boxes ||--o{ cash_transfers : "origen / destino"
    cash_boxes ||--o{ loans : "desembolsa de"
    cash_boxes ||--o{ payments : "cobra en"
```

---

## 2. Subsistemas Detallados

### 2.1 Subsistema de Seguridad, Usuarios y Organización
Gestiona las credenciales de acceso, la autenticación por JWT rotativo y el registro de bitácora inmutable.

```mermaid
erDiagram
    branches {
        UUID id PK
        varchar code UK
        varchar name
        text address
        varchar district
        boolean is_active
    }
    zones {
        UUID id PK
        varchar name
        UUID branch_id FK
        boolean is_active
    }
    users {
        UUID id PK
        varchar email UK
        varchar password_hash
        varchar full_name
        user_role role
        user_status_type status
        UUID branch_id FK
        smallint failed_login_count
        timestamptz locked_until
    }
    user_zones {
        UUID user_id PK,FK
        UUID zone_id PK,FK
    }
    refresh_tokens {
        UUID id PK
        UUID user_id FK
        varchar token_hash UK
        timestamptz expires_at
        timestamptz revoked_at
    }
    audit_logs {
        bigserial id PK
        varchar entity_name
        varchar entity_id
        varchar action
        jsonb state_before
        jsonb state_after
        UUID user_id FK
        timestamptz created_at
    }

    branches ||--o{ users : "pertenece"
    branches ||--o{ zones : "contiene"
    users ||--o{ user_zones : "asigna"
    zones ||--o{ user_zones : "asignada a"
    users ||--o{ refresh_tokens : "emite"
    users ||--o{ audit_logs : "interviene"
```

---

### 2.2 Subsistema de Clientes, Expedientes KYC y Portal
Administra el perfil integral del prestatario, el expediente documental y el portal de autoservicio.

```mermaid
erDiagram
    users ||--o| customers : "vincula cuenta portal"

    customers {
        UUID id PK
        person_type person_type
        document_type doc_type
        varchar doc_number
        varchar full_name
        varchar email
        varchar phone_primary
        numeric monthly_net_income
        kyc_status_type kyc_status
        numeric credit_balance
        UUID portal_user_id FK
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

    customers ||--o{ customer_documents : "expediente digital"
    customers ||--o{ otp_verifications : "codigos OTP"
    customers ||--o{ notifications : "envio de alertas"
```

---

### 2.3 Subsistema de Originación, Préstamos y Cronogramas (Core Financiero)
Gestiona el ciclo de vida de la solicitud, el desembolso con idempotencia y el cronograma congelado en snapshots.

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
        UUID application_id FK,UK
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
        varchar idempotency_key UK
        UUID cashbox_id FK
        UUID disbursed_by FK
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
        numeric prepayment_penalty
        numeric total_to_pay
        date quote_valid_until
        boolean is_applied
    }

    loan_products ||--o{ loan_applications : "plantilla de"
    customers ||--o{ loan_applications : "solicita"
    loan_applications ||--o| loans : "formaliza a"
    customers ||--o{ loans : "deudor"
    loan_products ||--o{ loans : "condiciones"
    loans ||--o{ schedules : "versiones"
    schedules ||--o{ installments : "lista cuotas"
    loans ||--o{ installments : "pertenece"
    loans ||--o{ prepayment_quotes : "cotiza prepago"
```

---

### 2.4 Subsistema de Pagos, Cascada de Prelación y Libro Mayor
Gobernado por la jerarquía estricta de imputación: Mora -> Intereses -> Comisiones -> Capital.

```mermaid
erDiagram
    loans ||--o{ payments : "recibe pago"
    payment_methods ||--o{ payments : "canal de abono"
    cash_boxes ||--o{ payments : "caja receptora"

    payments {
        UUID id PK
        UUID loan_id FK
        payment_kind payment_kind
        numeric amount
        UUID payment_method_id FK
        varchar reference
        timestamptz paid_at
        payment_status status
        UUID reversal_of_id FK
        text reversal_reason
        varchar receipt_number UK
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
        UUID cashbox_id FK
        transaction_type transaction_type
        transaction_dir direction
        numeric amount
        UUID currency_id FK
        varchar reference
    }

    payments ||--o{ payment_allocations : "imputa"
    installments ||--o{ payment_allocations : "es amortizada"
    payments ||--o{ transactions : "movimiento financiero"
    loans ||--o{ transactions : "desembolso o ajuste"
    payments ||--o| payments : "reverso de"
```

---

### 2.5 Subsistema de Tesorería, Cajas y Arqueos
Supervisa la custodia física de efectivo, sesiones con arqueo ciego y transferencias seguras entre cajas.

```mermaid
erDiagram
    cash_boxes {
        UUID id PK
        varchar name
        UUID branch_id FK
        UUID currency_id FK
        UUID responsible_user_id FK
        numeric current_balance
        cashbox_status status
    }
    cash_sessions {
        UUID id PK
        UUID cashbox_id FK
        UUID opened_by FK
        numeric opening_balance
        timestamptz opened_at
        UUID closed_by FK
        timestamptz closed_at
        numeric counted_amount
        numeric expected_amount
        numeric difference
        arqueo_result arqueo_result
        jsonb arqueo_detail
    }
    cash_movements {
        UUID id PK
        UUID cashbox_id FK
        UUID session_id FK
        cash_movement_type movement_type
        varchar concept
        numeric amount
        varchar voucher_number
        UUID created_by FK
    }
    cash_transfers {
        UUID id PK
        UUID source_cashbox_id FK
        UUID destination_cashbox_id FK
        numeric amount
        transfer_status status
        UUID initiated_by FK
        UUID accepted_by FK
    }

    cash_boxes ||--o{ cash_sessions : "sesiones"
    cash_boxes ||--o{ cash_movements : "flujos extraordinarios"
    cash_sessions ||--o{ cash_movements : "asocia"
    cash_boxes ||--o{ cash_transfers : "origen"
    cash_boxes ||--o{ cash_transfers : "destino"
```

---

### 2.6 Subsistema de Cobranzas, Integraciones y Conciliación
Motor de recuperación de cartera vencida y enlace seguro con el ecosistema bancario (webhooks y extractos).

```mermaid
erDiagram
    loans ||--o{ collection_actions : "gestiones"
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

    bank_accounts {
        UUID id PK
        varchar bank_name
        varchar account_number_masked
        UUID currency_id FK
        boolean is_active
    }

    reconciliations {
        UUID id PK
        UUID bank_account_id FK
        varchar source
        varchar external_reference
        date bank_date
        numeric bank_amount
        UUID loan_id FK
        UUID payment_id FK
        reconciliation_status status
        UUID reconciled_by FK
    }

    bank_accounts ||--o{ reconciliations : "extracto de"
    loans ||--o{ reconciliations : "vincula credito"
    payments ||--o{ reconciliations : "confirma abono"
```
