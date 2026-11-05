-- =============================================================================
-- MIGRACIÓN 08 — TESORERÍA DIGITAL Y CONCILIACIÓN BANCARIA
-- Modelo: Fintech 100% Digital
-- Dominio: US-37 (Conciliación Bancaria), US-38 (Reportes de Tesorería)
-- ELIMINADO COMPLETAMENTE: cash_boxes, cash_sessions, cash_movements, cash_transfers
--   (No hay cajas físicas, arqueo ciego, ni transferencias de efectivo entre sucursales)
-- MANTENIDO: bank_accounts → renombrado y extendido como company_bank_accounts (ya creado en 01)
-- NUEVO:     bank_statements (extractos bancarios para conciliación automática)
-- Dependencias: 01_admin_config.sql, 07_payments_ledger.sql
-- =============================================================================

-- ─────────────────────────────────────────────────────────────────────────────
-- Extractos bancarios importados — base de la conciliación automática (US-37)
-- Los extractos se importan desde el banco (archivo Excel/CSV/API) o por webhook
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE bank_statements (
    id                      UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    company_bank_account_id UUID        NOT NULL REFERENCES company_bank_accounts(id),
    -- Período del extracto
    statement_date          DATE        NOT NULL,
    -- Movimiento individual del extracto
    operation_number        VARCHAR(100) NOT NULL,         -- Número de operación del banco
    value_date              DATE        NOT NULL,          -- Fecha valor del movimiento
    amount                  NUMERIC(18,4) NOT NULL,        -- Positivo=ingreso, Negativo=egreso
    currency_id             UUID        NOT NULL REFERENCES currencies(id),
    description             TEXT,                          -- Concepto/descripción del banco
    external_reference      VARCHAR(150),                  -- Referencia externa del pagador
    -- Estado de conciliación
    reconciliation_status   reconciliation_status NOT NULL DEFAULT 'PENDIENTE_CONCILIACION',
    reconciled_payment_id   UUID        REFERENCES payments(id),
    reconciled_disbursement_id UUID     REFERENCES disbursements(id),
    reconciled_at           TIMESTAMPTZ,
    reconciled_by           UUID        REFERENCES users(id),
    -- Metadatos de importación
    imported_at             TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    imported_by             UUID        REFERENCES users(id),
    import_source           VARCHAR(50),  -- 'ARCHIVO_CSV', 'API_BANCO', 'WEBHOOK', 'MANUAL'
    UNIQUE (company_bank_account_id, operation_number)
);

COMMENT ON TABLE bank_statements IS
    'Movimientos importados del extracto bancario. Base de la conciliación automática de pagos y desembolsos (US-37, RF-37.2).';
COMMENT ON COLUMN bank_statements.amount IS
    'Positivo = ingreso a la cuenta de la Fintech (cobros de clientes). Negativo = egreso (desembolsos, comisiones bancarias).';
COMMENT ON COLUMN bank_statements.reconciliation_status IS
    'CONCILIADO_AUTOMATICO: coincidió con un pago/desembolso por referencia. PENDIENTE: no encontró coincidencia. DESCUADRADO: montos no cuadran.';
