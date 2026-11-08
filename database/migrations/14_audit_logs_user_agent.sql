-- =============================================================================
-- MIGRACIÓN 14 — AUDIT LOGS: COLUMNA USER_AGENT
-- Modelo: Fintech 100% Digital
-- Dominio: US-03, BE-SEC-07 (Trazabilidad completa de acciones)
-- Dependencias: 02_security_users.sql
-- =============================================================================

ALTER TABLE audit_logs ADD COLUMN IF NOT EXISTS user_agent TEXT;

COMMENT ON COLUMN audit_logs.user_agent IS
    'Cliente que originó la acción (navegador/app). Requerido por BE-SEC-07.';
