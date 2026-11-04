-- =============================================================================
-- SISTEMA DE PRÉSTAMOS — ESQUEMA COMPLETO DE BASE DE DATOS
-- Base de Datos: PostgreSQL 15+
-- Precisión Monetaria: NUMERIC(18,4) — Nunca FLOAT/DOUBLE
-- Redondeo: Half-Up a 2 decimales en cuotas finales
-- Inmutabilidad: AuditLog solo-inserción; pagos con contra-asientos
-- =============================================================================

-- ─────────────────────────────────────────────────────────────────────────────
-- EXTENSIONES
-- ─────────────────────────────────────────────────────────────────────────────
CREATE EXTENSION IF NOT EXISTS "pgcrypto";   -- gen_random_uuid()
CREATE EXTENSION IF NOT EXISTS "unaccent";   -- búsquedas sin tildes

-- ─────────────────────────────────────────────────────────────────────────────
-- TIPOS ENUMERADOS (ENUM)
-- ─────────────────────────────────────────────────────────────────────────────

-- Tipo de persona del cliente
CREATE TYPE person_type        AS ENUM ('NATURAL', 'JURIDICA');

-- Tipos de documento de identidad
CREATE TYPE document_type      AS ENUM ('DNI', 'CE', 'RUC', 'PASAPORTE');

-- Estado KYC del cliente
CREATE TYPE kyc_status_type    AS ENUM ('PENDING', 'VERIFIED', 'REJECTED', 'EXPIRED');

-- Roles del sistema (backoffice y portal cliente)
CREATE TYPE user_role          AS ENUM (
    'ROLE_ADMIN', 'ROLE_ANALYST', 'ROLE_CASHIER',
    'ROLE_COLLECTOR', 'ROLE_AUDITOR', 'ROLE_CLIENT'
);

-- Estado del usuario
CREATE TYPE user_status_type   AS ENUM ('ACTIVE', 'INACTIVE', 'LOCKED');

-- Frecuencias de pago de un préstamo
CREATE TYPE payment_frequency  AS ENUM ('DIARIA', 'SEMANAL', 'QUINCENAL', 'MENSUAL');

-- Métodos de amortización financiera
CREATE TYPE amortization_method AS ENUM ('FRANCES', 'FLAT', 'ALEMAN', 'AMERICANO');

-- Tipo de tasa de interés
CREATE TYPE rate_type          AS ENUM ('NOMINAL', 'EFECTIVA');

-- Base de cálculo de mora
CREATE TYPE mora_base_type     AS ENUM ('SALDO_CAPITAL_VENCIDO', 'CUOTA_TOTAL_VENCIDA');

-- Modo de cobro de comisión de apertura
CREATE TYPE commission_mode    AS ENUM ('DEDUCTIVA', 'FINANCIADA');

-- Estado de un producto de crédito
CREATE TYPE product_status     AS ENUM ('ACTIVO', 'INACTIVO');

-- Estados de la solicitud de préstamo
CREATE TYPE application_status AS ENUM (
    'SOLICITADO', 'EN_EVALUACION', 'APROBADO',
    'APROBADO_CON_CONDICIONES', 'RECHAZADO', 'ANULADO'
);

-- Decisión del motor de evaluación
CREATE TYPE credit_decision    AS ENUM ('APROBABLE', 'REVISION_MANUAL', 'RECHAZO_SUGERIDO');

-- Estados del préstamo activo
CREATE TYPE loan_status        AS ENUM (
    'VIGENTE', 'EN_MORA', 'PAGADO',
    'CASTIGADO', 'CANCELADO', 'DESEMBOLSADO'
);

-- Motivo de cierre del préstamo
CREATE TYPE loan_close_reason  AS ENUM ('PAGO_NORMAL', 'LIQUIDACION_ANTICIPADA', 'REFINANCIAMIENTO', 'CASTIGO');

-- Estado de cada cuota del cronograma
CREATE TYPE installment_status AS ENUM ('PENDING', 'PARTIAL', 'PAID', 'OVERDUE', 'CANCELLED_BY_PREPAYMENT');

-- Estado de un pago registrado
CREATE TYPE payment_status     AS ENUM ('APPLIED', 'REVERSED', 'PENDING_CONFIRMATION');

-- Tipo de pago (normal vs reverso)
CREATE TYPE payment_kind       AS ENUM ('NORMAL', 'REVERSAL');

-- Métodos de pago habilitados
CREATE TYPE payment_method_type AS ENUM (
    'EFECTIVO', 'TRANSFERENCIA', 'YAPE', 'PLIN',
    'TARJETA', 'DEPOSITO_VENTANILLA'
);

-- Tipos de transacción en el libro mayor
CREATE TYPE transaction_type   AS ENUM (
    'DISBURSEMENT', 'PAYMENT', 'REVERSAL',
    'CUSTOMER_CREDIT_BALANCE', 'WRITE_OFF', 'ADJUSTMENT',
    'CASH_TRANSFER', 'CASH_EXPENSE', 'CASH_INCOME'
);

-- Dirección del flujo de dinero
CREATE TYPE transaction_dir    AS ENUM ('INFLOW', 'OUTFLOW');

-- Estado de la caja física
CREATE TYPE cashbox_status     AS ENUM ('ABIERTA', 'CERRADA');

-- Tipo de movimiento de caja
CREATE TYPE cash_movement_type AS ENUM ('INGRESO_EXTRAORDINARIO', 'EGRESO_GASTO');

-- Estado de transferencia entre cajas
CREATE TYPE transfer_status    AS ENUM ('EN_TRANSITO', 'COMPLETED', 'CANCELLED');

-- Resultado del arqueo
CREATE TYPE arqueo_result      AS ENUM ('CUADRADO', 'SOBRANTE', 'FALTANTE');

-- Tipos de acción de cobranza
CREATE TYPE collection_action_type AS ENUM (
    'LLAMADA', 'WHATSAPP', 'VISITA_DOMICILIARIA',
    'NOTIFICACION_ESCRITA', 'PROMESA_DE_PAGO'
);

-- Estado de conciliación
CREATE TYPE reconciliation_status AS ENUM (
    'CONCILIADO_AUTOMATICO', 'PENDIENTE_CONCILIACION',
    'DESCUADRADO', 'RECONCILED'
);

-- Estado de un evento de integración externa
CREATE TYPE integration_event_status AS ENUM (
    'RECEIVED', 'PROCESSED', 'FAILED',
    'DEAD_LETTER', 'FAILED_NEEDS_REVIEW'
);

-- Canal de notificación al cliente
CREATE TYPE notification_channel AS ENUM ('EMAIL', 'SMS', 'WHATSAPP', 'PUSH');

