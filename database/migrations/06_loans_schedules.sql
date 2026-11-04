-- =============================================================================
-- MIGRACIÓN 06 — PRÉSTAMOS, CRONOGRAMAS Y CUOTAS
-- Dominio: US-13 (Desembolso), US-14 (Cronograma Snapshot), US-15 (Contrato)
--          US-31 (Reestructuración — versionado de schedule)
-- Dependencias: 05_loan_applications.sql, 08_treasury.sql (cashbox_id FK diferida)
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

    -- ── Desembolso ───────────────────────────────────────────────────────────
    cashbox_id                  UUID,
    -- FK → cash_boxes (añadida en migración 08 para evitar dependencia circular)
    disbursed_by                UUID                REFERENCES users(id),

    -- ── Idempotencia del desembolso (US-13, RF-13.2) ────────────────────────
    idempotency_key             VARCHAR(100)        UNIQUE,

    created_at                  TIMESTAMPTZ         NOT NULL DEFAULT NOW(),
    updated_at                  TIMESTAMPTZ         NOT NULL DEFAULT NOW()
);

COMMENT ON TABLE loans IS
    'Préstamos desembolsados. Los parámetros financieros quedan congelados (snapshot) al momento del desembolso (US-13, RNF-06).';
COMMENT ON COLUMN loans.disbursed_amount IS
    'Monto neto entregado al cliente. Puede diferir de principal si la comisión es deductiva (RF-13.5).';
COMMENT ON COLUMN loans.idempotency_key IS
    'Garantiza que reintentos de red no dupliquen desembolsos (RF-13.2, US-13, CA-13.2).';

CREATE TRIGGER tg_loans_updated_at
    BEFORE UPDATE ON loans
    FOR EACH ROW EXECUTE FUNCTION fn_set_updated_at();

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
