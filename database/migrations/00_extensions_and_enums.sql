-- =============================================================================
-- MIGRACIÓN 00 — EXTENSIONES Y TIPOS ENUMERADOS
-- Ejecutar PRIMERO. No depende de ningún otro archivo.
-- =============================================================================

-- ─────────────────────────────────────────────────────────────────────────────
-- EXTENSIONES
-- ─────────────────────────────────────────────────────────────────────────────
CREATE EXTENSION IF NOT EXISTS "pgcrypto";   -- gen_random_uuid()
CREATE EXTENSION IF NOT EXISTS "unaccent";   -- búsquedas sin tildes

-- ─────────────────────────────────────────────────────────────────────────────
-- MÓDULO: CLIENTES
-- ─────────────────────────────────────────────────────────────────────────────

CREATE TYPE person_type AS ENUM (
    'NATURAL',
    'JURIDICA'
);

CREATE TYPE document_type AS ENUM (
    'DNI',
    'CE',
    'RUC',
    'PASAPORTE'
);

CREATE TYPE kyc_status_type AS ENUM (
    'PENDING',
    'VERIFIED',
    'REJECTED',
    'EXPIRED'
);

-- ─────────────────────────────────────────────────────────────────────────────
-- MÓDULO: SEGURIDAD / USUARIOS
-- ─────────────────────────────────────────────────────────────────────────────

CREATE TYPE user_role AS ENUM (
    'ROLE_ADMIN',
    'ROLE_ANALYST',
    'ROLE_CASHIER',
    'ROLE_COLLECTOR',
    'ROLE_AUDITOR',
    'ROLE_CLIENT'
);

CREATE TYPE user_status_type AS ENUM (
    'ACTIVE',
    'INACTIVE',
    'LOCKED'
);

-- ─────────────────────────────────────────────────────────────────────────────
-- MÓDULO: PRODUCTOS DE CRÉDITO
-- ─────────────────────────────────────────────────────────────────────────────

CREATE TYPE payment_frequency AS ENUM (
    'DIARIA',
    'SEMANAL',
    'QUINCENAL',
    'MENSUAL'
);

CREATE TYPE amortization_method AS ENUM (
    'FRANCES',      -- Cuota nivelada (la más usada)
    'FLAT',         -- Interés simple / microcréditos
    'ALEMAN',       -- Capital constante, cuota decreciente
    'AMERICANO'     -- Solo intereses periódicos + capital al final (bullet)
);

CREATE TYPE rate_type AS ENUM (
    'NOMINAL',
    'EFECTIVA'
);

CREATE TYPE mora_base_type AS ENUM (
    'SALDO_CAPITAL_VENCIDO',
    'CUOTA_TOTAL_VENCIDA'
);

CREATE TYPE commission_mode AS ENUM (
    'DEDUCTIVA',    -- Se descuenta del desembolso líquido
    'FINANCIADA'    -- Se agrega al capital del préstamo
);

CREATE TYPE product_status AS ENUM (
    'ACTIVO',
    'INACTIVO'
);

-- ─────────────────────────────────────────────────────────────────────────────
-- MÓDULO: ORIGINACIÓN Y SOLICITUDES
-- ─────────────────────────────────────────────────────────────────────────────

CREATE TYPE application_status AS ENUM (
    'SOLICITADO',
    'EN_EVALUACION',
    'APROBADO',
    'APROBADO_CON_CONDICIONES',
    'RECHAZADO',
    'ANULADO'
);

CREATE TYPE credit_decision AS ENUM (
    'APROBABLE',
    'REVISION_MANUAL',
    'RECHAZO_SUGERIDO'
);

CREATE TYPE loan_purpose_type AS ENUM (
    'CONSUMO',
    'CAPITAL_TRABAJO',
    'VEHICULO',
    'VIVIENDA',
    'REFINANCING',
    'CONSOLIDATION',
    'OTRO'
);

-- ─────────────────────────────────────────────────────────────────────────────
-- MÓDULO: PRÉSTAMOS Y CRONOGRAMAS
-- ─────────────────────────────────────────────────────────────────────────────

CREATE TYPE loan_status AS ENUM (
    'VIGENTE',
    'EN_MORA',
    'PAGADO',
    'CASTIGADO',
    'CANCELADO',
    'DESEMBOLSADO'
);

