-- =============================================================================
-- MIGRACIÓN 16 — MFA (TOTP) PARA USUARIOS CON ROLES PRIVILEGIADOS (BE-SEC-03, DB-18)
-- Modelo: Fintech 100% Digital
-- Dependencias: 02_security_users.sql
-- =============================================================================

-- 1) Estado de MFA en users (DB-18)
ALTER TABLE users
    ADD COLUMN mfa_enabled          BOOLEAN     NOT NULL DEFAULT FALSE,
    ADD COLUMN mfa_secret_encrypted TEXT,
    ADD COLUMN mfa_enrolled_at      TIMESTAMPTZ,
    ADD COLUMN mfa_last_used_step   BIGINT;

ALTER TABLE users ADD CONSTRAINT ck_users_mfa_secret
    CHECK (NOT mfa_enabled OR mfa_secret_encrypted IS NOT NULL);

COMMENT ON COLUMN users.mfa_secret_encrypted IS
    'Secreto TOTP cifrado con AES-GCM (v1:<base64>). Nunca almacenar en texto plano.';
COMMENT ON COLUMN users.mfa_last_used_step IS
    'Último paso TOTP aceptado (anti-replay dentro de la ventana de deriva).';

-- 2) Desafíos del segundo paso del login (un solo uso, corta duración)
CREATE TABLE mfa_challenges (
    id          UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id     UUID         NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    token_hash  VARCHAR(255) NOT NULL UNIQUE,
    purpose     VARCHAR(20)  NOT NULL CHECK (purpose IN ('VERIFY', 'ENROLL')),
    attempts    SMALLINT     NOT NULL DEFAULT 0,
    expires_at  TIMESTAMPTZ  NOT NULL,
    consumed_at TIMESTAMPTZ,
    user_ip     INET,
    user_agent  TEXT,
    created_at  TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE INDEX ix_mfa_challenges_user_active
    ON mfa_challenges (user_id)
    WHERE consumed_at IS NULL;

COMMENT ON TABLE mfa_challenges IS
    'Desafíos temporales de un solo uso para verificación o enrolamiento MFA (BE-SEC-03).';

-- 3) Códigos de recuperación de un solo uso (D6)
CREATE TABLE mfa_recovery_codes (
    id         UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id    UUID         NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    code_hash  VARCHAR(255) NOT NULL,
    used_at    TIMESTAMPTZ,
    created_at TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE INDEX ix_mfa_recovery_codes_user_unused
    ON mfa_recovery_codes (user_id)
    WHERE used_at IS NULL;

COMMENT ON TABLE mfa_recovery_codes IS
    'Códigos de recuperación MFA de un solo uso almacenados como hash SHA-256 (BE-SEC-03).';

-- 4) Revocar sesiones vigentes de roles que pasan a exigir MFA
UPDATE refresh_tokens
   SET revoked_at = NOW()
 WHERE revoked_at IS NULL
   AND user_id IN (SELECT id FROM users WHERE role IN ('ROLE_ADMIN', 'ROLE_OPERATOR'));
