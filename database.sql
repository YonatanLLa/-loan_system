-- =============================================================================
-- SISTEMA DE PRÉSTAMOS — ESQUEMA COMPLETO (ARCHIVO ÚNICO GENERADO)
-- Modelo: Fintech 100% Digital (sin cajas físicas ni sucursales)
-- Base de Datos: PostgreSQL 15+
--
-- ⚠ ARCHIVO GENERADO — NO EDITAR A MANO.
--   Fuente de verdad: database/ (migrations, functions, indexes, seeds),
--   concatenados en el mismo orden de los \ir de database/run_all.sql.
--   Para instalar se recomienda run_all.sql (atómico y con ON_ERROR_STOP).
-- =============================================================================

-- >>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>
-- >>> database/migrations/00_extensions_and_enums.sql
-- >>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>
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

-- >>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>
-- >>> database/functions/fn_updated_at.sql
-- >>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>
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

-- >>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>
-- >>> database/migrations/01_admin_config.sql
-- >>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>
-- =============================================================================
-- MIGRACIÓN 01 — ADMINISTRACIÓN Y CONFIGURACIÓN GLOBAL
-- Modelo: Fintech 100% Digital
-- Dominio: US-39 (Empresa), US-41 (Feriados), US-42 (Catálogos y Plantillas)
-- ELIMINADO: branches (sucursales físicas), zones (zonas de cobranza)
-- AGREGADO:  company_bank_accounts (cuentas bancarias de la fintech)
-- Dependencias: 00_extensions_and_enums.sql
-- =============================================================================

