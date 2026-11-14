-- =============================================================================
-- MIGRACIÓN 22 — OTP VERIFICATIONS: PROPÓSITO, REFERENCIA, INTENTOS Y BLOQUEO
-- Modelo: Fintech 100% Digital
-- Dominio: US-04, US-06, BE-CUS-10, BE-CUS-11
-- Dependencias: 03_customers_kyc.sql
-- =============================================================================

DO $$
BEGIN
    CREATE TYPE otp_purpose AS ENUM ('LOGIN', 'CONTACT_VERIFICATION', 'CONTRACT_SIGNATURE', 'ACCOUNT_CHANGE');
EXCEPTION
    WHEN duplicate_object THEN NULL;
END $$;

ALTER TABLE otp_verifications
    ADD COLUMN IF NOT EXISTS purpose        otp_purpose NOT NULL DEFAULT 'CONTACT_VERIFICATION',
    ADD COLUMN IF NOT EXISTS reference_id   UUID,                         -- p. ej. id de la cuenta a cambiar
    ADD COLUMN IF NOT EXISTS attempts       SMALLINT    NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS max_attempts   SMALLINT    NOT NULL DEFAULT 5,
    ADD COLUMN IF NOT EXISTS invalidated_at TIMESTAMPTZ,                  -- reemplazado por uno nuevo o bloqueado
    ADD COLUMN IF NOT EXISTS requester_ip   INET;

COMMENT ON COLUMN otp_verifications.verified_at IS
    'Momento en que el OTP se usó correctamente (un solo uso). NULL = no consumido.';

CREATE INDEX IF NOT EXISTS ix_otp_active
    ON otp_verifications (customer_id, purpose)
    WHERE verified_at IS NULL AND invalidated_at IS NULL;
