-- =============================================================================
-- MIGRACIÓN 02 — SEGURIDAD, USUARIOS Y AUDITORÍA
-- Modelo: Fintech 100% Digital
-- Dominio: US-01 (Autenticación), US-02 (RBAC), US-03 (AuditLog), US-43
-- ELIMINADO: branch_id en users, tabla user_zones (sin sucursales ni zonas)
-- Dependencias: 01_admin_config.sql
-- =============================================================================

-- ─────────────────────────────────────────────────────────────────────────────
-- Usuarios del sistema — backoffice y portal cliente (US-01, US-43)
-- Sin branch_id: la fintech es 100% digital, operación centralizada
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE users (
    id                  UUID             PRIMARY KEY DEFAULT gen_random_uuid(),
    email               VARCHAR(150)     NOT NULL UNIQUE,
    password_hash       VARCHAR(255)     NOT NULL,            -- BCrypt / Argon2id con salt
    full_name           VARCHAR(200)     NOT NULL,
    phone               VARCHAR(20),
    role                user_role        NOT NULL,
    status              user_status_type NOT NULL DEFAULT 'ACTIVE',
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
    'Usuarios del backoffice y portal cliente con control de roles RBAC (US-01, US-02, US-43). Sin branch_id: operación 100% digital centralizada.';
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
-- Refresh Tokens — revocables individualmente por dispositivo (US-01, RF-01.2)
-- Permite sesión simultánea en celular y laptop sin exponer credenciales
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE refresh_tokens (
    id          UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id     UUID         NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    token_hash  VARCHAR(255) NOT NULL UNIQUE,  -- Hash del token; nunca el token en claro
    issued_at   TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    expires_at  TIMESTAMPTZ  NOT NULL,
    revoked_at  TIMESTAMPTZ,                   -- NULL = aún vigente
    user_ip     INET,
    user_agent  TEXT                           -- 'Chrome/Windows', 'App Android 14', etc.
);

COMMENT ON TABLE refresh_tokens IS
    'Refresh Tokens rotativos e invalidables individualmente por dispositivo (US-01, RF-01.2, RF-01.4).';

-- ─────────────────────────────────────────────────────────────────────────────
-- Bitácora de auditoría inmutable (US-03)
-- Todo cambio en entidades financieras sensibles queda registrado aquí
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE audit_logs (
    id           BIGSERIAL    PRIMARY KEY,
    entity_name  VARCHAR(100) NOT NULL,    -- Nombre de la tabla afectada: 'loans', 'payments'
    entity_id    VARCHAR(100) NOT NULL,    -- ID del registro afectado
    action       VARCHAR(50)  NOT NULL,    -- 'CREATE', 'UPDATE', 'APPROVE', 'DISBURSE', 'REVERSE'
    state_before JSONB,                    -- Snapshot del estado anterior (NULL si es INSERT)
    state_after  JSONB,                    -- Snapshot del estado nuevo
    user_id      UUID         REFERENCES users(id),
    user_ip      INET,                     -- IP desde donde se realizó la acción
    created_at   TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

COMMENT ON TABLE audit_logs IS
    'Bitácora inmutable de auditoría. SOLO INSERT. Nunca UPDATE ni DELETE (US-03, RF-03.1).';
COMMENT ON COLUMN audit_logs.state_before IS
    'Estado del registro ANTES de la modificación en formato JSONB. Permite reconstruir cualquier estado histórico.';
COMMENT ON COLUMN audit_logs.state_after IS
    'Estado del registro DESPUÉS de la modificación. Junto con state_before forma la pista de auditoría completa.';
