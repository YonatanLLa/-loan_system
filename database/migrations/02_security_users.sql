-- =============================================================================
-- MIGRACIÓN 02 — SEGURIDAD, USUARIOS Y AUDITORÍA
-- Dominio: US-01 (Autenticación), US-02 (RBAC), US-03 (AuditLog), US-43
-- Dependencias: 01_admin_config.sql
-- =============================================================================

-- ─────────────────────────────────────────────────────────────────────────────
-- Usuarios del sistema — backoffice y portal cliente (US-01, US-43)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE users (
    id                  UUID             PRIMARY KEY DEFAULT gen_random_uuid(),
    email               VARCHAR(150)     NOT NULL UNIQUE,
    password_hash       VARCHAR(255)     NOT NULL,            -- BCrypt / Argon2id con salt
    full_name           VARCHAR(200)     NOT NULL,
    phone               VARCHAR(20),
    role                user_role        NOT NULL,
    status              user_status_type NOT NULL DEFAULT 'ACTIVE',
    branch_id           UUID             REFERENCES branches(id),
    -- Control de intentos fallidos (RF-01.3: bloqueo tras 5 intentos)
    failed_login_count  SMALLINT         NOT NULL DEFAULT 0,
    locked_until        TIMESTAMPTZ,
    last_login_at       TIMESTAMPTZ,
    -- Flujo de invitación (US-43, RF-43.2)
    invitation_token    VARCHAR(255),
    invitation_sent_at  TIMESTAMPTZ,
    password_changed_at TIMESTAMPTZ,
    created_at          TIMESTAMPTZ      NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ      NOT NULL DEFAULT NOW()
);

COMMENT ON TABLE users IS
    'Usuarios del backoffice y portal cliente con control de roles RBAC (US-01, US-02, US-43).';
COMMENT ON COLUMN users.password_hash IS
    'Hash con BCrypt o Argon2id. Nunca almacenar la contraseña en texto plano.';
COMMENT ON COLUMN users.failed_login_count IS
    'Se incrementa con cada intento fallido. Se reinicia al hacer login exitoso (RF-01.3).';

CREATE TRIGGER tg_users_updated_at
    BEFORE UPDATE ON users
    FOR EACH ROW EXECUTE FUNCTION fn_set_updated_at();

-- FK diferida: legal_templates.created_by → users
ALTER TABLE legal_templates
    ADD CONSTRAINT fk_template_user
    FOREIGN KEY (created_by) REFERENCES users(id);

-- ─────────────────────────────────────────────────────────────────────────────
-- Refresh Tokens — revocables individualmente (US-01, RF-01.2)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE refresh_tokens (
    id          UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id     UUID        NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    token_hash  VARCHAR(255) NOT NULL UNIQUE,  -- Hash del token; nunca el token en claro
    issued_at   TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    expires_at  TIMESTAMPTZ NOT NULL,
    revoked_at  TIMESTAMPTZ,                   -- NULL = aún vigente
    user_ip     INET,
    user_agent  TEXT
);

COMMENT ON TABLE refresh_tokens IS
    'Refresh Tokens rotativos e invalidables individualmente (US-01, RF-01.2, RF-01.4).';

-- ─────────────────────────────────────────────────────────────────────────────
-- Asignación usuario ↔ zona (un cobrador puede cubrir varias zonas)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE user_zones (
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    zone_id UUID NOT NULL REFERENCES zones(id) ON DELETE CASCADE,
    PRIMARY KEY (user_id, zone_id)
);

COMMENT ON TABLE user_zones IS
    'Tabla pivot que asocia gestores/analistas con sus zonas de cobertura (US-40, RF-40.3).';

-- ─────────────────────────────────────────────────────────────────────────────
-- Bitácora de auditoría — APPEND-ONLY (US-03, RF-03.1 → RF-03.3)
-- La aplicación SOLO puede hacer INSERT; UPDATE y DELETE están bloqueados
-- a nivel de base de datos mediante triggers.
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE audit_logs (
    id           BIGSERIAL    PRIMARY KEY,
    entity_name  VARCHAR(100) NOT NULL,         -- 'LoanApplication', 'Payment', etc.
    entity_id    VARCHAR(50)  NOT NULL,         -- ID (UUID o bigint) de la entidad afectada
    action       VARCHAR(30)  NOT NULL,         -- CREATE | UPDATE | DELETE | APPROVE | REVERSE
    state_before JSONB,                         -- Estado previo serializado
    state_after  JSONB,                         -- Estado posterior serializado
    user_id      UUID         REFERENCES users(id),
    user_ip      INET,
    user_agent   TEXT,
    created_at   TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

COMMENT ON TABLE audit_logs IS
    'Bitácora append-only e inmutable. UPDATE y DELETE están bloqueados por trigger (US-03, RF-03.3).';

-- Triggers de inmutabilidad — bloquean UPDATE y DELETE
CREATE TRIGGER tg_audit_log_no_update
    BEFORE UPDATE ON audit_logs
    FOR EACH ROW EXECUTE FUNCTION fn_audit_log_immutable();

CREATE TRIGGER tg_audit_log_no_delete
    BEFORE DELETE ON audit_logs
    FOR EACH ROW EXECUTE FUNCTION fn_audit_log_immutable();
