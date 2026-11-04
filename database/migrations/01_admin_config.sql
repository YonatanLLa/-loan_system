-- =============================================================================
-- MIGRACIÓN 01 — ADMINISTRACIÓN Y CONFIGURACIÓN GLOBAL
-- Dominio: US-39 (Empresa), US-40 (Sucursales/Zonas),
--          US-41 (Feriados), US-42 (Catálogos y Plantillas)
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
-- Empresa / Institución financiera (US-39)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE company (
    id                  UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    legal_name          VARCHAR(200) NOT NULL,              -- Razón social
    trade_name          VARCHAR(200),                       -- Nombre comercial
    tax_id              VARCHAR(20)  NOT NULL UNIQUE,       -- RUC / NIT / RFC / CIF
    legal_address       TEXT         NOT NULL,
    support_phone       VARCHAR(20),
    support_email       VARCHAR(150),
    legal_rep_name      VARCHAR(200),                       -- Representante legal
    legal_rep_doc       VARCHAR(30),
    logo_url            TEXT,                               -- URI en Object Storage
    default_currency_id UUID         REFERENCES currencies(id),
    created_at          TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

COMMENT ON TABLE company IS 'Datos corporativos e institucionales de la entidad financiera (US-39).';

CREATE TRIGGER tg_company_updated_at
    BEFORE UPDATE ON company
    FOR EACH ROW EXECUTE FUNCTION fn_set_updated_at();

-- ─────────────────────────────────────────────────────────────────────────────
-- Sucursales / Agencias físicas (US-40)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE branches (
    id          UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    code        VARCHAR(20)  NOT NULL UNIQUE,
    name        VARCHAR(150) NOT NULL,
    address     TEXT,
    district    VARCHAR(100),
    is_active   BOOLEAN      NOT NULL DEFAULT TRUE,
    created_at  TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at  TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

COMMENT ON TABLE branches IS 'Sucursales y agencias físicas de la entidad (US-40).';

CREATE TRIGGER tg_branches_updated_at
    BEFORE UPDATE ON branches
    FOR EACH ROW EXECUTE FUNCTION fn_set_updated_at();

-- ─────────────────────────────────────────────────────────────────────────────
-- Zonas geográficas de cobranza (US-40)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE zones (
    id          UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    name        VARCHAR(100) NOT NULL,
    description TEXT,
    branch_id   UUID         REFERENCES branches(id),
    is_active   BOOLEAN      NOT NULL DEFAULT TRUE,
    created_at  TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

COMMENT ON TABLE zones IS 'Zonas geográficas para asignación de gestores de cobranza (US-40).';

-- ─────────────────────────────────────────────────────────────────────────────
-- Feriados y días no hábiles (US-41)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE holidays (
    id           UUID          PRIMARY KEY DEFAULT gen_random_uuid(),
    holiday_date DATE          NOT NULL,
    name         VARCHAR(150)  NOT NULL,
    scope        holiday_scope NOT NULL DEFAULT 'NACIONAL',
    branch_id    UUID          REFERENCES branches(id),   -- NULL = aplica a todas
    is_recurring BOOLEAN       NOT NULL DEFAULT FALSE,     -- Repite cada año
    created_at   TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    UNIQUE (holiday_date, scope, branch_id)
);

COMMENT ON TABLE holidays IS 'Calendario de feriados para ajuste de vencimientos de cuotas (US-41, RF-14.2).';

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
-- Métodos de pago habilitados — catálogo configurable (US-42)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE payment_methods (
    id            UUID                PRIMARY KEY DEFAULT gen_random_uuid(),
    name          VARCHAR(80)         NOT NULL,
    method_type   payment_method_type NOT NULL,
    provider_code VARCHAR(50),                    -- Código interno del proveedor/pasarela
    is_active     BOOLEAN             NOT NULL DEFAULT TRUE,
    created_at    TIMESTAMPTZ         NOT NULL DEFAULT NOW()
);

COMMENT ON TABLE payment_methods IS 'Métodos de pago habilitados en el sistema (US-42, RF-42.2).';

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
    'Plantillas versionadas de contratos y pagarés con variables dinámicas (US-42, RF-42.3, US-15).';
