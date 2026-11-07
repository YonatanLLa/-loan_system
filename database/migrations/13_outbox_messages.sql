-- =============================================================================
-- MIGRACIÓN 13 — OUTBOX TRANSACCIONAL
-- Modelo: Fintech 100% Digital
-- Dominio: ADR 04/05 (Outbox para desembolsos e integraciones asíncronas)
-- Los módulos escriben aquí sus eventos de integración en la MISMA transacción
-- que sus datos de negocio. El worker (OutboxDispatcherJob) los publica.
-- Dependencias: 00_extensions_and_enums.sql (pgcrypto)
-- Se aplica al final de la secuencia de DbMigrator (después de los seeds)
-- para no renumerar scripts ya aplicados.
-- =============================================================================

-- ─────────────────────────────────────────────────────────────────────────────
-- Mensajes pendientes de publicación
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE outbox_messages (
    id            UUID          PRIMARY KEY DEFAULT gen_random_uuid(),
    -- = EventId del evento de integración (deduplicación aguas abajo)

    event_type    VARCHAR(500)  NOT NULL,
    -- Nombre CLR completo del evento, ej. 'LoanSystem.Modules.Loans.Core.Contracts.LoanDisbursed'

    payload       JSONB         NOT NULL,  -- Evento serializado (System.Text.Json, camelCase)

    occurred_on   TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    processed_on  TIMESTAMPTZ,             -- NULL = pendiente
    error         TEXT,                    -- Último error de publicación
    retry_count   INTEGER       NOT NULL DEFAULT 0
);

COMMENT ON TABLE outbox_messages IS
    'Outbox transaccional: eventos de integración escritos junto con los datos de negocio y publicados por el worker (at-least-once).';
COMMENT ON COLUMN outbox_messages.retry_count IS
    'Intentos fallidos. Al alcanzar el máximo del dispatcher (5) el mensaje deja de reintentarse y queda para revisión manual.';

-- Lote de pendientes en orden de llegada (consulta del dispatcher con FOR UPDATE SKIP LOCKED)
CREATE INDEX ix_outbox_messages_pending
    ON outbox_messages (occurred_on)
    WHERE processed_on IS NULL;
