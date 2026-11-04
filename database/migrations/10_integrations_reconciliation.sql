-- =============================================================================
-- MIGRACIÓN 10 — INTEGRACIONES BANCARIAS Y CONCILIACIÓN
-- Dominio: US-36 (Webhooks), US-37 (Conciliación), US-38 (DLQ y Reintentos)
-- Dependencias: 07_payments_ledger.sql, 08_treasury_cashbox.sql
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

-- ─────────────────────────────────────────────────────────────────────────────
-- Conciliación de pagos — extractos bancarios vs sistema (US-37, RF-37.3 / RF-37.4)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE reconciliations (
    id                  UUID                    PRIMARY KEY DEFAULT gen_random_uuid(),
    bank_account_id     UUID                    REFERENCES bank_accounts(id),

    -- ── Movimiento bancario importado (extracto) ─────────────────────────────
    source              VARCHAR(50)             NOT NULL,
    -- 'BCP_EXTRACTO' | 'YAPE_ARCHIVO' | 'BBVA_API'
    external_reference  VARCHAR(150),
    bank_date           DATE,
    bank_description    TEXT,
    bank_amount         NUMERIC(18,4),

    -- ── Match contra sistema interno ─────────────────────────────────────────
    loan_id             UUID                    REFERENCES loans(id),
    payment_id          UUID                    REFERENCES payments(id),

    -- ── Estado de conciliación (RF-37.4) ────────────────────────────────────
    status              reconciliation_status   NOT NULL DEFAULT 'PENDIENTE_CONCILIACION',
    -- CONCILIADO_AUTOMATICO | PENDIENTE_CONCILIACION | DESCUADRADO | RECONCILED

    reconciled_by       UUID                    REFERENCES users(id),
    reconciled_at       TIMESTAMPTZ,
    created_at          TIMESTAMPTZ             NOT NULL DEFAULT NOW()
);

COMMENT ON TABLE reconciliations IS
    'Conciliación de extractos bancarios vs pagos internos. Estados: automático, pendiente, descuadrado (US-37, RF-37.3, RF-37.4).';