-- Estado de notificación
CREATE TYPE notification_status AS ENUM ('PENDING', 'SENT', 'FAILED');

-- Ámbito de un feriado
CREATE TYPE holiday_scope      AS ENUM ('NACIONAL', 'SUCURSAL');

-- Estado de una acción de cobranza
CREATE TYPE collection_action_status AS ENUM ('ACTIVA', 'CUMPLIDA', 'PROMESA_INCUMPLIDA', 'CERRADA');

-- Tipo de un préstamo de refinanciamiento
CREATE TYPE loan_purpose_type  AS ENUM ('CONSUMO', 'CAPITAL_TRABAJO', 'VEHICULO', 'VIVIENDA', 'REFINANCING', 'CONSOLIDATION', 'OTRO');

-- =============================================================================
-- MÓDULO 1 — ADMINISTRACIÓN Y CONFIGURACIÓN GLOBAL
-- =============================================================================

-- ─────────────────────────────────────────────────────────────────────────────
-- Empresa / Institución financiera
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE company (
    id                  UUID            PRIMARY KEY DEFAULT gen_random_uuid(),
    legal_name          VARCHAR(200)    NOT NULL,                 -- Razón social
    trade_name          VARCHAR(200),                             -- Nombre comercial
    tax_id              VARCHAR(20)     NOT NULL UNIQUE,          -- RUC / NIT / RFC
    legal_address       TEXT            NOT NULL,
    support_phone       VARCHAR(20),
    support_email       VARCHAR(150),
    legal_rep_name      VARCHAR(200),
    legal_rep_doc       VARCHAR(30),
    logo_url            TEXT,                                     -- URL del logo en Object Storage
    default_currency_id UUID,                                     -- FK → currencies (se agrega después)
    created_at          TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ     NOT NULL DEFAULT NOW()
);

-- ─────────────────────────────────────────────────────────────────────────────
-- Monedas
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE currencies (
    id          UUID            PRIMARY KEY DEFAULT gen_random_uuid(),
    code        CHAR(3)         NOT NULL UNIQUE,  -- ISO 4217: PEN, USD, EUR
    name        VARCHAR(60)     NOT NULL,
    symbol      VARCHAR(5)      NOT NULL,
    is_active   BOOLEAN         NOT NULL DEFAULT TRUE,
    created_at  TIMESTAMPTZ     NOT NULL DEFAULT NOW()
);

-- FK diferida: company.default_currency_id → currencies
ALTER TABLE company
    ADD CONSTRAINT fk_company_currency
    FOREIGN KEY (default_currency_id) REFERENCES currencies(id);

-- ─────────────────────────────────────────────────────────────────────────────
-- Sucursales / Agencias
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE branches (
    id          UUID            PRIMARY KEY DEFAULT gen_random_uuid(),
    code        VARCHAR(20)     NOT NULL UNIQUE,
    name        VARCHAR(150)    NOT NULL,
    address     TEXT,
    district    VARCHAR(100),
    is_active   BOOLEAN         NOT NULL DEFAULT TRUE,
    created_at  TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    updated_at  TIMESTAMPTZ     NOT NULL DEFAULT NOW()
);

-- ─────────────────────────────────────────────────────────────────────────────
-- Zonas geográficas de cobranza
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE zones (
    id          UUID            PRIMARY KEY DEFAULT gen_random_uuid(),
    name        VARCHAR(100)    NOT NULL,
    description TEXT,
    branch_id   UUID            REFERENCES branches(id),
    is_active   BOOLEAN         NOT NULL DEFAULT TRUE,
    created_at  TIMESTAMPTZ     NOT NULL DEFAULT NOW()
);

-- ─────────────────────────────────────────────────────────────────────────────
-- Feriados y días no hábiles
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE holidays (
    id           UUID            PRIMARY KEY DEFAULT gen_random_uuid(),
    holiday_date DATE            NOT NULL,
    name         VARCHAR(150)    NOT NULL,
    scope        holiday_scope   NOT NULL DEFAULT 'NACIONAL',
    branch_id    UUID            REFERENCES branches(id),     -- NULL = aplica a todas
    is_recurring BOOLEAN         NOT NULL DEFAULT FALSE,
    created_at   TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    UNIQUE (holiday_date, scope, branch_id)
);

-- ─────────────────────────────────────────────────────────────────────────────
-- Tipos de documentos de identidad (catálogo configurable)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE document_types (
    id              UUID            PRIMARY KEY DEFAULT gen_random_uuid(),
    code            VARCHAR(10)     NOT NULL UNIQUE,  -- DNI, CE, RUC, PAS
    name            VARCHAR(60)     NOT NULL,
    char_length     SMALLINT,                         -- Longitud exacta (ej. 8 para DNI)
    validation_regex VARCHAR(100),
    is_active       BOOLEAN         NOT NULL DEFAULT TRUE
);

-- ─────────────────────────────────────────────────────────────────────────────
-- Métodos de pago habilitados
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE payment_methods (
    id              UUID            PRIMARY KEY DEFAULT gen_random_uuid(),
    name            VARCHAR(80)     NOT NULL,
    method_type     payment_method_type NOT NULL,
    provider_code   VARCHAR(50),
    is_active       BOOLEAN         NOT NULL DEFAULT TRUE,
    created_at      TIMESTAMPTZ     NOT NULL DEFAULT NOW()
);

-- ─────────────────────────────────────────────────────────────────────────────
-- Plantillas legales de contratos (versionadas)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE legal_templates (
    id              UUID            PRIMARY KEY DEFAULT gen_random_uuid(),
    code            VARCHAR(50)     NOT NULL,         -- ej. 'CONTRATO_PRESTAMO', 'PAGARE'
    version         SMALLINT        NOT NULL DEFAULT 1,
    content         TEXT            NOT NULL,         -- HTML/Markdown con variables {{}}
    is_current      BOOLEAN         NOT NULL DEFAULT TRUE,
    created_by      UUID,                             -- FK → users
    created_at      TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    UNIQUE (code, version)
);

-- =============================================================================
-- MÓDULO 2 — SEGURIDAD, USUARIOS Y AUDITORÍA
-- =============================================================================

