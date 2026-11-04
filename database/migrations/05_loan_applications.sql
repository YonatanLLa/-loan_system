-- =============================================================================
-- MIGRACIÓN 05 — ORIGINACIÓN, SOLICITUDES Y EVALUACIÓN CREDITICIA
-- Dominio: US-10 (Solicitud), US-11 (Simulador), US-12 (Motor evaluación)
-- Dependencias: 03_customers_kyc.sql, 04_loan_products.sql
-- =============================================================================

-- ─────────────────────────────────────────────────────────────────────────────
-- Solicitudes de crédito (US-10, US-12)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE loan_applications (
    id                  UUID                PRIMARY KEY DEFAULT gen_random_uuid(),
    customer_id         UUID                NOT NULL REFERENCES customers(id),
    product_id          UUID                NOT NULL REFERENCES loan_products(id),

    -- ── Condiciones solicitadas (US-10, RF-10.1) ────────────────────────────
    requested_amount    NUMERIC(18,4)       NOT NULL CHECK (requested_amount > 0),
    approved_amount     NUMERIC(18,4),                -- Puede diferir si se aprueba con condiciones
    term                SMALLINT            NOT NULL CHECK (term >= 1),
    frequency           payment_frequency   NOT NULL,
    destination         loan_purpose_type   NOT NULL DEFAULT 'CONSUMO',

    -- ── Estados del proceso (US-10, RF-10.3) ────────────────────────────────
    status              application_status  NOT NULL DEFAULT 'SOLICITADO',

    -- ── Evaluación automática (US-12, RF-12.1 / RF-12.2) ───────────────────
    -- DTI = (Cuota proyectada + Otras cuotas activas) / Ingreso neto × 100
    dti_ratio           NUMERIC(8,4),
    score               NUMERIC(8,4),
    system_recommendation credit_decision,

    -- ── Dictamen del analista (US-12, RF-12.3) ──────────────────────────────
    -- RNF-09: una vez APROBADO o RECHAZADO no se puede cambiar sin reapertura
    decision            VARCHAR(30),
    decision_notes      TEXT,                         -- Justificación obligatoria (RF-12.3)
    decision_by         UUID                REFERENCES users(id),
    decided_at          TIMESTAMPTZ,

    -- ── Canal de origen ─────────────────────────────────────────────────────
    originated_from     VARCHAR(20)         NOT NULL DEFAULT 'BACKOFFICE',
    -- BACKOFFICE (analista) | PORTAL (cliente via US-27)

    created_by          UUID                REFERENCES users(id),
    created_at          TIMESTAMPTZ         NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ         NOT NULL DEFAULT NOW()
);

COMMENT ON TABLE loan_applications IS
    'Solicitudes de crédito con evaluación automática (DTI) y dictamen del analista (US-10, US-12).';
COMMENT ON COLUMN loan_applications.dti_ratio IS
    'Ratio Cuota/Ingreso en %. ≤35% → APROBABLE; 35-50% → REVISION_MANUAL; >50% → RECHAZO (RF-12.2).';
COMMENT ON COLUMN loan_applications.decision_notes IS
    'Obligatorio al dictaminar RECHAZADO. El sistema devuelve 422 si se omite (RF-12.3, CA-12.2).';

CREATE TRIGGER tg_loan_applications_updated_at
    BEFORE UPDATE ON loan_applications
    FOR EACH ROW EXECUTE FUNCTION fn_set_updated_at();
