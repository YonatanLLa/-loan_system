-- =============================================================================
-- MIGRACIÓN 10 — INTEGRACIONES BANCARIAS (WEBHOOKS, REINTENTOS Y DLQ)
-- Modelo: Fintech 100% Digital
-- Dominio: US-36 (Webhooks), US-38 (DLQ y Reintentos)
-- ELIMINADO: reconciliations — duplicaba a bank_statements (migración 08), que
--            es la única base de la conciliación bancaria (US-37).
-- Dependencias: 07_payments_ledger.sql, 08_treasury_reconciliation.sql
-- =============================================================================

-- ─────────────────────────────────────────────────────────────────────────────
-- Eventos de integración externa — webhooks bancarios (US-36, RF-36.3)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE integration_events (
    id            UUID                        PRIMARY KEY DEFAULT gen_random_uuid(),
    provider      VARCHAR(50)                 NOT NULL,
    -- 'bcp' | 'bbva' | 'yape' | 'plin' | 'niubiz' | 'scotiabank'

    event_type    VARCHAR(80)                 NOT NULL,
    -- Ej: 'payment.confirmed', 'transfer.received'

    external_id   VARCHAR(150)                NOT NULL,
    -- Código de operación del banco — único por proveedor

    payload       JSONB                       NOT NULL,  -- Cuerpo raw del webhook

    status        integration_event_status    NOT NULL DEFAULT 'RECEIVED',
    -- RECEIVED → PROCESSED | FAILED → DEAD_LETTER

    error_detail  TEXT,                       -- Stack trace del último error
    retry_count   SMALLINT                    NOT NULL DEFAULT 0,
    next_retry_at TIMESTAMPTZ,                -- Próximo intento con backoff exponencial

    processed_at  TIMESTAMPTZ,
    created_at    TIMESTAMPTZ                 NOT NULL DEFAULT NOW(),

    -- RF-36.4: Idempotencia — mismo external_id del mismo proveedor = único
    UNIQUE (provider, external_id)
);

COMMENT ON TABLE integration_events IS
    'Eventos de webhooks bancarios con reintentos exponenciales y DLQ. Idempotente por (provider, external_id) (US-36, US-38, RF-36.4).';
COMMENT ON COLUMN integration_events.retry_count IS
    'Contador de reintentos: 1°→30s, 2°→2min, 3°→10min, 4°→30min. Tras 4 fallos → DEAD_LETTER (US-38, RF-38.1).';
