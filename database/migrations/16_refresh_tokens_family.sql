-- =============================================================================
-- MIGRACIÓN 16 — REFRESH TOKENS: FAMILIAS Y DETECCIÓN DE REÚSO
-- Modelo: Fintech 100% Digital
-- Dominio: US-01, RF-01.2, BE-SEC-01 (refresh token rotativo y revocable)
-- Cada login abre una "familia" (sesión). Cada rotación emite el siguiente token
-- de la misma familia y enlaza el anterior con su reemplazo. Si se presenta un
-- token ya rotado, se revoca la familia completa.
-- Ver backend/docs/DECISION_02_REFRESH_TOKEN_REUSE_DETECTION.md
-- Dependencias: 02_security_users.sql
-- =============================================================================

-- ── family_id: sesión a la que pertenece el token ──────────────────────────────
ALTER TABLE refresh_tokens ADD COLUMN IF NOT EXISTS family_id UUID;

-- Los tokens existentes no tienen historial: cada uno es su propia familia.
UPDATE refresh_tokens SET family_id = id WHERE family_id IS NULL;

ALTER TABLE refresh_tokens ALTER COLUMN family_id SET NOT NULL;

-- ── replaced_by_token_id: token emitido al rotar este ──────────────────────────
ALTER TABLE refresh_tokens ADD COLUMN IF NOT EXISTS replaced_by_token_id UUID;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'fk_refresh_tokens_replaced_by'
    ) THEN
        -- DEFERRABLE: el INSERT del token nuevo y el UPDATE del anterior van en la
        -- misma transacción; la FK se valida al hacer COMMIT, sin depender del orden.
        ALTER TABLE refresh_tokens
            ADD CONSTRAINT fk_refresh_tokens_replaced_by
            FOREIGN KEY (replaced_by_token_id) REFERENCES refresh_tokens(id)
            ON DELETE SET NULL
            DEFERRABLE INITIALLY DEFERRED;
    END IF;
END $$;

-- ── Índice para revocar la familia completa ────────────────────────────────────
CREATE INDEX IF NOT EXISTS idx_refresh_tokens_family_id ON refresh_tokens(family_id);

COMMENT ON COLUMN refresh_tokens.family_id IS
    'Familia (sesión) del token: id del token emitido en el login, heredado en cada rotación. Si se reúsa un token ya rotado se revoca toda la familia (BE-SEC-01).';

COMMENT ON COLUMN refresh_tokens.replaced_by_token_id IS
    'Token que reemplazó a este al rotarlo. NULL = nunca rotado (vigente, cerrado por logout o revocado por suspensión). Si no es NULL y el token se vuelve a presentar, hay reúso.';
