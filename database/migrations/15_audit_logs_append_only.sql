-- =============================================================================
-- MIGRACIÓN 15 — AUDIT LOGS: INMUTABILIDAD APPEND-ONLY
-- Modelo: Fintech 100% Digital
-- Dominio: US-03, RF-03.3, CA-03.2 (Inmutabilidad del Log)
-- Prohíbe cualquier UPDATE, DELETE o TRUNCATE a nivel de base de datos.
-- Dependencias: 02_security_users.sql, functions/fn_updated_at.sql
-- =============================================================================

CREATE OR REPLACE FUNCTION fn_audit_log_immutable()
RETURNS TRIGGER LANGUAGE plpgsql AS $$
BEGIN
    RAISE EXCEPTION
        'audit_logs es append-only: las operaciones UPDATE, DELETE y TRUNCATE están prohibidas.';
END;
$$;

DROP TRIGGER IF EXISTS tg_audit_logs_immutable ON audit_logs;
CREATE TRIGGER tg_audit_logs_immutable
    BEFORE UPDATE OR DELETE ON audit_logs
    FOR EACH ROW EXECUTE FUNCTION fn_audit_log_immutable();

DROP TRIGGER IF EXISTS tg_audit_logs_immutable_truncate ON audit_logs;
CREATE TRIGGER tg_audit_logs_immutable_truncate
    BEFORE TRUNCATE ON audit_logs
    FOR EACH STATEMENT EXECUTE FUNCTION fn_audit_log_immutable();
