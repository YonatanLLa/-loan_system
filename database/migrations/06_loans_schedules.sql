-- =============================================================================
-- MIGRACIÓN 06 — PRÉSTAMOS, CRONOGRAMAS Y CUOTAS
-- Modelo: Fintech 100% Digital
-- Dominio: US-13 (Desembolso Digital), US-14 (Cronograma Snapshot), US-15 (Contrato)
--          US-31 (Reestructuración — versionado de schedule)
-- ELIMINADO: cashbox_id (sin cajas físicas de efectivo)
-- AGREGADO:  disbursement_bank_account_id (cuenta de la fintech que envió el dinero)
--            customer_bank_account_id (cuenta del cliente que recibió el dinero)
-- Dependencias: 05_loan_applications.sql, 03_customers_kyc.sql, 01_admin_config.sql
-- =============================================================================

-- ─────────────────────────────────────────────────────────────────────────────
-- Préstamo activo — parámetros congelados al momento del desembolso (US-13)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE loans (
    id                          UUID                PRIMARY KEY DEFAULT gen_random_uuid(),
    -- Trazabilidad de origen
    application_id              UUID                UNIQUE REFERENCES loan_applications(id),
    customer_id                 UUID                NOT NULL REFERENCES customers(id),
    product_id                  UUID                NOT NULL REFERENCES loan_products(id),

    -- ── Parámetros financieros CONGELADOS al desembolsar (snapshot) ──────────
    -- RNF-06: los cambios en loan_products NO deben afectar préstamos vigentes
    principal                   NUMERIC(18,4)       NOT NULL CHECK (principal > 0),
    disbursed_amount            NUMERIC(18,4)       NOT NULL,
    -- Neto entregado (= principal - comisión deductiva, si aplica)
    annual_rate                 NUMERIC(12,6)       NOT NULL,
    amortization_method         amortization_method NOT NULL,
    frequency                   payment_frequency   NOT NULL,
    term                        SMALLINT            NOT NULL,
    grace_days                  SMALLINT            NOT NULL DEFAULT 0,
    mora_rate_per_day           NUMERIC(12,6)       NOT NULL DEFAULT 0,
    mora_base                   mora_base_type      NOT NULL DEFAULT 'SALDO_CAPITAL_VENCIDO',
    opening_fee_amount          NUMERIC(18,4)       NOT NULL DEFAULT 0,
    periodic_fee                NUMERIC(18,4)       NOT NULL DEFAULT 0,
    prepayment_penalty_rate     NUMERIC(12,6)       NOT NULL DEFAULT 0,

    -- ── Fechas clave ─────────────────────────────────────────────────────────
    disbursement_date           DATE                NOT NULL,
    -- Inicio del cómputo de intereses (RF-13.4, RF-14.1)
    first_due_date              DATE                NOT NULL,

    -- ── Totales proyectados (calculados al generar el cronograma) ────────────
    total_scheduled_interest    NUMERIC(18,4)       NOT NULL DEFAULT 0,
    total_scheduled_fees        NUMERIC(18,4)       NOT NULL DEFAULT 0,

    -- ── Acumulados realizados por pagos recibidos ────────────────────────────
    total_paid_principal        NUMERIC(18,4)       NOT NULL DEFAULT 0,
    total_paid_interest         NUMERIC(18,4)       NOT NULL DEFAULT 0,
    total_paid_fees             NUMERIC(18,4)       NOT NULL DEFAULT 0,
    total_paid_mora             NUMERIC(18,4)       NOT NULL DEFAULT 0,

    -- ── Estado del préstamo (US-13, sección 3.9 del README) ─────────────────
    status                      loan_status         NOT NULL DEFAULT 'VIGENTE',
    close_reason                loan_close_reason,
    closed_at                   TIMESTAMPTZ,

    -- ── Refinanciamiento / Consolidación (US-32) ────────────────────────────
    purpose_type                loan_purpose_type   NOT NULL DEFAULT 'CONSUMO',
    refinanced_from_loan_id     UUID                REFERENCES loans(id),

    -- ── Desembolso Digital (reemplaza cashbox_id) ────────────────────────────
    -- Cuenta de la Fintech desde donde salió la transferencia
    disbursement_bank_account_id UUID               REFERENCES company_bank_accounts(id),
    -- Cuenta del cliente que recibió el depósito
    customer_bank_account_id    UUID                REFERENCES customer_bank_accounts(id),
    disbursed_by                UUID                REFERENCES users(id),

    -- ── Idempotencia del desembolso (US-13, RF-13.2) ────────────────────────
    idempotency_key             VARCHAR(100)        UNIQUE,

    created_at                  TIMESTAMPTZ         NOT NULL DEFAULT NOW(),
    updated_at                  TIMESTAMPTZ         NOT NULL DEFAULT NOW()
);

