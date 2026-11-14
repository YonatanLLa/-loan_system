-- =============================================================================
-- MIGRACIÓN 19 — CUSTOMERS: EXTENSIONES KYC, PEP Y VERIFICACIONES AUTOMÁTICAS
-- Modelo: Fintech 100% Digital
-- Dominio: US-04, US-05, BE-CUS-01, BE-CUS-04, BE-CUS-05, DB-21, DB-23
-- Dependencias: 00_extensions_and_enums.sql, 02_security_users.sql, 03_customers_kyc.sql
-- =============================================================================

-- ── Nivel de riesgo AML del cliente ───────────────────────────────────────────
DO $$
BEGIN
    CREATE TYPE aml_risk_level AS ENUM ('LOW', 'MEDIUM', 'HIGH');
EXCEPTION
    WHEN duplicate_object THEN NULL;
END $$;

-- ── Extensiones sobre la tabla customers ──────────────────────────────────────
ALTER TABLE customers
    ADD COLUMN IF NOT EXISTS is_pep              BOOLEAN        NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS aml_risk_level      aml_risk_level NOT NULL DEFAULT 'LOW',
    ADD COLUMN IF NOT EXISTS email_verified_at   TIMESTAMPTZ,
    ADD COLUMN IF NOT EXISTS phone_verified_at   TIMESTAMPTZ,
    ADD COLUMN IF NOT EXISTS kyc_rejected_reason VARCHAR(500),
    ADD COLUMN IF NOT EXISTS kyc_submitted_at    TIMESTAMPTZ,          -- cuándo el expediente quedó listo para revisión
    ADD COLUMN IF NOT EXISTS version             INTEGER        NOT NULL DEFAULT 0;  -- concurrencia optimista

COMMENT ON COLUMN customers.is_pep IS 'Indica si el cliente fue marcado como Persona Expuesta Políticamente (BE-CUS-04/05).';
COMMENT ON COLUMN customers.aml_risk_level IS 'Nivel de riesgo LAFT/AML del cliente: LOW, MEDIUM o HIGH.';
COMMENT ON COLUMN customers.kyc_submitted_at IS 'Instante UTC en que el expediente KYC pasó a revisión (PENDING*).';
COMMENT ON COLUMN customers.version IS 'Token de concurrencia optimista incrementado en cada actualización del cliente.';

-- ── Tipos y tabla de verificaciones automáticas KYC ───────────────────────────
DO $$
BEGIN
    CREATE TYPE kyc_check_type AS ENUM (
        'IDENTITY_REGISTRY',
        'BIOMETRIC_LIVENESS',
        'FACE_MATCH',
        'PEP_SCREENING',
        'AML_SANCTIONS'
    );
EXCEPTION
    WHEN duplicate_object THEN NULL;
END $$;

DO $$
BEGIN
    CREATE TYPE kyc_check_result AS ENUM ('PENDING', 'PASS', 'FAIL', 'REVIEW', 'ERROR');
EXCEPTION
    WHEN duplicate_object THEN NULL;
END $$;

CREATE TABLE IF NOT EXISTS kyc_verifications (
    id                  UUID              PRIMARY KEY DEFAULT gen_random_uuid(),
    customer_id         UUID              NOT NULL REFERENCES customers(id) ON DELETE CASCADE,
    check_type          kyc_check_type    NOT NULL,
    provider            VARCHAR(50)       NOT NULL,         -- 'FAKE', 'RENIEC', 'METAMAP', ...
    result              kyc_check_result  NOT NULL DEFAULT 'PENDING',
    score               NUMERIC(5,2),                       -- 0-100 cuando aplique
    external_reference  VARCHAR(100),
    raw_response        JSONB,                              -- respuesta cruda (sin imágenes)
    error_message       VARCHAR(500),
    requested_at        TIMESTAMPTZ       NOT NULL DEFAULT NOW(),
    completed_at        TIMESTAMPTZ,
    requested_by        UUID              REFERENCES users(id),
    CONSTRAINT uq_kyc_verifications_provider_ref UNIQUE (provider, external_reference)
);

CREATE INDEX IF NOT EXISTS ix_kyc_verifications_customer
    ON kyc_verifications (customer_id, check_type, requested_at DESC);

COMMENT ON TABLE kyc_verifications IS
    'Resultados de verificaciones automáticas KYC (RENIEC/biometría/listas PEP-AML) ejecutadas vía IIdentityVerifier (BE-CUS-05).';