CREATE TYPE loan_close_reason AS ENUM (
    'PAGO_NORMAL',
    'LIQUIDACION_ANTICIPADA',
    'REFINANCIAMIENTO',
    'CASTIGO'
);

CREATE TYPE installment_status AS ENUM (
    'PENDING',
    'PARTIAL',
    'PAID',
    'OVERDUE',
    'CANCELLED_BY_PREPAYMENT'
);

-- ─────────────────────────────────────────────────────────────────────────────
-- MÓDULO: PAGOS Y LIBRO MAYOR
-- ─────────────────────────────────────────────────────────────────────────────

CREATE TYPE payment_status AS ENUM (
    'APPLIED',
    'REVERSED',
    'PENDING_CONFIRMATION'
);

CREATE TYPE payment_kind AS ENUM (
    'NORMAL',
    'REVERSAL'
);

CREATE TYPE payment_method_type AS ENUM (
    'EFECTIVO',
    'TRANSFERENCIA',
    'YAPE',
    'PLIN',
    'TARJETA',
    'DEPOSITO_VENTANILLA'
);

CREATE TYPE transaction_type AS ENUM (
    'DISBURSEMENT',
    'PAYMENT',
    'REVERSAL',
    'CUSTOMER_CREDIT_BALANCE',
    'WRITE_OFF',
    'ADJUSTMENT',
    'CASH_TRANSFER',
    'CASH_EXPENSE',
    'CASH_INCOME'
);

CREATE TYPE transaction_dir AS ENUM (
    'INFLOW',
    'OUTFLOW'
);

-- ─────────────────────────────────────────────────────────────────────────────
-- MÓDULO: CAJA Y TESORERÍA
-- ─────────────────────────────────────────────────────────────────────────────

CREATE TYPE cashbox_status AS ENUM (
    'ABIERTA',
    'CERRADA'
);

CREATE TYPE cash_movement_type AS ENUM (
    'INGRESO_EXTRAORDINARIO',
    'EGRESO_GASTO'
);

CREATE TYPE transfer_status AS ENUM (
    'EN_TRANSITO',
    'COMPLETED',
    'CANCELLED'
);

CREATE TYPE arqueo_result AS ENUM (
    'CUADRADO',
    'SOBRANTE',
    'FALTANTE'
);

-- ─────────────────────────────────────────────────────────────────────────────
-- MÓDULO: COBRANZA
-- ─────────────────────────────────────────────────────────────────────────────

CREATE TYPE collection_action_type AS ENUM (
    'LLAMADA',
    'WHATSAPP',
    'VISITA_DOMICILIARIA',
    'NOTIFICACION_ESCRITA',
    'PROMESA_DE_PAGO'
);

CREATE TYPE collection_action_status AS ENUM (
    'ACTIVA',
    'CUMPLIDA',
    'PROMESA_INCUMPLIDA',
    'CERRADA'
);

-- ─────────────────────────────────────────────────────────────────────────────
-- MÓDULO: INTEGRACIONES Y CONCILIACIÓN
-- ─────────────────────────────────────────────────────────────────────────────

CREATE TYPE integration_event_status AS ENUM (
    'RECEIVED',
    'PROCESSED',
    'FAILED',
    'DEAD_LETTER',
    'FAILED_NEEDS_REVIEW'
);

CREATE TYPE reconciliation_status AS ENUM (
    'CONCILIADO_AUTOMATICO',
    'PENDIENTE_CONCILIACION',
    'DESCUADRADO',
    'RECONCILED'
);

-- ─────────────────────────────────────────────────────────────────────────────
-- MÓDULO: NOTIFICACIONES
-- ─────────────────────────────────────────────────────────────────────────────

CREATE TYPE notification_channel AS ENUM (
    'EMAIL',
    'SMS',
    'WHATSAPP',
    'PUSH'
);

CREATE TYPE notification_status AS ENUM (
    'PENDING',
    'SENT',
    'FAILED'
);

-- ─────────────────────────────────────────────────────────────────────────────
-- MÓDULO: ADMINISTRACIÓN
-- ─────────────────────────────────────────────────────────────────────────────

CREATE TYPE holiday_scope AS ENUM (
    'NACIONAL',
    'SUCURSAL'
);