COMMENT ON TABLE loans IS
    'Préstamos desembolsados digitalmente. Los parámetros financieros quedan congelados (snapshot) al momento del desembolso (US-13, RNF-06).';
COMMENT ON COLUMN loans.disbursed_amount IS
    'Monto neto transferido al cliente. Puede diferir de principal si la comisión es deductiva (RF-13.5).';
COMMENT ON COLUMN loans.disbursement_bank_account_id IS
    'Cuenta bancaria de la Fintech desde la cual se realizó la transferencia al cliente. Reemplaza cashbox_id.';
COMMENT ON COLUMN loans.customer_bank_account_id IS
    'Cuenta bancaria/tarjeta/billetera del cliente que recibió el desembolso.';
COMMENT ON COLUMN loans.idempotency_key IS
    'Garantiza que reintentos de red no dupliquen desembolsos (RF-13.2, US-13, CA-13.2).';

CREATE TRIGGER tg_loans_updated_at
    BEFORE UPDATE ON loans
    FOR EACH ROW EXECUTE FUNCTION fn_set_updated_at();

-- ─────────────────────────────────────────────────────────────────────────────
-- Desembolsos bancarios — registro detallado de cada transferencia al cliente
-- Nuevo en modelo digital: registra el estado de la operación bancaria
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE disbursements (
    id                          UUID                 PRIMARY KEY DEFAULT gen_random_uuid(),
    loan_id                     UUID                 NOT NULL UNIQUE REFERENCES loans(id),
    -- Origen (Fintech) y Destino (Cliente)
    company_bank_account_id     UUID                 NOT NULL REFERENCES company_bank_accounts(id),
    customer_bank_account_id    UUID                 NOT NULL REFERENCES customer_bank_accounts(id),
    -- Monto y moneda
    amount                      NUMERIC(18,4)        NOT NULL CHECK (amount > 0),
    currency_id                 UUID                 NOT NULL REFERENCES currencies(id),
    -- Canal de transferencia
    channel                     disbursement_channel NOT NULL,
    -- Estado del proceso bancario
    status                      disbursement_status  NOT NULL DEFAULT 'PENDING',
    -- Referencias bancarias para conciliación
    operation_number            VARCHAR(100),    -- Número de operación del banco (llega por webhook)
    external_reference          VARCHAR(150),    -- Referencia de la pasarela o banco
    bank_response_code          VARCHAR(20),     -- Código de respuesta del banco ('00' = éxito)
    bank_response_message       TEXT,            -- Mensaje de error si status=FAILED
    -- Trazabilidad temporal
    initiated_at                TIMESTAMPTZ      NOT NULL DEFAULT NOW(),
    confirmed_at                TIMESTAMPTZ,     -- Cuando el banco confirmó la transferencia
    failed_at                   TIMESTAMPTZ,     -- Cuando el banco rechazó la operación
    -- Idempotencia
    idempotency_key             VARCHAR(100)     UNIQUE,
    initiated_by                UUID             REFERENCES users(id)
);

COMMENT ON TABLE disbursements IS
    'Registro de cada transferencia bancaria de desembolso. Permite rastrear el estado desde PENDING hasta COMPLETED o FAILED (US-13, RF-13.3).';
COMMENT ON COLUMN disbursements.operation_number IS
    'Número de operación bancaria recibido por webhook de confirmación. Usado para conciliación (US-37).';

-- ─────────────────────────────────────────────────────────────────────────────
-- Versiones del cronograma — inmutable tras el desembolso (US-14)
-- Permite múltiples versiones por reestructuración (US-31, RF-31.3)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE schedules (
    id               UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    loan_id          UUID        NOT NULL REFERENCES loans(id),
    snapshot_version SMALLINT    NOT NULL DEFAULT 1,
    -- v1 = cronograma original; v2+ = tras reestructuración
    generated_at     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    is_active        BOOLEAN     NOT NULL DEFAULT TRUE,
    -- Solo un schedule activo por préstamo (garantizado por trigger)
    notes            TEXT,
    UNIQUE (loan_id, snapshot_version)
);

