-- =============================================================================
-- MIGRACIÓN 00 — EXTENSIONES Y TIPOS ENUMERADOS
-- Modelo: Fintech 100% Digital (sin sucursales físicas ni cajas de efectivo)
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
-- ROLES AJUSTADOS: sin CASHIER (cajero físico) ni COLLECTOR de campo.
-- El cobrador digital gestiona recordatorios/cobranza desde el backoffice.
-- ─────────────────────────────────────────────────────────────────────────────

CREATE TYPE user_role AS ENUM (
    'ROLE_ADMIN',       -- Administrador total del sistema
    'ROLE_ANALYST',     -- Analista de crédito: evalúa y aprueba solicitudes
    'ROLE_OPERATOR',    -- Operador: procesa desembolsos y pagos digitales
    'ROLE_COLLECTOR',   -- Gestor de cobranza digital: seguimiento de mora
    'ROLE_AUDITOR',     -- Solo lectura: bitácora y reportes de cumplimiento
    'ROLE_CLIENT'       -- Prestatario: accede al portal de autoservicio
);

CREATE TYPE user_status_type AS ENUM (
    'ACTIVE',
    'INACTIVE',
    'LOCKED'
);

-- ─────────────────────────────────────────────────────────────────────────────
-- MÓDULO: CUENTAS BANCARIAS DEL CLIENTE
-- Destino de desembolsos y origen de pagos por transferencia
-- ─────────────────────────────────────────────────────────────────────────────

CREATE TYPE bank_account_type AS ENUM (
    'CUENTA_CORRIENTE',
    'CUENTA_AHORRO',
    'TARJETA_DEBITO',
    'BILLETERA_DIGITAL'  -- Yape, Plin, Tunki, etc.
);

CREATE TYPE bank_account_status AS ENUM (
    'ACTIVE',
    'INACTIVE',
    'PENDING_VERIFICATION'
);

-- ─────────────────────────────────────────────────────────────────────────────
-- MÓDULO: DESEMBOLSOS BANCARIOS
-- ─────────────────────────────────────────────────────────────────────────────

CREATE TYPE disbursement_status AS ENUM (
    'PENDING',       -- Generado, pendiente de envío al banco
    'PROCESSING',    -- Enviado al banco / pasarela, en tránsito
    'COMPLETED',     -- Confirmado por el banco / webhook de confirmación
    'FAILED',        -- Rechazado por el banco (fondos insuficientes, cuenta inválida)
    'REVERSED'       -- Revertido por error o fraude
);

CREATE TYPE disbursement_channel AS ENUM (
    'TRANSFERENCIA_BANCARIA',  -- Transferencia interbancaria (CCI)
    'YAPE',
    'PLIN',
    'DEPOSITO_CCI'
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
-- Los métodos son 100% digitales: sin EFECTIVO ni DEPOSITO_VENTANILLA
-- ─────────────────────────────────────────────────────────────────────────────

CREATE TYPE payment_status AS ENUM (
    'PENDING_CONFIRMATION',  -- Registrado, pendiente de confirmación bancaria
    'APPLIED',               -- Confirmado y aplicado al préstamo
    'REVERSED'               -- Revertido mediante contra-asiento
);

CREATE TYPE payment_kind AS ENUM (
    'NORMAL',
    'REVERSAL'
);

CREATE TYPE payment_method_type AS ENUM (
    'TRANSFERENCIA_BANCARIA',  -- CCI a cuenta de la fintech
    'YAPE',
    'PLIN',
    'TARJETA_DEBITO',
    'TARJETA_CREDITO',
    'PASARELA_PAGO'            -- Niubiz, Culqi, Mercado Pago, etc.
);

CREATE TYPE transaction_type AS ENUM (
    'DISBURSEMENT',           -- Desembolso al cliente (OUTFLOW)
    'PAYMENT',                -- Cobro de cuota (INFLOW)
    'REVERSAL',               -- Contra-asiento de un pago o desembolso
    'CUSTOMER_CREDIT_BALANCE',-- Saldo a favor del cliente
    'WRITE_OFF',              -- Castigo contable
    'ADJUSTMENT'              -- Ajuste manual aprobado
);

CREATE TYPE transaction_dir AS ENUM (
    'INFLOW',   -- Dinero que entra: cobros de cuotas
    'OUTFLOW'   -- Dinero que sale: desembolsos
);

-- ─────────────────────────────────────────────────────────────────────────────
-- MÓDULO: COBRANZA DIGITAL
-- Sin VISITA_DOMICILIARIA (no hay cobradores de campo)
-- ─────────────────────────────────────────────────────────────────────────────

CREATE TYPE collection_action_type AS ENUM (
    'LLAMADA_TELEFONICA',
    'WHATSAPP',
    'EMAIL',
    'SMS',
    'NOTIFICACION_PUSH',
    'PROMESA_DE_PAGO',
    'ACUERDO_DE_PAGO_DIGITAL'
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
