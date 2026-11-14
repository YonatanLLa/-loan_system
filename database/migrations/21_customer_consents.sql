-- =============================================================================
-- MIGRACIÓN 21 — CONSENTIMIENTOS POR FINALIDAD Y VERSIÓN (LEY 29733)
-- Modelo: Fintech 100% Digital
-- Dominio: US-04, BE-CUS-07
-- Dependencias: 03_customers_kyc.sql
-- =============================================================================

DO $$
BEGIN
    CREATE TYPE consent_purpose AS ENUM ('DATA_PROCESSING', 'CREDIT_BUREAU_QUERY', 'MARKETING');
EXCEPTION
    WHEN duplicate_object THEN NULL;
END $$;

CREATE TABLE IF NOT EXISTS consent_texts (
    id           UUID            PRIMARY KEY DEFAULT gen_random_uuid(),
    purpose      consent_purpose NOT NULL,
    version      VARCHAR(20)     NOT NULL,       -- '2026-01', 'v1', ...
    body         TEXT            NOT NULL,
    sha256       VARCHAR(64)     NOT NULL,
    is_current   BOOLEAN         NOT NULL DEFAULT FALSE,
    published_at TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_consent_texts_purpose_version UNIQUE (purpose, version)
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_consent_texts_current
    ON consent_texts (purpose)
    WHERE is_current;

CREATE TABLE IF NOT EXISTS customer_consents (
    id              UUID            PRIMARY KEY DEFAULT gen_random_uuid(),
    customer_id     UUID            NOT NULL REFERENCES customers(id) ON DELETE CASCADE,
    consent_text_id UUID            NOT NULL REFERENCES consent_texts(id),
    purpose         consent_purpose NOT NULL,
    granted_at      TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    granted_ip      INET,
    user_agent      TEXT,
    channel         VARCHAR(20)     NOT NULL,       -- 'BACKOFFICE' | 'PORTAL'
    revoked_at      TIMESTAMPTZ,
    revoked_ip      INET
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_customer_consents_active
    ON customer_consents (customer_id, purpose)
    WHERE revoked_at IS NULL;

CREATE INDEX IF NOT EXISTS ix_customer_consents_customer
    ON customer_consents (customer_id, granted_at DESC);

-- Columnas previas en customers quedan deprecadas (no se eliminan por compatibilidad)
COMMENT ON COLUMN customers.consent_given_at IS
    'DEPRECADA (BE-CUS-07): el consentimiento vigente por finalidad y versión se gestiona en customer_consents.';
COMMENT ON COLUMN customers.consent_ip IS
    'DEPRECADA (BE-CUS-07): la IP de aceptación se registra en customer_consents.granted_ip.';