COMMENT ON TABLE schedules IS
    'Versiones inmutables del cronograma. v1 = original; v2+ = reestructurado (US-14, US-31, RF-31.3).';

-- Trigger: desactiva versiones anteriores al activar una nueva
CREATE TRIGGER tg_schedules_single_active
    BEFORE INSERT OR UPDATE ON schedules
    FOR EACH ROW EXECUTE FUNCTION fn_enforce_single_active_schedule();

-- ─────────────────────────────────────────────────────────────────────────────
-- Cuotas del cronograma (US-14, RF-14.4 / RF-14.5)
--
-- REGLA CLAVE (RNF-11):
--   Los campos proyectados (principal, interest, fee, total_due) son INMUTABLES.
--   Los campos paid_* y late_fee son los únicos actualizables (acumuladores).
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE installments (
    id                  UUID                PRIMARY KEY DEFAULT gen_random_uuid(),
    schedule_id         UUID                NOT NULL REFERENCES schedules(id),
    loan_id             UUID                NOT NULL REFERENCES loans(id),
    installment_number  SMALLINT            NOT NULL,

    -- ── Proyectados — INMUTABLES tras desembolso ────────────────────────────
    due_date            DATE                NOT NULL,
    principal           NUMERIC(18,4)       NOT NULL CHECK (principal >= 0),
    interest            NUMERIC(18,4)       NOT NULL CHECK (interest  >= 0),
    fee                 NUMERIC(18,4)       NOT NULL DEFAULT 0,
    total_due           NUMERIC(18,4)       NOT NULL,
    -- total_due = principal + interest + fee

    -- ── Realizados — acumuladores actualizables por pagos ───────────────────
    paid_principal      NUMERIC(18,4)       NOT NULL DEFAULT 0
                                            CHECK (paid_principal  >= 0),
    paid_interest       NUMERIC(18,4)       NOT NULL DEFAULT 0
                                            CHECK (paid_interest   >= 0),
    paid_fee            NUMERIC(18,4)       NOT NULL DEFAULT 0
                                            CHECK (paid_fee        >= 0),
    late_fee            NUMERIC(18,4)       NOT NULL DEFAULT 0
                                            CHECK (late_fee        >= 0),
    -- Mora devengada calculada por el job diario
    paid_late_fee       NUMERIC(18,4)       NOT NULL DEFAULT 0
                                            CHECK (paid_late_fee   >= 0),

    -- ── Estado y mora ────────────────────────────────────────────────────────
    status              installment_status  NOT NULL DEFAULT 'PENDING',
    days_late           SMALLINT            NOT NULL DEFAULT 0,
    last_mora_calc_date DATE,
    -- Idempotencia del job diario de mora (US-19, RF-19.5)

    UNIQUE (loan_id, installment_number)
);

COMMENT ON TABLE installments IS
    'Cuotas del cronograma. Campos proyectados = inmutables; paid_* = acumuladores de pagos (US-14, RNF-11).';
COMMENT ON COLUMN installments.late_fee IS
    'Mora devengada calculada por el job nocturno (US-19). No capitaliza salvo allows_anatocismo=true.';
COMMENT ON COLUMN installments.last_mora_calc_date IS
    'Garantiza idempotencia del job diario: si ya se calculó hoy, no recalcula (RF-19.5).';

-- ─────────────────────────────────────────────────────────────────────────────
-- Cotizaciones de cancelación anticipada (US-30, RF-30.1 → RF-30.4)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE prepayment_quotes (
    id                    UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    loan_id               UUID        NOT NULL REFERENCES loans(id),
    as_of_date            DATE        NOT NULL,
    outstanding_principal NUMERIC(18,4) NOT NULL,
    accrued_interest      NUMERIC(18,4) NOT NULL DEFAULT 0,
    accrued_mora          NUMERIC(18,4) NOT NULL DEFAULT 0,
    prepayment_penalty    NUMERIC(18,4) NOT NULL DEFAULT 0,
    total_to_pay          NUMERIC(18,4) NOT NULL,
    quote_valid_until     DATE        NOT NULL,     -- Tipicamente fin de jornada (RF-30.3)
    is_applied            BOOLEAN     NOT NULL DEFAULT FALSE,
    generated_by          UUID        REFERENCES users(id),
    created_at            TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

COMMENT ON TABLE prepayment_quotes IS
    'Cotizaciones de liquidación anticipada con vigencia hasta fin de jornada (US-30, RF-30.1 → RF-30.4).';
