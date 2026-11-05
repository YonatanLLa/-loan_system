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
