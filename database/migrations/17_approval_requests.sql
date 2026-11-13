-- =============================================================================
-- MIGRACIÓN 17 — CONTROL DE CUATRO OJOS / APROBACIONES (BE-SEC-09, DB-66)
-- Modelo: Fintech 100% Digital
-- Dependencias: 02_security_users.sql, 07_payments_ledger.sql, 15_audit_logs_append_only.sql
-- =============================================================================

-- 1) Solicitudes de aprobación de cuatro ojos (DB-66)
CREATE TABLE approval_requests (
    id               UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    operation_type   VARCHAR(40)  NOT NULL CHECK (operation_type IN (
                         'PAYMENT_REVERSAL',
                         'LOAN_WRITE_OFF',
                         'MANUAL_ADJUSTMENT',
                         'PRODUCT_RATE_CHANGE',
                         'LOAN_RESTRUCTURE',
                         'USER_ROLE_CHANGE')),
    module           VARCHAR(30)  NOT NULL,
    target_entity    VARCHAR(50)  NOT NULL,
    target_id        UUID         NOT NULL,
    payload          JSONB        NOT NULL,
    target_snapshot  JSONB        NOT NULL,
    reason           TEXT         NOT NULL CHECK (char_length(btrim(reason)) >= 10),
    evidence_ref     VARCHAR(100),
    status           VARCHAR(20)  NOT NULL DEFAULT 'PENDING'
                     CHECK (status IN ('PENDING', 'APPROVED', 'REJECTED', 'CANCELLED', 'EXPIRED')),
    requested_by     UUID         NOT NULL REFERENCES users(id),
    requested_at     TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    expires_at       TIMESTAMPTZ  NOT NULL,
    decided_by       UUID         REFERENCES users(id),
    decided_at       TIMESTAMPTZ,
    decision_comment TEXT,

    -- R1: quien aprueba o rechaza es un usuario autenticado distinto del solicitante
    CONSTRAINT ck_approval_requests_four_eyes CHECK (
        status NOT IN ('APPROVED', 'REJECTED')
        OR (decided_by IS NOT NULL AND decided_by <> requested_by)),
    -- R2: el usuario afectado no aprueba ni rechaza su propio cambio de rol
    CONSTRAINT ck_approval_requests_beneficiary CHECK (
        operation_type <> 'USER_ROLE_CHANGE'
        OR status NOT IN ('APPROVED', 'REJECTED')
        OR decided_by IS DISTINCT FROM target_id),
    CONSTRAINT ck_approval_requests_decided CHECK ((status = 'PENDING') = (decided_at IS NULL)),
    CONSTRAINT ck_approval_requests_reject_comment CHECK (
        status <> 'REJECTED' OR char_length(btrim(coalesce(decision_comment, ''))) > 0),
    CONSTRAINT ck_approval_requests_expiry CHECK (expires_at > requested_at)
);

-- R6: una sola solicitud pendiente por operación y objetivo
CREATE UNIQUE INDEX ux_approval_requests_pending_target
    ON approval_requests (operation_type, target_id) WHERE status = 'PENDING';

CREATE INDEX ix_approval_requests_inbox
    ON approval_requests (requested_at) WHERE status = 'PENDING';

CREATE INDEX ix_approval_requests_target
    ON approval_requests (target_entity, target_id);

CREATE INDEX ix_approval_requests_mine
    ON approval_requests (requested_by, requested_at DESC);

COMMENT ON TABLE approval_requests IS
    'Solicitudes de aprobación de cuatro ojos para operaciones sensibles (BE-SEC-09, DB-66).';

-- 2) R4 + inmutabilidad: lo solicitado no cambia y una decisión no se reescribe ni se borra
CREATE OR REPLACE FUNCTION fn_approval_requests_guard()
RETURNS TRIGGER LANGUAGE plpgsql AS $$
BEGIN
    IF TG_OP = 'DELETE' THEN
        RAISE EXCEPTION 'approval_requests no admite DELETE.';
    END IF;

    IF OLD.status <> 'PENDING' THEN
        RAISE EXCEPTION 'La solicitud % ya fue decidida (%) y es inmutable.', OLD.id, OLD.status;
    END IF;

    IF NEW.operation_type <> OLD.operation_type
       OR NEW.module <> OLD.module
       OR NEW.target_entity <> OLD.target_entity
       OR NEW.target_id <> OLD.target_id
       OR NEW.payload IS DISTINCT FROM OLD.payload
       OR NEW.target_snapshot IS DISTINCT FROM OLD.target_snapshot
       OR NEW.requested_by <> OLD.requested_by
       OR NEW.requested_at <> OLD.requested_at
       OR NEW.expires_at <> OLD.expires_at
       OR NEW.reason <> OLD.reason
       OR NEW.evidence_ref IS DISTINCT FROM OLD.evidence_ref THEN
        RAISE EXCEPTION 'Lo solicitado no puede modificarse: cancele y cree otra solicitud.';
    END IF;

    RETURN NEW;
END;
$$;

CREATE OR REPLACE FUNCTION fn_approval_requests_no_truncate()
RETURNS TRIGGER LANGUAGE plpgsql AS $$
BEGIN
    RAISE EXCEPTION 'approval_requests no admite TRUNCATE.';
END;
$$;

DROP TRIGGER IF EXISTS tg_approval_requests_guard ON approval_requests;
CREATE TRIGGER tg_approval_requests_guard
    BEFORE UPDATE OR DELETE ON approval_requests
    FOR EACH ROW EXECUTE FUNCTION fn_approval_requests_guard();

DROP TRIGGER IF EXISTS tg_approval_requests_no_truncate ON approval_requests;
CREATE TRIGGER tg_approval_requests_no_truncate
    BEFORE TRUNCATE ON approval_requests
    FOR EACH STATEMENT EXECUTE FUNCTION fn_approval_requests_no_truncate();

-- 3) Trazabilidad: enlazar auditoría, transacciones y pagos con su solicitud aprobada (R9)
ALTER TABLE audit_logs   ADD COLUMN approval_request_id UUID;
ALTER TABLE transactions ADD COLUMN approval_request_id UUID REFERENCES approval_requests(id);
ALTER TABLE payments     ADD COLUMN approval_request_id UUID REFERENCES approval_requests(id);

CREATE INDEX ix_audit_logs_approval_request
    ON audit_logs (approval_request_id) WHERE approval_request_id IS NOT NULL;
