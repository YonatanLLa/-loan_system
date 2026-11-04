-- =============================================================================
-- MIGRACIÓN 08 — CAJA Y TESORERÍA
-- Dominio: US-23 (Apertura/Cierre), US-24 (Movimientos), US-25 (Transferencias)
-- Dependencias: 02_security_users.sql, 01_admin_config.sql
-- NOTA: Esta migración agrega las FK diferidas que loans, payments y
--       transactions tienen hacia cash_boxes.
-- =============================================================================

-- ─────────────────────────────────────────────────────────────────────────────
-- Cajas físicas (ventanillas) — (US-23, RF-23.1)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE cash_boxes (
    id                  UUID            PRIMARY KEY DEFAULT gen_random_uuid(),
    name                VARCHAR(100)    NOT NULL,
    branch_id           UUID            NOT NULL REFERENCES branches(id),
    currency_id         UUID            NOT NULL REFERENCES currencies(id),
    responsible_user_id UUID            REFERENCES users(id),
    current_balance     NUMERIC(18,4)   NOT NULL DEFAULT 0,
    status              cashbox_status  NOT NULL DEFAULT 'CERRADA',
    created_at          TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ     NOT NULL DEFAULT NOW()
);

COMMENT ON TABLE cash_boxes IS
    'Cajas físicas de ventanilla asignadas a sucursales. Solo se opera si status=ABIERTA (US-23, RNF-16).';
COMMENT ON COLUMN cash_boxes.current_balance IS
    'Saldo actualizado en tiempo real. Se modifica por trigger al registrar movimientos (US-24, RF-24.3).';

CREATE TRIGGER tg_cash_boxes_updated_at
    BEFORE UPDATE ON cash_boxes
    FOR EACH ROW EXECUTE FUNCTION fn_set_updated_at();

-- ─────────────────────────────────────────────────────────────────────────────
-- Conexión de las FK diferidas hacia cash_boxes
-- (loans, payments y transactions fueron creadas antes en migraciones 06 y 07)
-- ─────────────────────────────────────────────────────────────────────────────
ALTER TABLE loans
    ADD CONSTRAINT fk_loan_cashbox
    FOREIGN KEY (cashbox_id) REFERENCES cash_boxes(id);

ALTER TABLE payments
    ADD CONSTRAINT fk_payment_cashbox
    FOREIGN KEY (cashbox_id) REFERENCES cash_boxes(id);

ALTER TABLE transactions
    ADD CONSTRAINT fk_transaction_cashbox
    FOREIGN KEY (cashbox_id) REFERENCES cash_boxes(id);

-- ─────────────────────────────────────────────────────────────────────────────
-- Sesiones de caja — apertura y cierre con arqueo ciego (US-23, RF-23.2 / RF-23.3)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE cash_sessions (
    id              UUID            PRIMARY KEY DEFAULT gen_random_uuid(),
    cashbox_id      UUID            NOT NULL REFERENCES cash_boxes(id),
    opened_by       UUID            NOT NULL REFERENCES users(id),
    opening_balance NUMERIC(18,4)   NOT NULL DEFAULT 0,
    opened_at       TIMESTAMPTZ     NOT NULL DEFAULT NOW(),

    -- ── Cierre con arqueo ciego ───────────────────────────────────────────────
    closed_by       UUID            REFERENCES users(id),
    closed_at       TIMESTAMPTZ,
    counted_amount  NUMERIC(18,4),  -- Monto físico contado por el cajero
    expected_amount NUMERIC(18,4),  -- Monto teórico calculado por el sistema
    -- expected = saldo_inicial + cobros - desembolsos + ingresos - gastos
    difference      NUMERIC(18,4),  -- counted_amount - expected_amount
    arqueo_result   arqueo_result,  -- CUADRADO | SOBRANTE | FALTANTE
    arqueo_detail   JSONB,
    -- Ej: {"billetes":{"100":5,"50":3},"monedas":{"1":20,"0.50":10}}
    report_url      TEXT            -- URL del PDF del acta de cierre firmada (RF-23.4)
);

COMMENT ON TABLE cash_sessions IS
    'Sesiones de apertura/cierre de caja con arqueo ciego (US-23, RF-23.2, RF-23.3). Inmutables tras cierre (RNF-17).';

-- ─────────────────────────────────────────────────────────────────────────────
-- Movimientos de caja — ingresos/egresos extraordinarios (US-24, RF-24.1)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE cash_movements (
    id            UUID                PRIMARY KEY DEFAULT gen_random_uuid(),
    cashbox_id    UUID                NOT NULL REFERENCES cash_boxes(id),
    session_id    UUID                REFERENCES cash_sessions(id),
    movement_type cash_movement_type  NOT NULL,
    concept       VARCHAR(200)        NOT NULL,
    amount        NUMERIC(18,4)       NOT NULL CHECK (amount > 0),
    voucher_number VARCHAR(60),                    -- Número de comprobante de sustento
    created_by    UUID                NOT NULL REFERENCES users(id),
    created_at    TIMESTAMPTZ         NOT NULL DEFAULT NOW()
);

COMMENT ON TABLE cash_movements IS
    'Movimientos de efectivo extraordinarios (gastos operativos, ingresos varios) no vinculados a préstamos (US-24).';

-- Trigger: actualiza cash_boxes.current_balance tras cada movimiento
CREATE TRIGGER tg_cash_movements_update_balance
    AFTER INSERT ON cash_movements
    FOR EACH ROW EXECUTE FUNCTION fn_update_cashbox_balance_on_movement();

-- ─────────────────────────────────────────────────────────────────────────────
-- Transferencias entre cajas — two-phase handshake (US-25, RF-25.3)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE cash_transfers (
    id                      UUID            PRIMARY KEY DEFAULT gen_random_uuid(),
    source_cashbox_id       UUID            NOT NULL REFERENCES cash_boxes(id),
    destination_cashbox_id  UUID            NOT NULL REFERENCES cash_boxes(id),
    amount                  NUMERIC(18,4)   NOT NULL CHECK (amount > 0),
    notes                   TEXT,
    status                  transfer_status NOT NULL DEFAULT 'EN_TRANSITO',
    -- EN_TRANSITO → COMPLETED (tras aceptación) | CANCELLED
    initiated_by            UUID            NOT NULL REFERENCES users(id),
    initiated_at            TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    accepted_by             UUID            REFERENCES users(id),
    accepted_at             TIMESTAMPTZ,
    CONSTRAINT chk_transfer_different_boxes
        CHECK (source_cashbox_id <> destination_cashbox_id)
);

COMMENT ON TABLE cash_transfers IS
    'Transferencias two-phase entre cajas: Envío → EN_TRANSITO → Recepción → COMPLETED (US-25, RF-25.3).';

-- ─────────────────────────────────────────────────────────────────────────────
-- Cuentas bancarias de la empresa (para conciliación — US-37)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE bank_accounts (
    id                      UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    bank_name               VARCHAR(100) NOT NULL,
    account_number_masked   VARCHAR(30)  NOT NULL,  -- Solo últimos 4 dígitos visibles (RNF-05)
    currency_id             UUID         NOT NULL REFERENCES currencies(id),
    is_active               BOOLEAN      NOT NULL DEFAULT TRUE,
    created_at              TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

COMMENT ON TABLE bank_accounts IS
    'Cuentas bancarias de la entidad para conciliación de extractos (US-37, RF-37.2).';
