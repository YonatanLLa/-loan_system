-- =============================================================================
-- MIGRACIÓN 04 — PRODUCTOS DE CRÉDITO
-- Dominio: US-07 (Catálogo), US-08 (Métodos amortización), US-09 (Mora/Comisiones)
-- Dependencias: 01_admin_config.sql, 02_security_users.sql
-- =============================================================================

-- ─────────────────────────────────────────────────────────────────────────────
-- Productos de crédito (plantillas configurables)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE loan_products (
    id                      UUID                PRIMARY KEY DEFAULT gen_random_uuid(),
    name                    VARCHAR(150)        NOT NULL,
    description             TEXT,
    currency_id             UUID                NOT NULL REFERENCES currencies(id),

    -- ── Límites de monto y plazo (US-07, RF-07.1) ───────────────────────────
    min_amount              NUMERIC(18,4)       NOT NULL CHECK (min_amount > 0),
    max_amount              NUMERIC(18,4)       NOT NULL,
    min_term                SMALLINT            NOT NULL CHECK (min_term >= 1),   -- cuotas
    max_term                SMALLINT            NOT NULL,

    -- ── Frecuencias de pago admitidas (US-07, RF-07.1) ──────────────────────
    -- Array de ENUMs: ['MENSUAL','QUINCENAL']
    allowed_frequencies     payment_frequency[] NOT NULL DEFAULT '{MENSUAL}',

    -- ── Método de amortización (US-08, RF-08.1) ─────────────────────────────
    amortization_method     amortization_method NOT NULL DEFAULT 'FRANCES',

    -- ── Tasa de interés (US-08, RF-08.2 / RF-08.3) ──────────────────────────
    -- Siempre se almacena la TEA (Tasa Efectiva Anual) como decimal.
    -- Ej: 30% anual → 0.300000. La app convierte a tasa del período.
    rate_type               rate_type           NOT NULL DEFAULT 'EFECTIVA',
    annual_rate             NUMERIC(12,6)       NOT NULL CHECK (annual_rate > 0),

    -- ── Comisión de apertura (US-09, RF-09.1) ───────────────────────────────
    opening_fee_rate        NUMERIC(12,6)       NOT NULL DEFAULT 0,  -- % del capital
    opening_fee_fixed       NUMERIC(18,4)       NOT NULL DEFAULT 0,  -- monto fijo
    opening_fee_mode        commission_mode     NOT NULL DEFAULT 'DEDUCTIVA',

    -- ── Seguros / gastos periódicos fijos por cuota ──────────────────────────
    periodic_fee            NUMERIC(18,4)       NOT NULL DEFAULT 0,

    -- ── Mora y días de gracia (US-09, RF-09.2 / RF-09.3) ───────────────────
    grace_days              SMALLINT            NOT NULL DEFAULT 0,
    mora_rate_per_day       NUMERIC(12,6)       NOT NULL DEFAULT 0,
    mora_base               mora_base_type      NOT NULL DEFAULT 'SALDO_CAPITAL_VENCIDO',
    allows_anatocismo       BOOLEAN             NOT NULL DEFAULT FALSE,  -- RF-09.4

    -- ── Penalidad por cancelación anticipada (US-30) ─────────────────────────
    prepayment_penalty_rate NUMERIC(12,6)       NOT NULL DEFAULT 0,

    -- ── Reglas de elegibilidad (US-07, RF-07.1) ─────────────────────────────
    min_age                 SMALLINT            NOT NULL DEFAULT 18,
    max_age                 SMALLINT            NOT NULL DEFAULT 70,
    min_monthly_income      NUMERIC(18,4)       NOT NULL DEFAULT 0,

    -- ── Ajuste de feriados: NEXT (siguiente hábil) | PREV (anterior) ─────────
    holiday_shift_direction VARCHAR(4)          NOT NULL DEFAULT 'NEXT'
                                                CHECK (holiday_shift_direction IN ('NEXT','PREV')),

    -- ── Estado del producto (US-07) ──────────────────────────────────────────
    status                  product_status      NOT NULL DEFAULT 'ACTIVO',

    created_by              UUID                REFERENCES users(id),
    created_at              TIMESTAMPTZ         NOT NULL DEFAULT NOW(),
    updated_at              TIMESTAMPTZ         NOT NULL DEFAULT NOW(),

    CONSTRAINT chk_product_amounts CHECK (max_amount >= min_amount),
    CONSTRAINT chk_product_terms   CHECK (max_term   >= min_term)
);

COMMENT ON TABLE loan_products IS
    'Plantillas configurables de productos de crédito (US-07, US-08, US-09).';
COMMENT ON COLUMN loan_products.annual_rate IS
    'TEA almacenada como decimal de alta precisión (ej. 0.300000 = 30%). Mínimo 6 decimales (RF-08.3).';
COMMENT ON COLUMN loan_products.allows_anatocismo IS
    'Por defecto FALSE: la mora NO capitaliza. Habilitarlo requiere autorización explícita (RF-09.4).';
COMMENT ON COLUMN loan_products.annual_rate IS
    'RNF-06: Si se modifica, los préstamos ya desembolsados conservan su tasa original (snapshot en loans).';

CREATE TRIGGER tg_loan_products_updated_at
    BEFORE UPDATE ON loan_products
    FOR EACH ROW EXECUTE FUNCTION fn_set_updated_at();
