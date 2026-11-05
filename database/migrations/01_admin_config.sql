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
