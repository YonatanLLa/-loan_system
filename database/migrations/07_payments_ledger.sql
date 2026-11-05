-- =============================================================================
-- MIGRACIÓN 07 — PAGOS Y LIBRO MAYOR (LEDGER)
-- Modelo: Fintech 100% Digital
-- Dominio: US-16 (Prelación), US-17 (Sobrepagos), US-18 (Reversos)
-- ELIMINADO: cashbox_id (sin cajas físicas)
-- AGREGADO:  customer_bank_account_id (desde qué cuenta pagó el cliente)
--            company_bank_account_id  (a qué cuenta de la fintech llegó el pago)
--            gateway_reference        (referencia de la pasarela de pago / webhook)
-- Dependencias: 06_loans_schedules.sql, 01_admin_config.sql, 03_customers_kyc.sql
-- =============================================================================

-- ─────────────────────────────────────────────────────────────────────────────
-- Pagos registrados — INMUTABLES (US-16, US-18)
--
-- REGLA CLAVE (RF-18.1):
--   Está terminantemente prohibido UPDATE o DELETE sobre esta tabla.
--   Las correcciones se realizan con un nuevo registro de payment_kind='REVERSAL'.
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE payments (
    id                        UUID            PRIMARY KEY DEFAULT gen_random_uuid(),
    loan_id                   UUID            NOT NULL REFERENCES loans(id),

    -- ── Tipo de pago ─────────────────────────────────────────────────────────
    payment_kind              payment_kind    NOT NULL DEFAULT 'NORMAL',
    -- REVERSAL apunta a reversal_of_id y tiene monto "negativo" en la lógica de app

    -- ── Monto ────────────────────────────────────────────────────────────────
    amount                    NUMERIC(18,4)   NOT NULL,

    -- ── Método de pago digital (US-16, RF-16.2) ──────────────────────────────
    payment_method_id         UUID            REFERENCES payment_methods(id),

    -- ── Trazabilidad bancaria digital ────────────────────────────────────────
    -- Cuenta del cliente desde donde se originó el pago
    customer_bank_account_id  UUID            REFERENCES customer_bank_accounts(id),
    -- Cuenta de la Fintech donde se recibió el pago
    company_bank_account_id   UUID            REFERENCES company_bank_accounts(id),
    -- Referencia de la pasarela de pago / banco / billetera digital
    gateway_reference         VARCHAR(150),   -- Código de operación Yape/Plin/Niubiz/Webhook
    external_reference        VARCHAR(150),   -- Número de operación del banco externo

    -- ── Fecha efectiva del pago ──────────────────────────────────────────────
    paid_at                   TIMESTAMPTZ     NOT NULL,

    -- ── Estado del pago digital ──────────────────────────────────────────────
    status                    payment_status  NOT NULL DEFAULT 'APPLIED',

    -- ── Reverso (US-18, RF-18.2 → RF-18.4) ─────────────────────────────────
    reversal_of_id            UUID            REFERENCES payments(id),
    reversal_reason           TEXT,           -- Obligatorio en REVERSAL (RF-18.3)

    -- ── Recibo digital (RNF-13) ───────────────────────────────────────────────
    receipt_number            VARCHAR(30)     UNIQUE,    -- Nro. correlativo auto-generado
    receipt_url               TEXT,                      -- URL del PDF en Object Storage

    -- ── Metadatos ────────────────────────────────────────────────────────────
    registered_by             UUID            REFERENCES users(id),
    idempotency_key           VARCHAR(100)    UNIQUE,    -- RF-16.2: evita doble registro

    created_at                TIMESTAMPTZ     NOT NULL DEFAULT NOW()
);

COMMENT ON TABLE payments IS
    'Pagos registrados. INMUTABLES: las correcciones se realizan con contra-asiento (payment_kind=REVERSAL). Prohibido UPDATE/DELETE (US-16, US-18, RF-18.1).';
COMMENT ON COLUMN payments.gateway_reference IS
    'Referencia de la pasarela de pago (Yape, Plin, Niubiz, etc.). Usar para conciliación automática (US-37).';