-- ─────────────────────────────────────────────────────────────────────────────
-- Usuarios del sistema (backoffice + portal cliente)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE users (
    id                  UUID            PRIMARY KEY DEFAULT gen_random_uuid(),
    email               VARCHAR(150)    NOT NULL UNIQUE,
    password_hash       VARCHAR(255)    NOT NULL,             -- BCrypt / Argon2id
    full_name           VARCHAR(200)    NOT NULL,
    phone               VARCHAR(20),
    role                user_role       NOT NULL,
    status              user_status_type NOT NULL DEFAULT 'ACTIVE',
    branch_id           UUID            REFERENCES branches(id),
    failed_login_count  SMALLINT        NOT NULL DEFAULT 0,
    locked_until        TIMESTAMPTZ,
    last_login_at       TIMESTAMPTZ,
    invitation_token    VARCHAR(255),
    invitation_sent_at  TIMESTAMPTZ,
    password_changed_at TIMESTAMPTZ,
    created_at          TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ     NOT NULL DEFAULT NOW()
);

-- FK diferida para legal_templates.created_by
ALTER TABLE legal_templates
    ADD CONSTRAINT fk_template_user
    FOREIGN KEY (created_by) REFERENCES users(id);

-- ─────────────────────────────────────────────────────────────────────────────
-- Refresh Tokens (revocables individualmente)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE refresh_tokens (
    id          UUID            PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id     UUID            NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    token_hash  VARCHAR(255)    NOT NULL UNIQUE,
    issued_at   TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    expires_at  TIMESTAMPTZ     NOT NULL,
    revoked_at  TIMESTAMPTZ,
    user_ip     INET,
    user_agent  TEXT
);

-- ─────────────────────────────────────────────────────────────────────────────
-- Asignación usuario ↔ zona (un cobrador puede cubrir varias zonas)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE user_zones (
    user_id     UUID    NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    zone_id     UUID    NOT NULL REFERENCES zones(id) ON DELETE CASCADE,
    PRIMARY KEY (user_id, zone_id)
);

-- ─────────────────────────────────────────────────────────────────────────────
-- Bitácora de auditoría — SOLO INSERCIÓN (append-only)
-- Regla: a nivel de BD se prohíben UPDATE y DELETE en esta tabla
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE audit_logs (
    id              BIGSERIAL       PRIMARY KEY,
    entity_name     VARCHAR(100)    NOT NULL,         -- 'LoanApplication', 'Payment', etc.
    entity_id       VARCHAR(50)     NOT NULL,         -- ID de la entidad afectada
    action          VARCHAR(30)     NOT NULL,         -- CREATE, UPDATE, DELETE, APPROVE, REVERSE
    state_before    JSONB,
    state_after     JSONB,
    user_id         UUID            REFERENCES users(id),
    user_ip         INET,
    user_agent      TEXT,
    created_at      TIMESTAMPTZ     NOT NULL DEFAULT NOW()
);

-- Trigger para rechazar UPDATE/DELETE en audit_logs
CREATE OR REPLACE FUNCTION fn_audit_log_immutable()
RETURNS TRIGGER LANGUAGE plpgsql AS $$
BEGIN
    RAISE EXCEPTION 'audit_logs es append-only: no se permiten UPDATE ni DELETE.';
END;
$$;

CREATE TRIGGER tg_audit_log_no_update
    BEFORE UPDATE ON audit_logs
    FOR EACH ROW EXECUTE FUNCTION fn_audit_log_immutable();

CREATE TRIGGER tg_audit_log_no_delete
    BEFORE DELETE ON audit_logs
    FOR EACH ROW EXECUTE FUNCTION fn_audit_log_immutable();

-- =============================================================================
-- MÓDULO 3 — CLIENTES Y KYC
-- =============================================================================

-- ─────────────────────────────────────────────────────────────────────────────
-- Clientes (personas naturales o jurídicas)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE customers (
    id                  UUID            PRIMARY KEY DEFAULT gen_random_uuid(),
    person_type         person_type     NOT NULL DEFAULT 'NATURAL',
    doc_type            document_type   NOT NULL,
    doc_number          VARCHAR(20)     NOT NULL,
    full_name           VARCHAR(200)    NOT NULL,          -- Nombre o Razón social
    email               VARCHAR(150),
    phone_primary       VARCHAR(20),
    phone_secondary     VARCHAR(20),
    address             TEXT,
    district            VARCHAR(100),
    birth_date          DATE,                              -- Requerido para validación de edad
    -- Perfil socioeconómico
    monthly_net_income  NUMERIC(18,4)   NOT NULL DEFAULT 0,
    occupation          VARCHAR(150),
    employer_name       VARCHAR(200),
    work_seniority_months SMALLINT      DEFAULT 0,
    -- Estado KYC
    kyc_status          kyc_status_type NOT NULL DEFAULT 'PENDING',
    kyc_verified_by     UUID            REFERENCES users(id),
    kyc_verified_at     TIMESTAMPTZ,
    kyc_notes           TEXT,
    kyc_expires_at      TIMESTAMPTZ,
    -- Saldo a favor
    credit_balance      NUMERIC(18,4)   NOT NULL DEFAULT 0 CHECK (credit_balance >= 0),
    -- Portal cliente
    portal_user_id      UUID            REFERENCES users(id),
    -- Consentimiento GDPR / LPDP
    consent_given_at    TIMESTAMPTZ,
    consent_ip          INET,
    created_by          UUID            REFERENCES users(id),
    created_at          TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    UNIQUE (doc_type, doc_number)       -- RF-04.2: unicidad de documento
);

-- ─────────────────────────────────────────────────────────────────────────────
-- Expediente KYC — Documentos adjuntos del cliente
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE customer_documents (
    id              UUID            PRIMARY KEY DEFAULT gen_random_uuid(),
    customer_id     UUID            NOT NULL REFERENCES customers(id) ON DELETE CASCADE,
    doc_category    VARCHAR(60)     NOT NULL,  -- 'DNI_FRONTAL','DNI_REVERSO','BOLETA_PAGO','SELFIE', etc.
    file_hash       VARCHAR(100)    NOT NULL,  -- SHA-256 del archivo
    storage_uri     TEXT            NOT NULL,  -- URI en Object Storage (S3/Blob)
    mime_type       VARCHAR(60),
    uploaded_by     UUID            REFERENCES users(id),
    uploaded_at     TIMESTAMPTZ     NOT NULL DEFAULT NOW()
);

-- ─────────────────────────────────────────────────────────────────────────────
-- Código OTP (verificación de portal cliente)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE otp_verifications (
    id              UUID            PRIMARY KEY DEFAULT gen_random_uuid(),
    customer_id     UUID            NOT NULL REFERENCES customers(id) ON DELETE CASCADE,
    target          VARCHAR(150)    NOT NULL,  -- email o teléfono
    channel         notification_channel NOT NULL,
    otp_hash        VARCHAR(255)    NOT NULL,
    expires_at      TIMESTAMPTZ     NOT NULL,
    verified_at     TIMESTAMPTZ,
    created_at      TIMESTAMPTZ     NOT NULL DEFAULT NOW()
);

