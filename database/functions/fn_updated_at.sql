-- =============================================================================
-- FUNCIONES Y TRIGGERS REUTILIZABLES
-- Ejecutar DESPUÉS de 00_extensions_and_enums.sql
-- y ANTES de crear las tablas (migrations 01+)
-- =============================================================================

-- ─────────────────────────────────────────────────────────────────────────────
-- Función: actualiza automáticamente updated_at en cada UPDATE
-- ─────────────────────────────────────────────────────────────────────────────
CREATE OR REPLACE FUNCTION fn_set_updated_at()
RETURNS TRIGGER LANGUAGE plpgsql AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$;

-- ─────────────────────────────────────────────────────────────────────────────
-- Función: rechaza cualquier UPDATE o DELETE en audit_logs
-- (append-only / bitácora inmutable — RF-03.3)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE OR REPLACE FUNCTION fn_audit_log_immutable()
RETURNS TRIGGER LANGUAGE plpgsql AS $$
BEGIN
    RAISE EXCEPTION
        'audit_logs es append-only: las operaciones UPDATE y DELETE están prohibidas.';
END;
$$;

-- ─────────────────────────────────────────────────────────────────────────────
-- Función: genera el número de recibo correlativo para pagos
-- Formato: REC-YYYYMMDD-NNNNNN  (ej. REC-20261104-000001)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE SEQUENCE IF NOT EXISTS seq_receipt_number START 1 INCREMENT 1;

CREATE OR REPLACE FUNCTION fn_generate_receipt_number()
RETURNS TRIGGER LANGUAGE plpgsql AS $$
BEGIN
    IF NEW.receipt_number IS NULL AND NEW.payment_kind = 'NORMAL' THEN
        NEW.receipt_number :=
            'REC-' ||
            TO_CHAR(NOW(), 'YYYYMMDD') || '-' ||
            LPAD(NEXTVAL('seq_receipt_number')::TEXT, 6, '0');
    END IF;
    RETURN NEW;
END;
$$;

-- ─────────────────────────────────────────────────────────────────────────────
-- Función: sincroniza el saldo actual de la caja tras cada movimiento
-- Se aplica como trigger AFTER INSERT en cash_movements
-- ─────────────────────────────────────────────────────────────────────────────
CREATE OR REPLACE FUNCTION fn_update_cashbox_balance_on_movement()
RETURNS TRIGGER LANGUAGE plpgsql AS $$
BEGIN
    IF NEW.movement_type = 'INGRESO_EXTRAORDINARIO' THEN
        UPDATE cash_boxes
           SET current_balance = current_balance + NEW.amount
         WHERE id = NEW.cashbox_id;
    ELSIF NEW.movement_type = 'EGRESO_GASTO' THEN
        UPDATE cash_boxes
           SET current_balance = current_balance - NEW.amount
         WHERE id = NEW.cashbox_id;
    END IF;
    RETURN NEW;
END;
$$;

-- ─────────────────────────────────────────────────────────────────────────────
-- Función: valida que solo haya 1 schedule activo por préstamo
-- Al activar un nuevo schedule, desactiva los anteriores
-- ─────────────────────────────────────────────────────────────────────────────
CREATE OR REPLACE FUNCTION fn_enforce_single_active_schedule()
RETURNS TRIGGER LANGUAGE plpgsql AS $$
BEGIN
    IF NEW.is_active = TRUE THEN
        UPDATE schedules
           SET is_active = FALSE
         WHERE loan_id = NEW.loan_id
           AND id      <> NEW.id;
    END IF;
    RETURN NEW;
END;
$$;