-- ─────────────────────────────────────────────────────────────────────────────
-- Monedas (ISO 4217)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE currencies (
    id          UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    code        CHAR(3)     NOT NULL UNIQUE,  -- PEN, USD, EUR …
    name        VARCHAR(60) NOT NULL,
    symbol      VARCHAR(5)  NOT NULL,
    is_active   BOOLEAN     NOT NULL DEFAULT TRUE,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

COMMENT ON TABLE currencies IS 'Catálogo ISO 4217 de monedas habilitadas en el sistema.';

-- ─────────────────────────────────────────────────────────────────────────────
-- Empresa / Institución financiera digital (US-39)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE company (
    id                  UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    legal_name          VARCHAR(200) NOT NULL,              -- Razón social
    trade_name          VARCHAR(200),                       -- Nombre comercial / marca
    tax_id              VARCHAR(20)  NOT NULL UNIQUE,       -- RUC / NIT / RFC / CIF
    legal_address       TEXT         NOT NULL,
    support_phone       VARCHAR(20),
    support_email       VARCHAR(150),
    website_url         VARCHAR(255),                       -- Portal web de la fintech
    legal_rep_name      VARCHAR(200),                       -- Representante legal
    legal_rep_doc       VARCHAR(30),
    logo_url            TEXT,                               -- URI en Object Storage
    default_currency_id UUID         REFERENCES currencies(id),
    created_at          TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

COMMENT ON TABLE company IS 'Datos corporativos e institucionales de la Fintech (US-39). Entidad única, sin sucursales físicas.';

CREATE TRIGGER tg_company_updated_at
    BEFORE UPDATE ON company
    FOR EACH ROW EXECUTE FUNCTION fn_set_updated_at();

-- ─────────────────────────────────────────────────────────────────────────────
-- Cuentas bancarias de la Fintech — origen de desembolsos y destino de cobros
-- Reemplaza a: branches + cash_boxes (infraestructura física eliminada)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE company_bank_accounts (
    id                    UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    bank_name             VARCHAR(100) NOT NULL,            -- BCP, Interbank, BBVA, Scotiabank, etc.
    account_number_masked VARCHAR(30)  NOT NULL,            -- Solo últimos 4 dígitos visibles (RNF-05)
    cci                   VARCHAR(30),                      -- Código de Cuenta Interbancario (Perú)
    currency_id           UUID         NOT NULL REFERENCES currencies(id),
    is_disbursement       BOOLEAN      NOT NULL DEFAULT TRUE,  -- ¿Se usa para desembolsar créditos?
    is_collection         BOOLEAN      NOT NULL DEFAULT TRUE,  -- ¿Se usa para recibir cobros?
    is_active             BOOLEAN      NOT NULL DEFAULT TRUE,
    created_at            TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at            TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

COMMENT ON TABLE company_bank_accounts IS
    'Cuentas bancarias de la Fintech. Origen de desembolsos y destino de cobros por transferencia (reemplaza cash_boxes y branches).';

CREATE TRIGGER tg_company_bank_accounts_updated_at
    BEFORE UPDATE ON company_bank_accounts
    FOR EACH ROW EXECUTE FUNCTION fn_set_updated_at();

-- ─────────────────────────────────────────────────────────────────────────────
-- Feriados y días no hábiles (US-41)
-- Sin branch_id: aplica globalmente para toda la operación digital
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE holidays (
    id           UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    holiday_date DATE        NOT NULL,
    name         VARCHAR(150) NOT NULL,
    is_recurring BOOLEAN     NOT NULL DEFAULT FALSE,  -- Repite cada año (ej. Navidad)
    created_at   TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (holiday_date)
);

COMMENT ON TABLE holidays IS 'Calendario de feriados nacionales para ajuste de vencimientos de cuotas (US-41, RF-14.2).';

-- ─────────────────────────────────────────────────────────────────────────────
-- Tipos de documento de identidad — catálogo configurable (US-42)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE document_types (
    id               UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    code             VARCHAR(10) NOT NULL UNIQUE,  -- DNI, CE, RUC, PAS
    name             VARCHAR(60) NOT NULL,
    char_length      SMALLINT,                     -- Longitud exacta (8 para DNI peruano)
    validation_regex VARCHAR(100),                 -- Regex de validación de formato
    is_active        BOOLEAN     NOT NULL DEFAULT TRUE
);

COMMENT ON TABLE document_types IS 'Catálogo configurable de tipos de documento de identidad (US-42, RF-42.1).';

-- ─────────────────────────────────────────────────────────────────────────────
-- Métodos de pago habilitados — solo canales digitales (US-42)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE payment_methods (
    id              UUID                PRIMARY KEY DEFAULT gen_random_uuid(),
    name            VARCHAR(80)         NOT NULL,          -- 'Yape', 'Transferencia BCP', etc.
    method_type     payment_method_type NOT NULL,
    provider_code   VARCHAR(50),                           -- Código interno del proveedor/pasarela
    webhook_enabled BOOLEAN             NOT NULL DEFAULT FALSE, -- ¿Tiene confirmación automática por webhook?
    is_active       BOOLEAN             NOT NULL DEFAULT TRUE,
    created_at      TIMESTAMPTZ         NOT NULL DEFAULT NOW()
);

COMMENT ON TABLE payment_methods IS 'Métodos de pago digitales habilitados en el sistema. Sin efectivo ni ventanilla (US-42, RF-42.2).';

-- ─────────────────────────────────────────────────────────────────────────────
-- Plantillas legales de contratos — versionadas (US-42, US-15)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE legal_templates (
    id          UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    code        VARCHAR(50) NOT NULL,              -- 'CONTRATO_PRESTAMO', 'PAGARE'
    version     SMALLINT    NOT NULL DEFAULT 1,
    content     TEXT        NOT NULL,              -- HTML/Markdown con variables {{}}
    is_current  BOOLEAN     NOT NULL DEFAULT TRUE,
    created_by  UUID,                              -- FK → users (se agrega en migración 02)
    created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (code, version)
);

COMMENT ON TABLE legal_templates IS
    'Plantillas versionadas de contratos y pagarés con variables dinámicas. Se firman digitalmente (US-42, RF-42.3, US-15).';

-- >>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>
-- >>> database/migrations/02_security_users.sql
-- >>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>
-- =============================================================================
-- MIGRACIÓN 02 — SEGURIDAD, USUARIOS Y AUDITORÍA
-- Modelo: Fintech 100% Digital
-- Dominio: US-01 (Autenticación), US-02 (RBAC), US-03 (AuditLog), US-43
-- ELIMINADO: branch_id en users, tabla user_zones (sin sucursales ni zonas)
-- Dependencias: 01_admin_config.sql
-- =============================================================================

-- ─────────────────────────────────────────────────────────────────────────────
-- Usuarios del sistema — backoffice y portal cliente (US-01, US-43)
-- Sin branch_id: la fintech es 100% digital, operación centralizada
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE users (
    id                  UUID             PRIMARY KEY DEFAULT gen_random_uuid(),
    email               VARCHAR(150)     NOT NULL UNIQUE,
    password_hash       VARCHAR(255)     NOT NULL,            -- BCrypt / Argon2id con salt
    full_name           VARCHAR(200)     NOT NULL,
    phone               VARCHAR(20),
    role                user_role        NOT NULL,
    status              user_status_type NOT NULL DEFAULT 'ACTIVE',
    -- Control de intentos fallidos (RF-01.3: bloqueo tras 5 intentos)
    failed_login_count  SMALLINT         NOT NULL DEFAULT 0,
    locked_until        TIMESTAMPTZ,
    last_login_at       TIMESTAMPTZ,
    -- Flujo de invitación (US-43, RF-43.2)
    invitation_token    VARCHAR(255),
    invitation_sent_at  TIMESTAMPTZ,
    password_changed_at TIMESTAMPTZ,
    created_at          TIMESTAMPTZ      NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ      NOT NULL DEFAULT NOW()
);

COMMENT ON TABLE users IS
    'Usuarios del backoffice y portal cliente con control de roles RBAC (US-01, US-02, US-43). Sin branch_id: operación 100% digital centralizada.';
COMMENT ON COLUMN users.password_hash IS
    'Hash con BCrypt o Argon2id. Nunca almacenar la contraseña en texto plano.';
COMMENT ON COLUMN users.failed_login_count IS
    'Se incrementa con cada intento fallido. Se reinicia al hacer login exitoso (RF-01.3).';

CREATE TRIGGER tg_users_updated_at
    BEFORE UPDATE ON users
    FOR EACH ROW EXECUTE FUNCTION fn_set_updated_at();

-- FK diferida: legal_templates.created_by → users
ALTER TABLE legal_templates
    ADD CONSTRAINT fk_template_user
    FOREIGN KEY (created_by) REFERENCES users(id);

-- ─────────────────────────────────────────────────────────────────────────────
-- Refresh Tokens — revocables individualmente por dispositivo (US-01, RF-01.2)
-- Permite sesión simultánea en celular y laptop sin exponer credenciales
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE refresh_tokens (
    id          UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id     UUID         NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    token_hash  VARCHAR(255) NOT NULL UNIQUE,  -- Hash del token; nunca el token en claro
    issued_at   TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    expires_at  TIMESTAMPTZ  NOT NULL,
    revoked_at  TIMESTAMPTZ,                   -- NULL = aún vigente
    user_ip     INET,
    user_agent  TEXT                           -- 'Chrome/Windows', 'App Android 14', etc.
);

COMMENT ON TABLE refresh_tokens IS
    'Refresh Tokens rotativos e invalidables individualmente por dispositivo (US-01, RF-01.2, RF-01.4).';

-- ─────────────────────────────────────────────────────────────────────────────
-- Bitácora de auditoría inmutable (US-03)
-- Todo cambio en entidades financieras sensibles queda registrado aquí
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE audit_logs (
    id           BIGSERIAL    PRIMARY KEY,
    entity_name  VARCHAR(100) NOT NULL,    -- Nombre de la tabla afectada: 'loans', 'payments'
    entity_id    VARCHAR(100) NOT NULL,    -- ID del registro afectado
    action       VARCHAR(50)  NOT NULL,    -- 'CREATE', 'UPDATE', 'APPROVE', 'DISBURSE', 'REVERSE'
    state_before JSONB,                    -- Snapshot del estado anterior (NULL si es INSERT)
    state_after  JSONB,                    -- Snapshot del estado nuevo
    user_id      UUID         REFERENCES users(id),
    user_ip      INET,                     -- IP desde donde se realizó la acción
    created_at   TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

COMMENT ON TABLE audit_logs IS
    'Bitácora inmutable de auditoría. SOLO INSERT. Nunca UPDATE ni DELETE (US-03, RF-03.1).';
COMMENT ON COLUMN audit_logs.state_before IS
    'Estado del registro ANTES de la modificación en formato JSONB. Permite reconstruir cualquier estado histórico.';
COMMENT ON COLUMN audit_logs.state_after IS
    'Estado del registro DESPUÉS de la modificación. Junto con state_before forma la pista de auditoría completa.';

-- >>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>
-- >>> database/migrations/03_customers_kyc.sql
-- >>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>
-- =============================================================================
-- MIGRACIÓN 03 — CLIENTES, KYC Y CUENTAS BANCARIAS DEL CLIENTE
-- Modelo: Fintech 100% Digital
-- Dominio: US-04 (Registro), US-05 (KYC), US-06 (Vista 360), US-26 (Portal)
-- AGREGADO: customer_bank_accounts — tarjeta/CCI a donde se deposita el préstamo
-- Dependencias: 02_security_users.sql
-- =============================================================================

-- ─────────────────────────────────────────────────────────────────────────────
-- Clientes (personas naturales o jurídicas) — (US-04)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE customers (
    id                      UUID            PRIMARY KEY DEFAULT gen_random_uuid(),
    person_type             person_type     NOT NULL DEFAULT 'NATURAL',
    doc_type                document_type   NOT NULL,
    doc_number              VARCHAR(20)     NOT NULL,
    full_name               VARCHAR(200)    NOT NULL,   -- Nombre completo o Razón social
    email                   VARCHAR(150),
    phone_primary           VARCHAR(20),
    phone_secondary         VARCHAR(20),
    address                 TEXT,
    district                VARCHAR(100),
    birth_date              DATE,                       -- Requerido para validación de edad (RF-10.2)
    -- ── Perfil socioeconómico ────────────────────────────────────────────────
    monthly_net_income      NUMERIC(18,4)   NOT NULL DEFAULT 0,
    occupation              VARCHAR(150),
    employer_name           VARCHAR(200),
    work_seniority_months   SMALLINT        NOT NULL DEFAULT 0,
    -- ── Estado KYC Digital (US-05) ───────────────────────────────────────────
    kyc_status              kyc_status_type NOT NULL DEFAULT 'PENDING',
    kyc_verified_by         UUID            REFERENCES users(id),
    kyc_verified_at         TIMESTAMPTZ,
    kyc_notes               TEXT,
    kyc_expires_at          TIMESTAMPTZ,
    -- ── Saldo a favor del cliente (US-17, RF-17.2) ───────────────────────────
    credit_balance          NUMERIC(18,4)   NOT NULL DEFAULT 0
                                            CHECK (credit_balance >= 0),
    -- ── Vínculo con el portal de autoservicio (US-26) ────────────────────────
    portal_user_id          UUID            REFERENCES users(id),
    -- ── Consentimiento GDPR / LPDP digital (RF-05.4) ─────────────────────────
    consent_given_at        TIMESTAMPTZ,
    consent_ip              INET,
    -- ── Metadatos ───────────────────────────────────────────────────────────
    created_by              UUID            REFERENCES users(id),
    created_at              TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    updated_at              TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    -- RF-04.2: Unicidad del documento de identidad
    UNIQUE (doc_type, doc_number)
);

COMMENT ON TABLE customers IS
    'Clientes prestatarios (personas naturales o jurídicas). Un cliente puede tener múltiples préstamos activos (US-04, US-05, US-06).';
COMMENT ON COLUMN customers.credit_balance IS
    'Saldo acreedor generado por sobrepagos; reutilizable en otros préstamos del mismo cliente (US-17, RF-17.2).';
COMMENT ON COLUMN customers.kyc_status IS
    'El desembolso está bloqueado si no es VERIFIED (US-05, RF-05.3).';

CREATE TRIGGER tg_customers_updated_at
    BEFORE UPDATE ON customers
    FOR EACH ROW EXECUTE FUNCTION fn_set_updated_at();

-- ─────────────────────────────────────────────────────────────────────────────
-- Cuentas bancarias del cliente — destino de desembolsos y origen de pagos
-- Reemplaza la lógica de "caja física" por datos de cuenta/tarjeta del cliente
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE customer_bank_accounts (
    id              UUID                PRIMARY KEY DEFAULT gen_random_uuid(),
    customer_id     UUID                NOT NULL REFERENCES customers(id) ON DELETE CASCADE,
    bank_name       VARCHAR(100)        NOT NULL,            -- BCP, Interbank, BBVA, Yape, etc.
    account_type    bank_account_type   NOT NULL,            -- CUENTA_AHORRO, TARJETA_DEBITO, etc.
    -- Datos enmascarados: solo últimos 4 dígitos visibles para el analista (RNF-05)
    account_number_masked VARCHAR(30)   NOT NULL,            -- '**** **** **** 4821'
    cci              VARCHAR(30),                            -- Código de Cuenta Interbancario (Perú, 20 dígitos)
    phone_wallet     VARCHAR(20),                            -- Teléfono vinculado a Yape/Plin
    -- Verificación de titularidad (RF-04.3: la cuenta debe pertenecer al cliente)
    is_verified      BOOLEAN            NOT NULL DEFAULT FALSE,
    verified_by      UUID               REFERENCES users(id),
    verified_at      TIMESTAMPTZ,
    -- Una sola cuenta activa como destino principal de desembolsos
    is_primary       BOOLEAN            NOT NULL DEFAULT FALSE,
    status           bank_account_status NOT NULL DEFAULT 'PENDING_VERIFICATION',
    created_at       TIMESTAMPTZ        NOT NULL DEFAULT NOW(),
    updated_at       TIMESTAMPTZ        NOT NULL DEFAULT NOW()
);

COMMENT ON TABLE customer_bank_accounts IS
    'Cuentas bancarias/tarjetas/billeteras digitales del cliente. Destino de desembolsos y origen de pagos por transferencia (US-04, RF-04.3).';
COMMENT ON COLUMN customer_bank_accounts.cci IS
    'Código de Cuenta Interbancario de 20 dígitos. Requerido para transferencias interbancarias en Perú.';
COMMENT ON COLUMN customer_bank_accounts.is_primary IS
    'Solo una cuenta puede ser primaria por cliente. El desembolso va a la cuenta primaria verificada.';

CREATE TRIGGER tg_customer_bank_accounts_updated_at
    BEFORE UPDATE ON customer_bank_accounts
    FOR EACH ROW EXECUTE FUNCTION fn_set_updated_at();

-- ─────────────────────────────────────────────────────────────────────────────
-- Expediente digital KYC — documentos adjuntos (US-05, RF-05.1)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE customer_documents (
    id            UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    customer_id   UUID        NOT NULL REFERENCES customers(id) ON DELETE CASCADE,
    doc_category  VARCHAR(60) NOT NULL,
    -- Categorías esperadas: DNI_FRONTAL | DNI_REVERSO | BOLETA_PAGO |
    --                       RECIBO_SERVICIOS | SELFIE | ESTADO_CUENTA_BANCO | OTRO
    file_hash     VARCHAR(100) NOT NULL,    -- SHA-256 del archivo para verificar integridad
    storage_uri   TEXT         NOT NULL,    -- URI en Object Storage (S3 / Azure Blob)
    mime_type     VARCHAR(60),
    uploaded_by   UUID         REFERENCES users(id),
    uploaded_at   TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

COMMENT ON TABLE customer_documents IS
    'Expediente KYC digital: archivos adjuntos del cliente almacenados en Object Storage (US-05, RNF-04).';
COMMENT ON COLUMN customer_documents.storage_uri IS
    'Solo se guarda la URI/hash en BD. El archivo físico reside en Object Storage (RNF-04).';

-- ─────────────────────────────────────────────────────────────────────────────
-- Códigos OTP para verificación del portal cliente (US-26, RF-26.2)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE otp_verifications (
    id          UUID                 PRIMARY KEY DEFAULT gen_random_uuid(),
    customer_id UUID                 NOT NULL REFERENCES customers(id) ON DELETE CASCADE,
    target      VARCHAR(150)         NOT NULL,  -- Email o número de teléfono
    channel     notification_channel NOT NULL,  -- SMS | WHATSAPP | EMAIL
    otp_hash    VARCHAR(255)         NOT NULL,  -- Hash del código de 6 dígitos
    expires_at  TIMESTAMPTZ          NOT NULL,
    verified_at TIMESTAMPTZ,                    -- NULL = aún no verificado
    created_at  TIMESTAMPTZ          NOT NULL DEFAULT NOW()
);

COMMENT ON TABLE otp_verifications IS
    'Códigos OTP de verificación para onboarding y operaciones sensibles del portal cliente (US-26, RF-26.2).';

-- >>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>
-- >>> database/migrations/04_loan_products.sql
-- >>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>
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

-- >>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>
-- >>> database/migrations/05_loan_applications.sql
-- >>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>
-- =============================================================================
-- MIGRACIÓN 05 — ORIGINACIÓN, SOLICITUDES Y EVALUACIÓN CREDITICIA
-- Dominio: US-10 (Solicitud), US-11 (Simulador), US-12 (Motor evaluación)
-- Dependencias: 03_customers_kyc.sql, 04_loan_products.sql
-- =============================================================================

-- ─────────────────────────────────────────────────────────────────────────────
-- Solicitudes de crédito (US-10, US-12)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE loan_applications (
    id                  UUID                PRIMARY KEY DEFAULT gen_random_uuid(),
    customer_id         UUID                NOT NULL REFERENCES customers(id),
    product_id          UUID                NOT NULL REFERENCES loan_products(id),

    -- ── Condiciones solicitadas (US-10, RF-10.1) ────────────────────────────
    requested_amount    NUMERIC(18,4)       NOT NULL CHECK (requested_amount > 0),
    approved_amount     NUMERIC(18,4),                -- Puede diferir si se aprueba con condiciones
    term                SMALLINT            NOT NULL CHECK (term >= 1),
    frequency           payment_frequency   NOT NULL,
    destination         loan_purpose_type   NOT NULL DEFAULT 'CONSUMO',

    -- ── Estados del proceso (US-10, RF-10.3) ────────────────────────────────
    status              application_status  NOT NULL DEFAULT 'SOLICITADO',

    -- ── Evaluación automática (US-12, RF-12.1 / RF-12.2) ───────────────────
    -- DTI = (Cuota proyectada + Otras cuotas activas) / Ingreso neto × 100
    dti_ratio           NUMERIC(8,4),
    score               NUMERIC(8,4),
    system_recommendation credit_decision,

    -- ── Dictamen del analista (US-12, RF-12.3) ──────────────────────────────
    -- RNF-09: una vez APROBADO o RECHAZADO no se puede cambiar sin reapertura
    decision            VARCHAR(30),
    decision_notes      TEXT,                         -- Justificación obligatoria (RF-12.3)
    decision_by         UUID                REFERENCES users(id),
    decided_at          TIMESTAMPTZ,

    -- ── Canal de origen ─────────────────────────────────────────────────────
    originated_from     VARCHAR(20)         NOT NULL DEFAULT 'BACKOFFICE',
    -- BACKOFFICE (analista) | PORTAL (cliente via US-27)

    created_by          UUID                REFERENCES users(id),
    created_at          TIMESTAMPTZ         NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ         NOT NULL DEFAULT NOW()
);

COMMENT ON TABLE loan_applications IS
    'Solicitudes de crédito con evaluación automática (DTI) y dictamen del analista (US-10, US-12).';
COMMENT ON COLUMN loan_applications.dti_ratio IS
    'Ratio Cuota/Ingreso en %. ≤35% → APROBABLE; 35-50% → REVISION_MANUAL; >50% → RECHAZO (RF-12.2).';
COMMENT ON COLUMN loan_applications.decision_notes IS
    'Obligatorio al dictaminar RECHAZADO. El sistema devuelve 422 si se omite (RF-12.3, CA-12.2).';

CREATE TRIGGER tg_loan_applications_updated_at
    BEFORE UPDATE ON loan_applications
    FOR EACH ROW EXECUTE FUNCTION fn_set_updated_at();

-- >>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>
-- >>> database/migrations/06_loans_schedules.sql
-- >>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>
-- =============================================================================
-- MIGRACIÓN 06 — PRÉSTAMOS, CRONOGRAMAS Y CUOTAS
-- Modelo: Fintech 100% Digital
-- Dominio: US-13 (Desembolso Digital), US-14 (Cronograma Snapshot), US-15 (Contrato)
--          US-31 (Reestructuración — versionado de schedule)
-- ELIMINADO: cashbox_id (sin cajas físicas de efectivo)
-- AGREGADO:  disbursement_bank_account_id (cuenta de la fintech que envió el dinero)
--            customer_bank_account_id (cuenta del cliente que recibió el dinero)
-- Dependencias: 05_loan_applications.sql, 03_customers_kyc.sql, 01_admin_config.sql
-- =============================================================================

-- ─────────────────────────────────────────────────────────────────────────────
-- Préstamo activo — parámetros congelados al momento del desembolso (US-13)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE loans (
    id                          UUID                PRIMARY KEY DEFAULT gen_random_uuid(),
    -- Trazabilidad de origen
    application_id              UUID                UNIQUE REFERENCES loan_applications(id),
    customer_id                 UUID                NOT NULL REFERENCES customers(id),
    product_id                  UUID                NOT NULL REFERENCES loan_products(id),

    -- ── Parámetros financieros CONGELADOS al desembolsar (snapshot) ──────────
    -- RNF-06: los cambios en loan_products NO deben afectar préstamos vigentes
    principal                   NUMERIC(18,4)       NOT NULL CHECK (principal > 0),
    disbursed_amount            NUMERIC(18,4)       NOT NULL,
    -- Neto entregado (= principal - comisión deductiva, si aplica)
    annual_rate                 NUMERIC(12,6)       NOT NULL,
    amortization_method         amortization_method NOT NULL,
    frequency                   payment_frequency   NOT NULL,
    term                        SMALLINT            NOT NULL,
    grace_days                  SMALLINT            NOT NULL DEFAULT 0,
    mora_rate_per_day           NUMERIC(12,6)       NOT NULL DEFAULT 0,
    mora_base                   mora_base_type      NOT NULL DEFAULT 'SALDO_CAPITAL_VENCIDO',
    opening_fee_amount          NUMERIC(18,4)       NOT NULL DEFAULT 0,
    periodic_fee                NUMERIC(18,4)       NOT NULL DEFAULT 0,
    prepayment_penalty_rate     NUMERIC(12,6)       NOT NULL DEFAULT 0,

    -- ── Fechas clave ─────────────────────────────────────────────────────────
    disbursement_date           DATE                NOT NULL,
    -- Inicio del cómputo de intereses (RF-13.4, RF-14.1)
    first_due_date              DATE                NOT NULL,

    -- ── Totales proyectados (calculados al generar el cronograma) ────────────
    total_scheduled_interest    NUMERIC(18,4)       NOT NULL DEFAULT 0,
    total_scheduled_fees        NUMERIC(18,4)       NOT NULL DEFAULT 0,

    -- ── Acumulados realizados por pagos recibidos ────────────────────────────
    total_paid_principal        NUMERIC(18,4)       NOT NULL DEFAULT 0,
    total_paid_interest         NUMERIC(18,4)       NOT NULL DEFAULT 0,
    total_paid_fees             NUMERIC(18,4)       NOT NULL DEFAULT 0,
    total_paid_mora             NUMERIC(18,4)       NOT NULL DEFAULT 0,

    -- ── Estado del préstamo (US-13, sección 3.9 del README) ─────────────────
    status                      loan_status         NOT NULL DEFAULT 'VIGENTE',
    close_reason                loan_close_reason,
    closed_at                   TIMESTAMPTZ,

    -- ── Refinanciamiento / Consolidación (US-32) ────────────────────────────
    purpose_type                loan_purpose_type   NOT NULL DEFAULT 'CONSUMO',
    refinanced_from_loan_id     UUID                REFERENCES loans(id),

    -- ── Desembolso Digital (reemplaza cashbox_id) ────────────────────────────
    -- Cuenta de la Fintech desde donde salió la transferencia
    disbursement_bank_account_id UUID               REFERENCES company_bank_accounts(id),
    -- Cuenta del cliente que recibió el depósito
    customer_bank_account_id    UUID                REFERENCES customer_bank_accounts(id),
    disbursed_by                UUID                REFERENCES users(id),

    -- ── Idempotencia del desembolso (US-13, RF-13.2) ────────────────────────
    idempotency_key             VARCHAR(100)        UNIQUE,

    created_at                  TIMESTAMPTZ         NOT NULL DEFAULT NOW(),
    updated_at                  TIMESTAMPTZ         NOT NULL DEFAULT NOW()
);

COMMENT ON TABLE loans IS
    'Préstamos desembolsados digitalmente. Los parámetros financieros quedan congelados (snapshot) al momento del desembolso (US-13, RNF-06).';
COMMENT ON COLUMN loans.disbursed_amount IS
    'Monto neto transferido al cliente. Puede diferir de principal si la comisión es deductiva (RF-13.5).';
COMMENT ON COLUMN loans.disbursement_bank_account_id IS
    'Cuenta bancaria de la Fintech desde la cual se realizó la transferencia al cliente. Reemplaza cashbox_id.';
COMMENT ON COLUMN loans.customer_bank_account_id IS
    'Cuenta bancaria/tarjeta/billetera del cliente que recibió el desembolso.';
COMMENT ON COLUMN loans.idempotency_key IS
    'Garantiza que reintentos de red no dupliquen desembolsos (RF-13.2, US-13, CA-13.2).';

CREATE TRIGGER tg_loans_updated_at
    BEFORE UPDATE ON loans
    FOR EACH ROW EXECUTE FUNCTION fn_set_updated_at();

-- ─────────────────────────────────────────────────────────────────────────────
-- Desembolsos bancarios — registro detallado de cada transferencia al cliente
-- Nuevo en modelo digital: registra el estado de la operación bancaria
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE disbursements (
    id                          UUID                 PRIMARY KEY DEFAULT gen_random_uuid(),
    loan_id                     UUID                 NOT NULL UNIQUE REFERENCES loans(id),
    -- Origen (Fintech) y Destino (Cliente)
    company_bank_account_id     UUID                 NOT NULL REFERENCES company_bank_accounts(id),
    customer_bank_account_id    UUID                 NOT NULL REFERENCES customer_bank_accounts(id),
    -- Monto y moneda
    amount                      NUMERIC(18,4)        NOT NULL CHECK (amount > 0),
    currency_id                 UUID                 NOT NULL REFERENCES currencies(id),
    -- Canal de transferencia
    channel                     disbursement_channel NOT NULL,
    -- Estado del proceso bancario
    status                      disbursement_status  NOT NULL DEFAULT 'PENDING',
    -- Referencias bancarias para conciliación
    operation_number            VARCHAR(100),    -- Número de operación del banco (llega por webhook)
    external_reference          VARCHAR(150),    -- Referencia de la pasarela o banco
    bank_response_code          VARCHAR(20),     -- Código de respuesta del banco ('00' = éxito)
    bank_response_message       TEXT,            -- Mensaje de error si status=FAILED
    -- Trazabilidad temporal
    initiated_at                TIMESTAMPTZ      NOT NULL DEFAULT NOW(),
    confirmed_at                TIMESTAMPTZ,     -- Cuando el banco confirmó la transferencia
    failed_at                   TIMESTAMPTZ,     -- Cuando el banco rechazó la operación
    -- Idempotencia
    idempotency_key             VARCHAR(100)     UNIQUE,
    initiated_by                UUID             REFERENCES users(id)
);

COMMENT ON TABLE disbursements IS
    'Registro de cada transferencia bancaria de desembolso. Permite rastrear el estado desde PENDING hasta COMPLETED o FAILED (US-13, RF-13.3).';
COMMENT ON COLUMN disbursements.operation_number IS
    'Número de operación bancaria recibido por webhook de confirmación. Usado para conciliación (US-37).';

-- ─────────────────────────────────────────────────────────────────────────────
-- Versiones del cronograma — inmutable tras el desembolso (US-14)
-- Permite múltiples versiones por reestructuración (US-31, RF-31.3)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE schedules (
    id               UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    loan_id          UUID        NOT NULL REFERENCES loans(id),
    snapshot_version SMALLINT    NOT NULL DEFAULT 1,
    -- v1 = cronograma original; v2+ = tras reestructuración
    generated_at     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    is_active        BOOLEAN     NOT NULL DEFAULT TRUE,
    -- Solo un schedule activo por préstamo (garantizado por trigger)
    notes            TEXT,
    UNIQUE (loan_id, snapshot_version)
);

COMMENT ON TABLE schedules IS
    'Versiones inmutables del cronograma. v1 = original; v2+ = reestructurado (US-14, US-31, RF-31.3).';

-- Trigger: desactiva versiones anteriores al activar una nueva
CREATE TRIGGER tg_schedules_single_active
    BEFORE INSERT OR UPDATE ON schedules
    FOR EACH ROW EXECUTE FUNCTION fn_enforce_single_active_schedule();

-- ─────────────────────────────────────────────────────────────────────────────
-- Cuotas del cronograma (US-14, RF-14.4 / RF-14.5)
--
-- REGLA CLAVE (RNF-11):
--   Los campos proyectados (principal, interest, fee, total_due) son INMUTABLES.
--   Los campos paid_* y late_fee son los únicos actualizables (acumuladores).
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE installments (
    id                  UUID                PRIMARY KEY DEFAULT gen_random_uuid(),
    schedule_id         UUID                NOT NULL REFERENCES schedules(id),
    loan_id             UUID                NOT NULL REFERENCES loans(id),
    installment_number  SMALLINT            NOT NULL,

    -- ── Proyectados — INMUTABLES tras desembolso ────────────────────────────
    due_date            DATE                NOT NULL,
    principal           NUMERIC(18,4)       NOT NULL CHECK (principal >= 0),
    interest            NUMERIC(18,4)       NOT NULL CHECK (interest  >= 0),
    fee                 NUMERIC(18,4)       NOT NULL DEFAULT 0,
    total_due           NUMERIC(18,4)       NOT NULL,
    -- total_due = principal + interest + fee

    -- ── Realizados — acumuladores actualizables por pagos ───────────────────
    paid_principal      NUMERIC(18,4)       NOT NULL DEFAULT 0
                                            CHECK (paid_principal  >= 0),
    paid_interest       NUMERIC(18,4)       NOT NULL DEFAULT 0
                                            CHECK (paid_interest   >= 0),
    paid_fee            NUMERIC(18,4)       NOT NULL DEFAULT 0
                                            CHECK (paid_fee        >= 0),
    late_fee            NUMERIC(18,4)       NOT NULL DEFAULT 0
                                            CHECK (late_fee        >= 0),
    -- Mora devengada calculada por el job diario
    paid_late_fee       NUMERIC(18,4)       NOT NULL DEFAULT 0
                                            CHECK (paid_late_fee   >= 0),

    -- ── Estado y mora ────────────────────────────────────────────────────────
    status              installment_status  NOT NULL DEFAULT 'PENDING',
    days_late           SMALLINT            NOT NULL DEFAULT 0,
    last_mora_calc_date DATE,
    -- Idempotencia del job diario de mora (US-19, RF-19.5)

    UNIQUE (loan_id, installment_number)
);

COMMENT ON TABLE installments IS
    'Cuotas del cronograma. Campos proyectados = inmutables; paid_* = acumuladores de pagos (US-14, RNF-11).';
COMMENT ON COLUMN installments.late_fee IS
    'Mora devengada calculada por el job nocturno (US-19). No capitaliza salvo allows_anatocismo=true.';
COMMENT ON COLUMN installments.last_mora_calc_date IS
    'Garantiza idempotencia del job diario: si ya se calculó hoy, no recalcula (RF-19.5).';

-- ─────────────────────────────────────────────────────────────────────────────
-- Cotizaciones de cancelación anticipada (US-30, RF-30.1 → RF-30.4)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE prepayment_quotes (
    id                    UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    loan_id               UUID        NOT NULL REFERENCES loans(id),
    as_of_date            DATE        NOT NULL,
    outstanding_principal NUMERIC(18,4) NOT NULL,
    accrued_interest      NUMERIC(18,4) NOT NULL DEFAULT 0,
    accrued_mora          NUMERIC(18,4) NOT NULL DEFAULT 0,
    prepayment_penalty    NUMERIC(18,4) NOT NULL DEFAULT 0,
    total_to_pay          NUMERIC(18,4) NOT NULL,
    quote_valid_until     DATE        NOT NULL,     -- Tipicamente fin de jornada (RF-30.3)
    is_applied            BOOLEAN     NOT NULL DEFAULT FALSE,
    generated_by          UUID        REFERENCES users(id),
    created_at            TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

COMMENT ON TABLE prepayment_quotes IS
    'Cotizaciones de liquidación anticipada con vigencia hasta fin de jornada (US-30, RF-30.1 → RF-30.4).';

-- >>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>
-- >>> database/migrations/07_payments_ledger.sql
-- >>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>
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

-- >>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>
-- >>> database/migrations/08_treasury_reconciliation.sql
-- >>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>
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

-- >>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>
-- >>> database/migrations/09_collections_notifications.sql
-- >>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>
-- =============================================================================
-- MIGRACIÓN 09 — MORA, COBRANZA Y NOTIFICACIONES
-- Modelo: Fintech 100% Digital (sin cobradores de campo ni visitas)
-- Dominio: US-19 (Job mora), US-20 (Aging), US-21 (Acciones), US-22 (Castigo)
-- Dependencias: 06_loans_schedules.sql, 02_security_users.sql
-- =============================================================================

-- ─────────────────────────────────────────────────────────────────────────────
-- Acciones de cobranza — gestiones, llamadas y promesas de pago (US-21, RF-21.2)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE collection_actions (
    id              UUID                        PRIMARY KEY DEFAULT gen_random_uuid(),
    loan_id         UUID                        NOT NULL REFERENCES loans(id),
    installment_id  UUID                        REFERENCES installments(id),
    -- NULL = acción sobre el préstamo completo; con valor = cuota específica

    action_type     collection_action_type      NOT NULL,
    -- LLAMADA_TELEFONICA | WHATSAPP | EMAIL | SMS | NOTIFICACION_PUSH |
    -- PROMESA_DE_PAGO | ACUERDO_DE_PAGO_DIGITAL  (cobranza 100% remota)

    note            TEXT,                        -- Resumen del contacto

    status          collection_action_status    NOT NULL DEFAULT 'ACTIVA',
    -- ACTIVA → CUMPLIDA | PROMESA_INCUMPLIDA | CERRADA

    next_action_at  TIMESTAMPTZ,                 -- Próxima gestión o verificación de promesa

    -- ── Promesa de pago (RF-21.2) ────────────────────────────────────────────
    promise_amount  NUMERIC(18,4),
    promise_date    DATE,

    created_by      UUID                        NOT NULL REFERENCES users(id),
    created_at      TIMESTAMPTZ                 NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ                 NOT NULL DEFAULT NOW()
);

COMMENT ON TABLE collection_actions IS
    'Gestiones de cobranza digital: llamadas, mensajes y promesas de pago. El job diario marca PROMESA_INCUMPLIDA si vence sin pago (US-21, RF-21.3).';
COMMENT ON COLUMN collection_actions.promise_date IS
    'Si llega esta fecha y no se detecta pago, el job cambia el status a PROMESA_INCUMPLIDA (RF-21.3).';

CREATE TRIGGER tg_collection_actions_updated_at
    BEFORE UPDATE ON collection_actions
    FOR EACH ROW EXECUTE FUNCTION fn_set_updated_at();

-- ─────────────────────────────────────────────────────────────────────────────
-- Notificaciones omnicanal al cliente (US-19, US-21, RNF-15)
-- Encoladas de forma asíncrona (Redis / RabbitMQ)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE notifications (
    id            UUID                    PRIMARY KEY DEFAULT gen_random_uuid(),
    customer_id   UUID                    NOT NULL REFERENCES customers(id),
    channel       notification_channel    NOT NULL,       -- EMAIL | SMS | WHATSAPP | PUSH
    template_code VARCHAR(80)             NOT NULL,       -- Código de la plantilla de mensaje
    payload       JSONB,                                  -- Variables dinámicas de la plantilla
    status        notification_status     NOT NULL DEFAULT 'PENDING',
    sent_at       TIMESTAMPTZ,
    error_detail  TEXT,
    created_at    TIMESTAMPTZ             NOT NULL DEFAULT NOW()
);

COMMENT ON TABLE notifications IS
    'Cola de notificaciones omnicanal al cliente. Procesadas de forma asíncrona (US-19, US-21, RNF-15).';

-- >>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>
-- >>> database/migrations/10_integrations_reconciliation.sql
-- >>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>
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

-- >>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>
-- >>> database/indexes/11_indexes.sql
-- >>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>
-- =============================================================================
-- ÍNDICES DE RENDIMIENTO — Sistema de Préstamos
-- Ejecutar DESPUÉS de todas las migraciones (00 → 10)
-- Separados para facilitar revisión y ajuste independiente.
-- =============================================================================

-- ─────────────────────────────────────────────────────────────────────────────
-- CLIENTES (03_customers_kyc.sql)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE INDEX idx_customers_doc
    ON customers (doc_type, doc_number);
-- Búsqueda rápida por tipo + número de documento (unicidad ya garantizada por UK)

CREATE INDEX idx_customers_kyc_status
    ON customers (kyc_status)
    WHERE kyc_status != 'VERIFIED';
-- Partial index: identifica clientes pendientes de verificar KYC

CREATE INDEX idx_customers_portal_user
    ON customers (portal_user_id)
    WHERE portal_user_id IS NOT NULL;

-- ─────────────────────────────────────────────────────────────────────────────
-- CUENTAS BANCARIAS DEL CLIENTE (03_customers_kyc.sql)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE UNIQUE INDEX idx_customer_bank_primary
    ON customer_bank_accounts (customer_id)
    WHERE is_primary = TRUE AND status = 'ACTIVE';
-- UNIQUE parcial: solo una cuenta primaria activa por cliente (chk_one_primary_per_customer)

CREATE INDEX idx_customer_bank_customer
    ON customer_bank_accounts (customer_id);

-- ─────────────────────────────────────────────────────────────────────────────
-- USUARIOS Y TOKENS (02_security_users.sql)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE INDEX idx_users_email
    ON users (email);

CREATE INDEX idx_users_role_status
    ON users (role, status);

CREATE INDEX idx_refresh_tokens_user
    ON refresh_tokens (user_id)
    WHERE revoked_at IS NULL;
-- Solo tokens aún vigentes

-- ─────────────────────────────────────────────────────────────────────────────
-- SOLICITUDES (05_loan_applications.sql)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE INDEX idx_applications_customer
    ON loan_applications (customer_id);

CREATE INDEX idx_applications_product
    ON loan_applications (product_id);

CREATE INDEX idx_applications_status
    ON loan_applications (status);

CREATE INDEX idx_applications_pending
    ON loan_applications (created_at)
    WHERE status IN ('SOLICITADO', 'EN_EVALUACION');
-- Partial index: bandeja de solicitudes pendientes de dictamen

-- ─────────────────────────────────────────────────────────────────────────────
-- PRÉSTAMOS (06_loans_schedules.sql)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE INDEX idx_loans_customer
    ON loans (customer_id);

CREATE INDEX idx_loans_product
    ON loans (product_id);

CREATE INDEX idx_loans_status
    ON loans (status);

CREATE INDEX idx_loans_active
    ON loans (customer_id, status)
    WHERE status IN ('VIGENTE', 'EN_MORA');
-- Ficha 360 del cliente: préstamos activos (US-06)

CREATE INDEX idx_loans_disbursement_date
    ON loans (disbursement_date);

CREATE INDEX idx_loans_idempotency
    ON loans (idempotency_key)
    WHERE idempotency_key IS NOT NULL;

-- ─────────────────────────────────────────────────────────────────────────────
-- DESEMBOLSOS DIGITALES (06_loans_schedules.sql)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE INDEX idx_disbursements_idempotency
    ON disbursements (idempotency_key)
    WHERE idempotency_key IS NOT NULL;

CREATE INDEX idx_disbursements_pending
    ON disbursements (status, initiated_at)
    WHERE status IN ('PENDING', 'PROCESSING');
-- Transferencias de desembolso aún sin confirmación bancaria

CREATE INDEX idx_disbursements_operation
    ON disbursements (operation_number)
    WHERE operation_number IS NOT NULL;
-- Match del webhook / extracto con el desembolso (US-37)

-- ─────────────────────────────────────────────────────────────────────────────
-- CUOTAS — CRÍTICO para el job diario de mora (06_loans_schedules.sql)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE INDEX idx_installments_loan
    ON installments (loan_id);

CREATE INDEX idx_installments_due_date
    ON installments (due_date);

CREATE INDEX idx_installments_status
    ON installments (status);

-- Partial index más importante del sistema:
-- El job nocturno de mora (US-19) solo necesita cuotas no pagadas/canceladas
CREATE INDEX idx_installments_mora_job
    ON installments (due_date, status)
    WHERE status NOT IN ('PAID', 'CANCELLED_BY_PREPAYMENT');

CREATE INDEX idx_installments_schedule
    ON installments (schedule_id);

-- ─────────────────────────────────────────────────────────────────────────────
-- PAGOS (07_payments_ledger.sql)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE INDEX idx_payments_loan
    ON payments (loan_id);

CREATE INDEX idx_payments_paid_at
    ON payments (paid_at DESC);

CREATE INDEX idx_payments_status
    ON payments (status);

CREATE INDEX idx_payments_idempotency
    ON payments (idempotency_key)
    WHERE idempotency_key IS NOT NULL;

CREATE INDEX idx_payments_active
    ON payments (loan_id, paid_at)
    WHERE status = 'APPLIED' AND payment_kind = 'NORMAL';
-- Solo pagos vigentes (no revertidos)

CREATE INDEX idx_payments_gateway_ref
    ON payments (gateway_reference)
    WHERE gateway_reference IS NOT NULL;
-- Match de webhook / extracto con el pago (US-37)

CREATE INDEX idx_payments_pending_confirmation
    ON payments (created_at)
    WHERE status = 'PENDING_CONFIRMATION';
-- Pagos digitales a la espera de confirmación bancaria o de pasarela

-- ─────────────────────────────────────────────────────────────────────────────
-- IMPUTACIONES (07_payments_ledger.sql)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE INDEX idx_alloc_payment
    ON payment_allocations (payment_id);

CREATE INDEX idx_alloc_installment
    ON payment_allocations (installment_id);

-- ─────────────────────────────────────────────────────────────────────────────
-- LIBRO MAYOR (07_payments_ledger.sql)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE INDEX idx_transactions_loan
    ON transactions (loan_id);

CREATE INDEX idx_transactions_created_at
    ON transactions (created_at DESC);

CREATE INDEX idx_transactions_type
    ON transactions (transaction_type);

CREATE INDEX idx_transactions_bank_account
    ON transactions (company_bank_account_id, created_at DESC)
    WHERE company_bank_account_id IS NOT NULL;
-- Movimientos por cuenta bancaria de la Fintech (reemplaza idx_transactions_cashbox)

-- ─────────────────────────────────────────────────────────────────────────────
-- TESORERÍA DIGITAL Y CONCILIACIÓN (08_treasury_reconciliation.sql)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE INDEX idx_bank_statements_pending
    ON bank_statements (company_bank_account_id, reconciliation_status)
    WHERE reconciliation_status = 'PENDIENTE_CONCILIACION';
-- Bandeja de movimientos del extracto aún sin emparejar (US-37)

CREATE INDEX idx_bank_statements_payment
    ON bank_statements (reconciled_payment_id)
    WHERE reconciled_payment_id IS NOT NULL;

CREATE INDEX idx_bank_statements_disbursement
    ON bank_statements (reconciled_disbursement_id)
    WHERE reconciled_disbursement_id IS NOT NULL;

-- ─────────────────────────────────────────────────────────────────────────────
-- COBRANZA (09_collections_notifications.sql)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE INDEX idx_collection_loan
    ON collection_actions (loan_id);

CREATE INDEX idx_collection_next_action
    ON collection_actions (next_action_at)
    WHERE status = 'ACTIVA';
-- Job de recordatorios: solo acciones activas con próxima fecha programada

CREATE INDEX idx_collection_promise_vencida
    ON collection_actions (promise_date)
    WHERE action_type = 'PROMESA_DE_PAGO' AND status = 'ACTIVA';
-- Job que marca promesas incumplidas (RF-21.3)

-- ─────────────────────────────────────────────────────────────────────────────
-- NOTIFICACIONES (09_collections_notifications.sql)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE INDEX idx_notifications_customer
    ON notifications (customer_id);

CREATE INDEX idx_notifications_pending
    ON notifications (created_at)
    WHERE status = 'PENDING';
-- Cola de notificaciones sin procesar

-- ─────────────────────────────────────────────────────────────────────────────
-- INTEGRACIONES (10_integrations_reconciliation.sql)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE INDEX idx_integration_provider_external
    ON integration_events (provider, external_id);
-- Deduplicación de webhooks (RF-36.4)

CREATE INDEX idx_integration_status
    ON integration_events (status);

CREATE INDEX idx_integration_retry
    ON integration_events (next_retry_at)
    WHERE status = 'FAILED';
-- Job de reintentos exponenciales (US-38)

-- ─────────────────────────────────────────────────────────────────────────────
-- AUDITORÍA (02_security_users.sql)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE INDEX idx_audit_entity
    ON audit_logs (entity_name, entity_id);

CREATE INDEX idx_audit_user
    ON audit_logs (user_id);

CREATE INDEX idx_audit_created_at
    ON audit_logs (created_at DESC);

-- ─────────────────────────────────────────────────────────────────────────────
-- FERIADOS (01_admin_config.sql)
-- ─────────────────────────────────────────────────────────────────────────────
-- holidays.holiday_date ya tiene índice por su restricción UNIQUE (sin scope ni
-- sucursal en el modelo digital); no requiere índice adicional.

-- >>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>
-- >>> database/seeds/12_seed_data.sql
-- >>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>
-- =============================================================================
-- SEEDS — DATOS INICIALES DEL SISTEMA
-- Ejecutar DESPUÉS de todas las migraciones e índices.
-- Contiene catálogos base y el usuario administrador inicial.
-- =============================================================================

-- ─────────────────────────────────────────────────────────────────────────────
-- Monedas (ISO 4217)
-- ─────────────────────────────────────────────────────────────────────────────
INSERT INTO currencies (code, name, symbol) VALUES
    ('PEN', 'Sol Peruano',       'S/'),
    ('USD', 'Dólar Americano',   '$'),
    ('EUR', 'Euro',              '€')
ON CONFLICT (code) DO NOTHING;

-- ─────────────────────────────────────────────────────────────────────────────
-- Empresa placeholder (actualizar vía US-39 en el panel de administración)
-- ─────────────────────────────────────────────────────────────────────────────
INSERT INTO company (legal_name, trade_name, tax_id, legal_address, default_currency_id)
VALUES (
    'Mi Empresa Financiera S.A.C.',
    'FinCredit',
    '00000000000',
    'Lima, Perú',
    (SELECT id FROM currencies WHERE code = 'PEN')
)
ON CONFLICT (tax_id) DO NOTHING;

-- ─────────────────────────────────────────────────────────────────────────────
-- Tipos de documento de identidad
-- ─────────────────────────────────────────────────────────────────────────────
INSERT INTO document_types (code, name, char_length, validation_regex) VALUES
    ('DNI',       'Documento Nacional de Identidad',    8,   '^\d{8}$'),
    ('CE',        'Carné de Extranjería',               9,   '^\d{9}$'),
    ('RUC',       'Registro Único de Contribuyentes',   11,  '^\d{11}$'),
    ('PASAPORTE', 'Pasaporte',                          NULL, '^[A-Z0-9]{6,12}$')
ON CONFLICT (code) DO NOTHING;

-- ─────────────────────────────────────────────────────────────────────────────
-- Métodos de pago digitales (sin efectivo ni ventanilla)
--   webhook_enabled = TRUE  → confirmación automática por pasarela / banco
--   webhook_enabled = FALSE → confirmación por conciliación con el extracto
--   Tarjeta de crédito queda inactiva hasta que negocio apruebe su uso
--   (costo de comisión de la pasarela y pago de deuda con deuda).
-- ─────────────────────────────────────────────────────────────────────────────
INSERT INTO payment_methods (name, method_type, webhook_enabled, is_active) VALUES
    ('Transferencia bancaria (CCI)', 'TRANSFERENCIA_BANCARIA', FALSE, TRUE),
    ('Yape',                         'YAPE',                   TRUE,  TRUE),
    ('Plin',                         'PLIN',                   TRUE,  TRUE),
    ('Tarjeta de débito',            'TARJETA_DEBITO',         TRUE,  TRUE),
    ('Tarjeta de crédito',           'TARJETA_CREDITO',        TRUE,  FALSE),
    ('Pasarela de pago',             'PASARELA_PAGO',          TRUE,  TRUE)
ON CONFLICT DO NOTHING;

-- ─────────────────────────────────────────────────────────────────────────────
-- Usuario Administrador inicial
-- ⚠️ IMPORTANTE: Reemplazar el password_hash con un hash real
--    generado con BCrypt (cost=12) o Argon2id ANTES de usar en producción.
--    Nunca almacenar contraseñas en texto plano.
-- ─────────────────────────────────────────────────────────────────────────────
INSERT INTO users (email, password_hash, full_name, role, status)
VALUES (
    'admin@sistema.local',
    '$2b$12$PLACEHOLDER_REEMPLAZAR_CON_HASH_REAL',
    'Administrador del Sistema',
    'ROLE_ADMIN',
    'ACTIVE'
)
ON CONFLICT (email) DO NOTHING;

-- ─────────────────────────────────────────────────────────────────────────────
-- Plantilla legal de contrato (versión 1 — placeholder)
-- ─────────────────────────────────────────────────────────────────────────────
INSERT INTO legal_templates (code, version, content, is_current, created_by) VALUES
(
    'CONTRATO_PRESTAMO',
    1,
    '# CONTRATO DE PRÉSTAMO

**Prestamista:** {{empresa_razon_social}} — RUC: {{empresa_ruc}}
**Prestatario:** {{cliente_nombre}} — {{cliente_tipo_doc}}: {{cliente_documento}}
**Monto del Crédito:** {{moneda}} {{monto_prestamo}}
**Tasa Efectiva Anual (TEA):** {{tasa_tea}}%
**Plazo:** {{plazo_cuotas}} cuotas {{frecuencia_pago}}
**Primera cuota:** {{primera_fecha_vencimiento}}

## Tabla de Amortización
{{cronograma_tabla}}

## Condiciones de Mora
En caso de incumplimiento, se aplicará una tasa de mora de {{tasa_mora_diaria}}% diario
sobre {{base_mora}}, con {{dias_gracia}} día(s) de gracia.

## Cláusula de Cancelación Anticipada
Se aplicará una penalidad de {{penalidad_prepago}}% sobre el saldo de capital insoluto.',
    TRUE,
    (SELECT id FROM users WHERE email = 'admin@sistema.local')
)
ON CONFLICT (code, version) DO NOTHING;

-- ─────────────────────────────────────────────────────────────────────────────
-- Feriados nacionales de Perú (año corriente — ajustar anualmente)
-- ─────────────────────────────────────────────────────────────────────────────
INSERT INTO holidays (holiday_date, name, is_recurring) VALUES
    ('2026-01-01', 'Año Nuevo',                             TRUE),
    ('2026-04-02', 'Jueves Santo',                          FALSE),
    ('2026-04-03', 'Viernes Santo',                         FALSE),
    ('2026-05-01', 'Día del Trabajo',                       TRUE),
    ('2026-06-29', 'San Pedro y San Pablo',                 TRUE),
    ('2026-07-28', 'Fiestas Patrias — Independencia',       TRUE),
    ('2026-07-29', 'Fiestas Patrias — Día de las FFAA',    TRUE),
    ('2026-08-30', 'Santa Rosa de Lima',                    TRUE),
    ('2026-10-08', 'Combate de Angamos',                    TRUE),
    ('2026-11-01', 'Día de Todos los Santos',               TRUE),
    ('2026-12-08', 'Inmaculada Concepción',                 TRUE),
    ('2026-12-25', 'Navidad',                               TRUE)
ON CONFLICT (holiday_date) DO NOTHING;