-- =============================================================================
-- MÓDULO 4 — PRODUCTOS DE CRÉDITO
-- =============================================================================

CREATE TABLE loan_products (
    id                      UUID            PRIMARY KEY DEFAULT gen_random_uuid(),
    name                    VARCHAR(150)    NOT NULL,
    description             TEXT,
    currency_id             UUID            NOT NULL REFERENCES currencies(id),
    -- Montos y plazos
    min_amount              NUMERIC(18,4)   NOT NULL CHECK (min_amount > 0),
    max_amount              NUMERIC(18,4)   NOT NULL,
    min_term                SMALLINT        NOT NULL CHECK (min_term >= 1),   -- cuotas
    max_term                SMALLINT        NOT NULL,
    -- Frecuencias admitidas (array)
    allowed_frequencies     payment_frequency[] NOT NULL DEFAULT '{MENSUAL}',
    -- Método de amortización
    amortization_method     amortization_method NOT NULL DEFAULT 'FRANCES',
    -- Tasa de interés
    rate_type               rate_type       NOT NULL DEFAULT 'EFECTIVA',
    annual_rate             NUMERIC(12,6)   NOT NULL CHECK (annual_rate > 0), -- TEA como decimal (ej. 0.300000)
    -- Comisión de apertura
    opening_fee_rate        NUMERIC(12,6)   NOT NULL DEFAULT 0,               -- % del capital
    opening_fee_fixed       NUMERIC(18,4)   NOT NULL DEFAULT 0,               -- monto fijo
    opening_fee_mode        commission_mode NOT NULL DEFAULT 'DEDUCTIVA',
    -- Seguro/gastos periódicos (monto fijo por cuota)
    periodic_fee            NUMERIC(18,4)   NOT NULL DEFAULT 0,
    -- Mora
    grace_days              SMALLINT        NOT NULL DEFAULT 0,
    mora_rate_per_day       NUMERIC(12,6)   NOT NULL DEFAULT 0,               -- tasa diaria decimal
    mora_base               mora_base_type  NOT NULL DEFAULT 'SALDO_CAPITAL_VENCIDO',
    allows_anatocismo       BOOLEAN         NOT NULL DEFAULT FALSE,
    -- Penalidad por prepago
    prepayment_penalty_rate NUMERIC(12,6)   NOT NULL DEFAULT 0,
    -- Elegibilidad
    min_age                 SMALLINT        NOT NULL DEFAULT 18,
    max_age                 SMALLINT        NOT NULL DEFAULT 70,
    min_monthly_income      NUMERIC(18,4)   NOT NULL DEFAULT 0,
    -- Ajuste de feriados
    holiday_shift_direction VARCHAR(10)     NOT NULL DEFAULT 'NEXT',  -- NEXT | PREV
    -- Estado
    status                  product_status  NOT NULL DEFAULT 'ACTIVO',
    created_by              UUID            REFERENCES users(id),
    created_at              TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    updated_at              TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    CONSTRAINT chk_product_amounts CHECK (max_amount >= min_amount),
    CONSTRAINT chk_product_terms   CHECK (max_term >= min_term)
);

-- =============================================================================
-- MÓDULO 5 — ORIGINACIÓN, SOLICITUDES Y EVALUACIÓN
-- =============================================================================

-- ─────────────────────────────────────────────────────────────────────────────
-- Solicitud de crédito
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE loan_applications (
    id                  UUID                PRIMARY KEY DEFAULT gen_random_uuid(),
    customer_id         UUID                NOT NULL REFERENCES customers(id),
    product_id          UUID                NOT NULL REFERENCES loan_products(id),
    -- Montos y condiciones solicitadas
    requested_amount    NUMERIC(18,4)       NOT NULL CHECK (requested_amount > 0),
    approved_amount     NUMERIC(18,4),
    term                SMALLINT            NOT NULL CHECK (term >= 1),
    frequency           payment_frequency   NOT NULL,
    destination         loan_purpose_type   NOT NULL DEFAULT 'CONSUMO',
    -- Estado del proceso
    status              application_status  NOT NULL DEFAULT 'SOLICITADO',
    -- Evaluación crediticia
    dti_ratio           NUMERIC(8,4),       -- Debt-to-Income %
    score               NUMERIC(8,4),
    system_recommendation credit_decision,
    -- Dictamen del analista
    decision            VARCHAR(30),
    decision_notes      TEXT,
    decision_by         UUID                REFERENCES users(id),
    decided_at          TIMESTAMPTZ,
    -- Canal de origen
    originated_from     VARCHAR(20)         NOT NULL DEFAULT 'BACKOFFICE', -- BACKOFFICE | PORTAL
    created_by          UUID                REFERENCES users(id),
    created_at          TIMESTAMPTZ         NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ         NOT NULL DEFAULT NOW()
);

-- =============================================================================
-- MÓDULO 6 — PRÉSTAMOS, CRONOGRAMAS Y CUOTAS
-- =============================================================================

-- ─────────────────────────────────────────────────────────────────────────────
-- Préstamo activo
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE loans (
    id                      UUID            PRIMARY KEY DEFAULT gen_random_uuid(),
    application_id          UUID            UNIQUE REFERENCES loan_applications(id),
    customer_id             UUID            NOT NULL REFERENCES customers(id),
    product_id              UUID            NOT NULL REFERENCES loan_products(id),
    -- Parámetros congelados del contrato (snapshot al momento del desembolso)
    principal               NUMERIC(18,4)   NOT NULL CHECK (principal > 0),
    disbursed_amount        NUMERIC(18,4)   NOT NULL,  -- Neto entregado (descontada comisión deductiva)
    annual_rate             NUMERIC(12,6)   NOT NULL,
    amortization_method     amortization_method NOT NULL,
    frequency               payment_frequency   NOT NULL,
    term                    SMALLINT        NOT NULL,
    grace_days              SMALLINT        NOT NULL DEFAULT 0,
    mora_rate_per_day       NUMERIC(12,6)   NOT NULL DEFAULT 0,
    mora_base               mora_base_type  NOT NULL DEFAULT 'SALDO_CAPITAL_VENCIDO',
    opening_fee_amount      NUMERIC(18,4)   NOT NULL DEFAULT 0,
    periodic_fee            NUMERIC(18,4)   NOT NULL DEFAULT 0,
    prepayment_penalty_rate NUMERIC(12,6)   NOT NULL DEFAULT 0,
    -- Fechas clave
    disbursement_date       DATE            NOT NULL,
    first_due_date          DATE            NOT NULL,
    -- Totales proyectados (calculados al generar el cronograma)
    total_scheduled_interest  NUMERIC(18,4) NOT NULL DEFAULT 0,
    total_scheduled_fees      NUMERIC(18,4) NOT NULL DEFAULT 0,
    -- Acumulados realizados
    total_paid_principal    NUMERIC(18,4)   NOT NULL DEFAULT 0,
    total_paid_interest     NUMERIC(18,4)   NOT NULL DEFAULT 0,
    total_paid_fees         NUMERIC(18,4)   NOT NULL DEFAULT 0,
    total_paid_mora         NUMERIC(18,4)   NOT NULL DEFAULT 0,
    -- Estado
    status                  loan_status     NOT NULL DEFAULT 'VIGENTE',
    close_reason            loan_close_reason,
    closed_at               TIMESTAMPTZ,
    -- Propósito especial (refinanciamiento)
    purpose_type            loan_purpose_type NOT NULL DEFAULT 'CONSUMO',
    refinanced_from_loan_id UUID            REFERENCES loans(id),
    -- Caja donde se efectuó el desembolso
    cashbox_id              UUID,           -- FK → cash_boxes (se agrega después)
    disbursed_by            UUID            REFERENCES users(id),
    -- Idempotencia del desembolso
    idempotency_key         VARCHAR(100)    UNIQUE,
    created_at              TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    updated_at              TIMESTAMPTZ     NOT NULL DEFAULT NOW()
);