COMMENT ON COLUMN payments.receipt_number IS
    'Número correlativo único de recibo digital generado automáticamente (RNF-13).';
COMMENT ON COLUMN payments.idempotency_key IS
    'Garantiza que reintentos de red o doble clic no dupliquen registros de pago (RF-16.2).';

-- Trigger: genera receipt_number automáticamente al insertar un pago NORMAL
CREATE TRIGGER tg_payments_receipt_number
    BEFORE INSERT ON payments
    FOR EACH ROW EXECUTE FUNCTION fn_generate_receipt_number();

-- ─────────────────────────────────────────────────────────────────────────────
-- Detalle de cascada de prelación mora→interés→comisión→capital (US-16, RF-16.4 / RF-16.5)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE payment_allocations (
    id              UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    payment_id      UUID        NOT NULL REFERENCES payments(id),
    installment_id  UUID        NOT NULL REFERENCES installments(id),

    -- Orden de prelación (RF-16.4):
    -- 1° mora/penalidades → 2° interés → 3° comisiones → 4° capital
    to_mora         NUMERIC(18,4) NOT NULL DEFAULT 0 CHECK (to_mora     >= 0),
    to_interest     NUMERIC(18,4) NOT NULL DEFAULT 0 CHECK (to_interest >= 0),
    to_fee          NUMERIC(18,4) NOT NULL DEFAULT 0 CHECK (to_fee      >= 0),
    to_principal    NUMERIC(18,4) NOT NULL DEFAULT 0 CHECK (to_principal>= 0),

    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

COMMENT ON TABLE payment_allocations IS
    'Desglosa cómo se distribuyó cada pago en mora→interés→comisión→capital por cuota (US-16, RF-16.4 / RF-16.5).';

-- ─────────────────────────────────────────────────────────────────────────────
-- Libro Mayor / Ledger — transacciones financieras inmutables
-- Registra TODO movimiento de dinero para control contable (US-13, US-16)
-- Sin cashbox_id: el flujo de fondos se rastrea por company_bank_account_id
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE transactions (
    id                      BIGSERIAL           PRIMARY KEY,
    -- Usando BIGSERIAL para garantizar orden cronológico confiable

    -- ── Referencias de negocio ───────────────────────────────────────────────
    loan_id                 UUID                REFERENCES loans(id),
    payment_id              UUID                REFERENCES payments(id),
    disbursement_id         UUID                REFERENCES disbursements(id),
    -- Cuenta de la Fintech involucrada en la transacción (reemplaza cashbox_id)
    company_bank_account_id UUID                REFERENCES company_bank_accounts(id),

    -- ── Clasificación ────────────────────────────────────────────────────────
    transaction_type        transaction_type    NOT NULL,
    direction               transaction_dir     NOT NULL,
    -- INFLOW = ingresa dinero al sistema (cobros); OUTFLOW = sale dinero (desembolsos)

    -- ── Monto y moneda ───────────────────────────────────────────────────────
    amount                  NUMERIC(18,4)       NOT NULL CHECK (amount > 0),
    currency_id             UUID                NOT NULL REFERENCES currencies(id),

    -- ── Trazabilidad ─────────────────────────────────────────────────────────
    reference               VARCHAR(150),       -- Referencia bancaria / número de operación
    notes                   TEXT,
    created_by              UUID                REFERENCES users(id),
    created_at              TIMESTAMPTZ         NOT NULL DEFAULT NOW()
);

COMMENT ON TABLE transactions IS
    'Libro mayor / ledger financiero. Inmutable: registra TODO flujo de dinero digital para control contable (US-13, US-16, US-18).';
COMMENT ON COLUMN transactions.direction IS
    'INFLOW = cobro/ingreso al sistema (cuotas pagadas); OUTFLOW = desembolso/egreso del sistema (transferencia al cliente).';
COMMENT ON COLUMN transactions.company_bank_account_id IS
    'Cuenta bancaria de la Fintech involucrada. Reemplaza cashbox_id del modelo físico.';
