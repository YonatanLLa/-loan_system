-- =============================================================================
-- MIGRACIÓN 09 — MORA, COBRANZA Y NOTIFICACIONES
-- Modelo: Fintech 100% Digital (sin cobradores de campo ni visitas)
-- Dominio: US-19 (Job mora), US-20 (Aging), US-21 (Acciones), US-22 (Castigo)
-- Dependencias: 06_loans_schedules.sql, 02_security_users.sql
-- =============================================================================

-- ─────────────────────────────────────────────────────────────────────────────
-- Acciones de cobranza — gestiones, llamadas y promesas de pago (US-21, RF-21.2)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE collection_actions (
    id              UUID                        PRIMARY KEY DEFAULT gen_random_uuid(),
    loan_id         UUID                        NOT NULL REFERENCES loans(id),
    installment_id  UUID                        REFERENCES installments(id),
    -- NULL = acción sobre el préstamo completo; con valor = cuota específica

    action_type     collection_action_type      NOT NULL,
    -- LLAMADA_TELEFONICA | WHATSAPP | EMAIL | SMS | NOTIFICACION_PUSH |
    -- PROMESA_DE_PAGO | ACUERDO_DE_PAGO_DIGITAL  (cobranza 100% remota)

    note            TEXT,                        -- Resumen del contacto

    status          collection_action_status    NOT NULL DEFAULT 'ACTIVA',
    -- ACTIVA → CUMPLIDA | PROMESA_INCUMPLIDA | CERRADA

    next_action_at  TIMESTAMPTZ,                 -- Próxima gestión o verificación de promesa

    -- ── Promesa de pago (RF-21.2) ────────────────────────────────────────────
    promise_amount  NUMERIC(18,4),
    promise_date    DATE,

    created_by      UUID                        NOT NULL REFERENCES users(id),
    created_at      TIMESTAMPTZ                 NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ                 NOT NULL DEFAULT NOW()
);

COMMENT ON TABLE collection_actions IS
    'Gestiones de cobranza digital: llamadas, mensajes y promesas de pago. El job diario marca PROMESA_INCUMPLIDA si vence sin pago (US-21, RF-21.3).';
COMMENT ON COLUMN collection_actions.promise_date IS
    'Si llega esta fecha y no se detecta pago, el job cambia el status a PROMESA_INCUMPLIDA (RF-21.3).';

CREATE TRIGGER tg_collection_actions_updated_at
    BEFORE UPDATE ON collection_actions
    FOR EACH ROW EXECUTE FUNCTION fn_set_updated_at();

-- ─────────────────────────────────────────────────────────────────────────────
-- Notificaciones omnicanal al cliente (US-19, US-21, RNF-15)
-- Encoladas de forma asíncrona (Redis / RabbitMQ)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE notifications (
    id            UUID                    PRIMARY KEY DEFAULT gen_random_uuid(),
    customer_id   UUID                    NOT NULL REFERENCES customers(id),
    channel       notification_channel    NOT NULL,       -- EMAIL | SMS | WHATSAPP | PUSH
    template_code VARCHAR(80)             NOT NULL,       -- Código de la plantilla de mensaje
    payload       JSONB,                                  -- Variables dinámicas de la plantilla
    status        notification_status     NOT NULL DEFAULT 'PENDING',
    sent_at       TIMESTAMPTZ,
    error_detail  TEXT,
    created_at    TIMESTAMPTZ             NOT NULL DEFAULT NOW()
);

COMMENT ON TABLE notifications IS
    'Cola de notificaciones omnicanal al cliente. Procesadas de forma asíncrona (US-19, US-21, RNF-15).';