-- ─────────────────────────────────────────────────────────────────────────────
-- Cronograma (versión inmutable – permite múltiples versiones por reestructuración)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE schedules (
    id               UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    loan_id          UUID        NOT NULL REFERENCES loans(id),
    snapshot_version SMALLINT    NOT NULL DEFAULT 1,
    generated_at     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    is_active        BOOLEAN     NOT NULL DEFAULT TRUE,  -- Solo 1 activo por préstamo
    notes            TEXT,
    UNIQUE (loan_id, snapshot_version)
);

-- ─────────────────────────────────────────────────────────────────────────────
-- Cuotas del cronograma (snapshot inmutable)
-- Los campos `paid_*` y `late_fee` son los únicos mutables (acumuladores de pago)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE installments (
    id                  UUID                PRIMARY KEY DEFAULT gen_random_uuid(),
    schedule_id         UUID                NOT NULL REFERENCES schedules(id),
    loan_id             UUID                NOT NULL REFERENCES loans(id),
    installment_number  SMALLINT            NOT NULL,
    due_date            DATE                NOT NULL,
    -- Proyectados (INMUTABLES tras el desembolso)
    principal           NUMERIC(18,4)       NOT NULL CHECK (principal >= 0),
    interest            NUMERIC(18,4)       NOT NULL CHECK (interest >= 0),
    fee                 NUMERIC(18,4)       NOT NULL DEFAULT 0,
    total_due           NUMERIC(18,4)       NOT NULL,  -- principal + interest + fee
    -- Realizados (acumuladores, actualizables por pagos)
    paid_principal      NUMERIC(18,4)       NOT NULL DEFAULT 0,
    paid_interest       NUMERIC(18,4)       NOT NULL DEFAULT 0,
    paid_fee            NUMERIC(18,4)       NOT NULL DEFAULT 0,
    late_fee            NUMERIC(18,4)       NOT NULL DEFAULT 0,  -- mora devengada calculada
    paid_late_fee       NUMERIC(18,4)       NOT NULL DEFAULT 0,
    -- Estado y mora
    status              installment_status  NOT NULL DEFAULT 'PENDING',
    days_late           SMALLINT            NOT NULL DEFAULT 0,
    last_mora_calc_date DATE,                                     -- idempotencia del job diario
    UNIQUE (loan_id, installment_number),
    CONSTRAINT chk_paid_principal   CHECK (paid_principal   >= 0),
    CONSTRAINT chk_paid_interest    CHECK (paid_interest    >= 0),
    CONSTRAINT chk_paid_fee         CHECK (paid_fee         >= 0),
    CONSTRAINT chk_late_fee         CHECK (late_fee         >= 0),
    CONSTRAINT chk_paid_late_fee    CHECK (paid_late_fee    >= 0)
);

-- =============================================================================
-- MÓDULO 7 — PAGOS Y LIBRO MAYOR
-- =============================================================================

-- ─────────────────────────────────────────────────────────────────────────────
-- Pagos registrados (INMUTABLES — correcciones solo con REVERSAL)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE payments (
    id                  UUID                PRIMARY KEY DEFAULT gen_random_uuid(),
    loan_id             UUID                NOT NULL REFERENCES loans(id),
    payment_kind        payment_kind        NOT NULL DEFAULT 'NORMAL',
    amount              NUMERIC(18,4)       NOT NULL,  -- positivo (normal) o negativo (reverso)
    payment_method_id   UUID                REFERENCES payment_methods(id),
    reference           VARCHAR(150),                  -- Código de operación / voucher
    paid_at             TIMESTAMPTZ         NOT NULL,
    status              payment_status      NOT NULL DEFAULT 'APPLIED',
    reversal_of_id      UUID                REFERENCES payments(id),  -- apunta al pago original
    reversal_reason     TEXT,
    receipt_number      VARCHAR(30)         UNIQUE,    -- Nro. correlativo de recibo
    cashbox_id          UUID,                          -- FK → cash_boxes (agregada después)
    registered_by       UUID                REFERENCES users(id),
    idempotency_key     VARCHAR(100)        UNIQUE,
    created_at          TIMESTAMPTZ         NOT NULL   DEFAULT NOW()
);

-- ─────────────────────────────────────────────────────────────────────────────
-- Detalle de imputación de cada pago a las cuotas (cascada de prelación)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE payment_allocations (
    id              UUID            PRIMARY KEY DEFAULT gen_random_uuid(),
    payment_id      UUID            NOT NULL REFERENCES payments(id),
    installment_id  UUID            NOT NULL REFERENCES installments(id),
    to_mora         NUMERIC(18,4)   NOT NULL DEFAULT 0,
    to_interest     NUMERIC(18,4)   NOT NULL DEFAULT 0,
    to_fee          NUMERIC(18,4)   NOT NULL DEFAULT 0,
    to_principal    NUMERIC(18,4)   NOT NULL DEFAULT 0,
    created_at      TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    CONSTRAINT chk_alloc_mora       CHECK (to_mora      >= 0),
    CONSTRAINT chk_alloc_interest   CHECK (to_interest  >= 0),
    CONSTRAINT chk_alloc_fee        CHECK (to_fee       >= 0),
    CONSTRAINT chk_alloc_principal  CHECK (to_principal >= 0)
);

-- ─────────────────────────────────────────────────────────────────────────────
-- Libro Mayor / Ledger (transacciones financieras inmutables)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE transactions (
    id              BIGSERIAL           PRIMARY KEY,
    loan_id         UUID                REFERENCES loans(id),
    payment_id      UUID                REFERENCES payments(id),
    cashbox_id      UUID,                               -- FK → cash_boxes
    transaction_type transaction_type   NOT NULL,
    direction       transaction_dir     NOT NULL,
    amount          NUMERIC(18,4)       NOT NULL CHECK (amount > 0),
    currency_id     UUID                NOT NULL REFERENCES currencies(id),
    reference       VARCHAR(150),
    notes           TEXT,
    created_by      UUID                REFERENCES users(id),
    created_at      TIMESTAMPTZ         NOT NULL DEFAULT NOW()
);

-- =============================================================================
-- MÓDULO 8 — CAJA Y TESORERÍA
-- =============================================================================

-- ─────────────────────────────────────────────────────────────────────────────
-- Cajas físicas (ventanillas)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE cash_boxes (
    id                  UUID            PRIMARY KEY DEFAULT gen_random_uuid(),
    name                VARCHAR(100)    NOT NULL,
    branch_id           UUID            NOT NULL REFERENCES branches(id),
    currency_id         UUID            NOT NULL REFERENCES currencies(id),
    responsible_user_id UUID            REFERENCES users(id),
    current_balance     NUMERIC(18,4)   NOT NULL DEFAULT 0,
    status              cashbox_status  NOT NULL DEFAULT 'CERRADA',
    created_at          TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ     NOT NULL DEFAULT NOW()
);

-- FK diferidas hacia cash_boxes
ALTER TABLE loans     ADD CONSTRAINT fk_loan_cashbox      FOREIGN KEY (cashbox_id)    REFERENCES cash_boxes(id);
ALTER TABLE payments  ADD CONSTRAINT fk_payment_cashbox   FOREIGN KEY (cashbox_id)    REFERENCES cash_boxes(id);
ALTER TABLE transactions ADD CONSTRAINT fk_tx_cashbox     FOREIGN KEY (cashbox_id)    REFERENCES cash_boxes(id);

-- ─────────────────────────────────────────────────────────────────────────────
-- Sesiones de caja (apertura / cierre)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE cash_sessions (
    id                  UUID            PRIMARY KEY DEFAULT gen_random_uuid(),
    cashbox_id          UUID            NOT NULL REFERENCES cash_boxes(id),
    opened_by           UUID            NOT NULL REFERENCES users(id),
    opening_balance     NUMERIC(18,4)   NOT NULL DEFAULT 0,
    opened_at           TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    -- Cierre
    closed_by           UUID            REFERENCES users(id),
    closed_at           TIMESTAMPTZ,
    counted_amount      NUMERIC(18,4),  -- Monto físico contado en arqueo
    expected_amount     NUMERIC(18,4),  -- Monto teórico calculado por el sistema
    difference          NUMERIC(18,4),  -- counted - expected
    arqueo_result       arqueo_result,
    arqueo_detail       JSONB,          -- {billetes: {100: 5, 50: 3, ...}, monedas: {...}}
    report_url          TEXT            -- URL del PDF del acta de cierre
);

-- ─────────────────────────────────────────────────────────────────────────────
-- Movimientos de caja (ingresos/egresos extraordinarios)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE cash_movements (
    id              UUID                PRIMARY KEY DEFAULT gen_random_uuid(),
    cashbox_id      UUID                NOT NULL REFERENCES cash_boxes(id),
    session_id      UUID                REFERENCES cash_sessions(id),
    movement_type   cash_movement_type  NOT NULL,
    concept         VARCHAR(200)        NOT NULL,
    amount          NUMERIC(18,4)       NOT NULL CHECK (amount > 0),
    voucher_number  VARCHAR(60),
    created_by      UUID                NOT NULL REFERENCES users(id),
    created_at      TIMESTAMPTZ         NOT NULL DEFAULT NOW()
);

-- ─────────────────────────────────────────────────────────────────────────────
-- Transferencias entre cajas (two-phase handshake)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE cash_transfers (
    id                      UUID            PRIMARY KEY DEFAULT gen_random_uuid(),
    source_cashbox_id       UUID            NOT NULL REFERENCES cash_boxes(id),
    destination_cashbox_id  UUID            NOT NULL REFERENCES cash_boxes(id),
    amount                  NUMERIC(18,4)   NOT NULL CHECK (amount > 0),
    notes                   TEXT,
    status                  transfer_status NOT NULL DEFAULT 'EN_TRANSITO',
    initiated_by            UUID            NOT NULL REFERENCES users(id),
    initiated_at            TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    accepted_by             UUID            REFERENCES users(id),
    accepted_at             TIMESTAMPTZ,
    CONSTRAINT chk_transfer_different_boxes CHECK (source_cashbox_id <> destination_cashbox_id)
);

-- ─────────────────────────────────────────────────────────────────────────────
-- Cuentas bancarias de la empresa
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE bank_accounts (
    id                  UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    bank_name           VARCHAR(100) NOT NULL,
    account_number_masked VARCHAR(30) NOT NULL,  -- Solo últimos 4 dígitos visible
    currency_id         UUID        NOT NULL REFERENCES currencies(id),
    is_active           BOOLEAN     NOT NULL DEFAULT TRUE,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- =============================================================================
-- MÓDULO 9 — MORA Y COBRANZAS
-- =============================================================================

-- ─────────────────────────────────────────────────────────────────────────────
-- Acciones de cobranza (gestiones, promesas de pago)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE collection_actions (
    id              UUID                        PRIMARY KEY DEFAULT gen_random_uuid(),
    loan_id         UUID                        NOT NULL REFERENCES loans(id),
    installment_id  UUID                        REFERENCES installments(id),
    action_type     collection_action_type      NOT NULL,
    note            TEXT,
    status          collection_action_status    NOT NULL DEFAULT 'ACTIVA',
    next_action_at  TIMESTAMPTZ,
    -- Promesa de pago
    promise_amount  NUMERIC(18,4),
    promise_date    DATE,
    created_by      UUID                        NOT NULL REFERENCES users(id),
    created_at      TIMESTAMPTZ                 NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ                 NOT NULL DEFAULT NOW()
);

-- =============================================================================
-- MÓDULO 10 — INTEGRACIONES Y CONCILIACIÓN
-- =============================================================================

-- ─────────────────────────────────────────────────────────────────────────────
-- Eventos de integración externa (webhooks bancarios)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE integration_events (
    id              UUID                        PRIMARY KEY DEFAULT gen_random_uuid(),
    provider        VARCHAR(50)                 NOT NULL,  -- 'bcp','bbva','yape','plin','niubiz'
    event_type      VARCHAR(80)                 NOT NULL,
    external_id     VARCHAR(150)                NOT NULL,  -- Código de operación del banco
    payload         JSONB                       NOT NULL,
    status          integration_event_status    NOT NULL DEFAULT 'RECEIVED',
    error_detail    TEXT,
    retry_count     SMALLINT                    NOT NULL DEFAULT 0,
    next_retry_at   TIMESTAMPTZ,
    processed_at    TIMESTAMPTZ,
    created_at      TIMESTAMPTZ                 NOT NULL DEFAULT NOW(),
    UNIQUE (provider, external_id)              -- RF-36.4: idempotencia por external_id
);

-- ─────────────────────────────────────────────────────────────────────────────
-- Conciliación de pagos (extractos bancarios vs sistema)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE reconciliations (
    id                  UUID                    PRIMARY KEY DEFAULT gen_random_uuid(),
    bank_account_id     UUID                    REFERENCES bank_accounts(id),
    source              VARCHAR(50)             NOT NULL,  -- 'BCP_EXTRATO','YAPE_FILE', etc.
    external_reference  VARCHAR(150),
    bank_date           DATE,
    bank_description    TEXT,
    bank_amount         NUMERIC(18,4),
    loan_id             UUID                    REFERENCES loans(id),
    payment_id          UUID                    REFERENCES payments(id),
    status              reconciliation_status   NOT NULL DEFAULT 'PENDIENTE_CONCILIACION',
    reconciled_by       UUID                    REFERENCES users(id),
    reconciled_at       TIMESTAMPTZ,
    created_at          TIMESTAMPTZ             NOT NULL DEFAULT NOW()
);

-- =============================================================================
-- MÓDULO 11 — NOTIFICACIONES
-- =============================================================================

CREATE TABLE notifications (
    id              UUID                    PRIMARY KEY DEFAULT gen_random_uuid(),
    customer_id     UUID                    NOT NULL REFERENCES customers(id),
    channel         notification_channel    NOT NULL,
    template_code   VARCHAR(80)             NOT NULL,
    payload         JSONB,
    status          notification_status     NOT NULL DEFAULT 'PENDING',
    sent_at         TIMESTAMPTZ,
    error_detail    TEXT,
    created_at      TIMESTAMPTZ             NOT NULL DEFAULT NOW()
);

-- =============================================================================
-- MÓDULO 12 — COTIZACIONES DE PREPAGO (Liquidaciones anticipadas)
-- =============================================================================

CREATE TABLE prepayment_quotes (
    id                  UUID            PRIMARY KEY DEFAULT gen_random_uuid(),
    loan_id             UUID            NOT NULL REFERENCES loans(id),
    as_of_date          DATE            NOT NULL,
    outstanding_principal NUMERIC(18,4) NOT NULL,
    accrued_interest    NUMERIC(18,4)   NOT NULL DEFAULT 0,
    accrued_mora        NUMERIC(18,4)   NOT NULL DEFAULT 0,
    prepayment_penalty  NUMERIC(18,4)   NOT NULL DEFAULT 0,
    total_to_pay        NUMERIC(18,4)   NOT NULL,
    quote_valid_until   DATE            NOT NULL,
    is_applied          BOOLEAN         NOT NULL DEFAULT FALSE,
    generated_by        UUID            REFERENCES users(id),
    created_at          TIMESTAMPTZ     NOT NULL DEFAULT NOW()
);

-- =============================================================================
-- ÍNDICES DE RENDIMIENTO
-- =============================================================================

-- Clientes
CREATE INDEX idx_customers_doc         ON customers (doc_type, doc_number);
CREATE INDEX idx_customers_kyc         ON customers (kyc_status);
CREATE INDEX idx_customers_portal_user ON customers (portal_user_id);

-- Solicitudes
CREATE INDEX idx_applications_customer ON loan_applications (customer_id);
CREATE INDEX idx_applications_product  ON loan_applications (product_id);
CREATE INDEX idx_applications_status   ON loan_applications (status);

-- Préstamos
CREATE INDEX idx_loans_customer        ON loans (customer_id);
CREATE INDEX idx_loans_product         ON loans (product_id);
CREATE INDEX idx_loans_status          ON loans (status);
CREATE INDEX idx_loans_disbursement    ON loans (disbursement_date);

-- Cuotas (crítico para el job diario de mora)
CREATE INDEX idx_installments_loan     ON installments (loan_id);
CREATE INDEX idx_installments_due_date ON installments (due_date);
CREATE INDEX idx_installments_status   ON installments (status);
CREATE INDEX idx_installments_mora_job ON installments (due_date, status)
    WHERE status NOT IN ('PAID','CANCELLED_BY_PREPAYMENT');

-- Pagos
CREATE INDEX idx_payments_loan         ON payments (loan_id);
CREATE INDEX idx_payments_paid_at      ON payments (paid_at);
CREATE INDEX idx_payments_status       ON payments (status);

-- Allocations
CREATE INDEX idx_alloc_payment         ON payment_allocations (payment_id);
CREATE INDEX idx_alloc_installment     ON payment_allocations (installment_id);

-- Transactions (libro mayor)
CREATE INDEX idx_tx_loan               ON transactions (loan_id);
CREATE INDEX idx_tx_created_at         ON transactions (created_at);
CREATE INDEX idx_tx_type               ON transactions (transaction_type);

-- Audit
CREATE INDEX idx_audit_entity          ON audit_logs (entity_name, entity_id);
CREATE INDEX idx_audit_user            ON audit_logs (user_id);
CREATE INDEX idx_audit_created_at      ON audit_logs (created_at);

-- Cobranza
CREATE INDEX idx_collection_loan       ON collection_actions (loan_id);
CREATE INDEX idx_collection_next_at    ON collection_actions (next_action_at)
    WHERE status = 'ACTIVA';

-- Integraciones
CREATE INDEX idx_integration_status    ON integration_events (status);
CREATE INDEX idx_integration_provider  ON integration_events (provider, external_id);

-- Conciliación
CREATE INDEX idx_reconcil_status       ON reconciliations (status);
CREATE INDEX idx_reconcil_payment      ON reconciliations (payment_id);

-- Notificaciones
CREATE INDEX idx_notif_customer        ON notifications (customer_id);
CREATE INDEX idx_notif_status          ON notifications (status);

-- Feriados
CREATE INDEX idx_holidays_date         ON holidays (holiday_date);

-- =============================================================================
-- FUNCIÓN AUXILIAR: updated_at automático
-- =============================================================================
CREATE OR REPLACE FUNCTION fn_set_updated_at()
RETURNS TRIGGER LANGUAGE plpgsql AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$;

-- Aplicar trigger updated_at a todas las tablas que lo tienen
DO $$
DECLARE
    tbl TEXT;
BEGIN
    FOREACH tbl IN ARRAY ARRAY[
        'company','branches','zones','users',
        'customers','loan_products','loan_applications',
        'loans','cash_boxes','collection_actions'
    ] LOOP
        EXECUTE format(
            'CREATE TRIGGER tg_%s_updated_at
             BEFORE UPDATE ON %s
             FOR EACH ROW EXECUTE FUNCTION fn_set_updated_at()',
            tbl, tbl
        );
    END LOOP;
END;
$$;

-- =============================================================================
-- DATOS SEMILLA (Seed Data)
-- =============================================================================

-- Moneda por defecto
INSERT INTO currencies (code, name, symbol) VALUES
    ('PEN', 'Sol Peruano',  'S/'),
    ('USD', 'Dólar Americano', '$');

-- Tipos de documentos
INSERT INTO document_types (code, name, char_length, validation_regex) VALUES
    ('DNI',      'Documento Nacional de Identidad',  8,  '^\d{8}$'),
    ('CE',       'Carné de Extranjería',              9,  '^\d{9}$'),
    ('RUC',      'Registro Único de Contribuyentes', 11,  '^\d{11}$'),
    ('PASAPORTE','Pasaporte',                        NULL, '^[A-Z0-9]{6,12}$');

-- Métodos de pago
INSERT INTO payment_methods (name, method_type, is_active) VALUES
    ('Efectivo',             'EFECTIVO',           TRUE),
    ('Transferencia Bancaria','TRANSFERENCIA',      TRUE),
    ('Yape',                 'YAPE',               TRUE),
    ('Plin',                 'PLIN',               TRUE),
    ('Tarjeta',              'TARJETA',            TRUE),
    ('Depósito en Ventanilla','DEPOSITO_VENTANILLA',TRUE);

-- Empresa (placeholder — se actualiza vía US-39)
INSERT INTO company (legal_name, trade_name, tax_id, legal_address)
VALUES ('Mi Empresa Financiera S.A.C.', 'FinCredit', '00000000000', 'Lima, Perú');

-- Usuario administrador inicial (contraseña debe ser hasheada en la app)
-- password_hash: placeholder — la aplicación debe sobrescribir con BCrypt/Argon2id
INSERT INTO users (email, password_hash, full_name, role)
VALUES ('admin@sistema.local', '$2b$12$PLACEHOLDER_HASH', 'Administrador Sistema', 'ROLE_ADMIN');

-- =============================================================================
-- COMENTARIOS EN TABLAS (documentación inline)
-- =============================================================================
COMMENT ON TABLE company                IS 'Datos corporativos e institucionales de la entidad financiera (US-39).';
COMMENT ON TABLE currencies             IS 'Catálogo ISO 4217 de monedas habilitadas.';
COMMENT ON TABLE branches               IS 'Sucursales y agencias físicas (US-40).';
COMMENT ON TABLE zones                  IS 'Zonas geográficas de cobertura de cobranza (US-40).';
COMMENT ON TABLE holidays               IS 'Calendario de feriados para ajuste de vencimientos (US-41).';
COMMENT ON TABLE document_types         IS 'Catálogo configurable de tipos de documento (US-42).';
COMMENT ON TABLE payment_methods        IS 'Métodos de pago habilitados (US-42).';
COMMENT ON TABLE legal_templates        IS 'Plantillas versionadas de contratos y pagarés (US-42, US-15).';
COMMENT ON TABLE users                  IS 'Usuarios del backoffice y portal cliente (US-01, US-02, US-43).';
COMMENT ON TABLE refresh_tokens         IS 'Tokens de sesión revocables (US-01).';
COMMENT ON TABLE audit_logs             IS 'Bitácora append-only de auditoría (US-03). UPDATE/DELETE prohibidos por trigger.';
COMMENT ON TABLE customers              IS 'Clientes prestatarios (US-04, US-05, US-06).';
COMMENT ON TABLE customer_documents     IS 'Expediente digital KYC del cliente (US-05).';
COMMENT ON TABLE otp_verifications      IS 'Códigos OTP para verificación de portal cliente (US-26).';
COMMENT ON TABLE loan_products          IS 'Plantillas de productos de crédito configurables (US-07, US-08, US-09).';
COMMENT ON TABLE loan_applications      IS 'Solicitudes de crédito con evaluación y dictamen (US-10, US-12).';
COMMENT ON TABLE loans                  IS 'Préstamos desembolsados con parámetros congelados (US-13).';
COMMENT ON TABLE schedules              IS 'Versiones de cronograma inmutable por préstamo (US-14, US-31).';
COMMENT ON TABLE installments           IS 'Cuotas del cronograma — proyectadas inmutables, realizadas acumulables (US-14).';
COMMENT ON TABLE payments               IS 'Pagos registrados, inmutables. Reversos via REVERSAL kind (US-16, US-18).';
COMMENT ON TABLE payment_allocations    IS 'Detalle de cascada de prelación mora→interés→comisión→capital (US-16).';
COMMENT ON TABLE transactions           IS 'Libro mayor / ledger financiero inmutable (US-13, US-16).';
COMMENT ON TABLE cash_boxes             IS 'Cajas físicas asignadas a sucursales (US-23).';
COMMENT ON TABLE cash_sessions          IS 'Sesiones de apertura y cierre de caja con arqueo ciego (US-23).';
COMMENT ON TABLE cash_movements         IS 'Movimientos de efectivo extraordinarios en caja (US-24).';
COMMENT ON TABLE cash_transfers         IS 'Transferencias two-phase entre cajas (US-25).';
COMMENT ON TABLE bank_accounts          IS 'Cuentas bancarias de la empresa para conciliación (US-37).';
COMMENT ON TABLE collection_actions     IS 'Gestiones y promesas de pago en cobranza (US-21).';
COMMENT ON TABLE integration_events     IS 'Eventos de webhooks bancarios con reintentos exponenciales (US-36, US-38).';
COMMENT ON TABLE reconciliations        IS 'Conciliación de extractos bancarios vs pagos internos (US-37).';
COMMENT ON TABLE notifications          IS 'Cola de notificaciones omnicanal al cliente (US-19, US-21).';
COMMENT ON TABLE prepayment_quotes      IS 'Cotizaciones de liquidación anticipada con vigencia (US-30).';
