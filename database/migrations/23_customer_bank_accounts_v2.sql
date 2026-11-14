-- =============================================================================
-- MIGRACIÓN 23 — CUSTOMER BANK ACCOUNTS V2 (CIFRADO CCI, COOLING PERIOD, FK DB-42)
-- Modelo: Fintech 100% Digital
-- Dominio: US-06, BE-CUS-09, BE-CUS-10, DB-25, DB-26, DB-42
-- Dependencias: 00_extensions_and_enums.sql (pgcrypto), 03_customers_kyc.sql
-- =============================================================================

ALTER TABLE customer_bank_accounts
    ADD COLUMN IF NOT EXISTS bank_code                 VARCHAR(3),              -- 3 primeros dígitos del CCI
    ADD COLUMN IF NOT EXISTS account_number_encrypted  TEXT,                    -- CCI / número cifrado AES-GCM (D4)
    ADD COLUMN IF NOT EXISTS account_number_hash       VARCHAR(64),             -- HMAC-SHA256 para unicidad
    ADD COLUMN IF NOT EXISTS last4                     VARCHAR(4)  NOT NULL DEFAULT '',
    ADD COLUMN IF NOT EXISTS holder_name               VARCHAR(200),            -- nombre del titular declarado
    ADD COLUMN IF NOT EXISTS gateway_token             VARCHAR(200),            -- tarjetas: solo token de pasarela
    ADD COLUMN IF NOT EXISTS disbursement_enabled_from TIMESTAMPTZ,             -- fin del periodo de espera
    ADD COLUMN IF NOT EXISTS verification_method       VARCHAR(30),             -- 'MANUAL' | 'PENNY_TEST' (F2)
    ADD COLUMN IF NOT EXISTS verification_notes        VARCHAR(500),
    ADD COLUMN IF NOT EXISTS deactivated_at            TIMESTAMPTZ;

ALTER TABLE customer_bank_accounts
    ALTER COLUMN account_number_masked DROP NOT NULL;

-- Si existen registros previos de seeds/entornos anteriores, poblar las columnas v2 antes de los constraints
UPDATE customer_bank_accounts
   SET last4 = COALESCE(NULLIF(last4, ''), RIGHT(COALESCE(NULLIF(cci, ''), NULLIF(account_number_masked, ''), id::text), 4)),
       bank_code = CASE
           WHEN bank_code IS NULL AND cci ~ '^\d{20}$' THEN LEFT(cci, 3)
           ELSE bank_code
       END,
       account_number_hash = CASE
           WHEN account_number_hash IS NULL AND account_type <> 'BILLETERA_DIGITAL'
               THEN encode(digest(COALESCE(NULLIF(cci, ''), NULLIF(account_number_masked, ''), id::text), 'sha256'), 'hex')
           ELSE account_number_hash
       END
 WHERE last4 = ''
    OR (account_type <> 'BILLETERA_DIGITAL' AND account_number_hash IS NULL);

-- Una sola primaria activa por cliente
CREATE UNIQUE INDEX IF NOT EXISTS ux_customer_bank_accounts_primary
    ON customer_bank_accounts (customer_id)
    WHERE is_primary AND status = 'ACTIVE';

-- Una misma cuenta no puede registrarse dos veces para el mismo cliente
CREATE UNIQUE INDEX IF NOT EXISTS ux_customer_bank_accounts_number
    ON customer_bank_accounts (customer_id, account_number_hash)
    WHERE status <> 'INACTIVE';

-- DB-42: permite FK compuesta desde loans (customer_id, customer_bank_account_id)
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'uq_customer_bank_accounts_customer_id'
    ) THEN
        ALTER TABLE customer_bank_accounts
            ADD CONSTRAINT uq_customer_bank_accounts_customer_id UNIQUE (customer_id, id);
    END IF;
END $$;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'ck_wallet_or_number'
    ) THEN
        ALTER TABLE customer_bank_accounts
            ADD CONSTRAINT ck_wallet_or_number CHECK (
                (account_type = 'BILLETERA_DIGITAL' AND phone_wallet IS NOT NULL)
             OR (account_type <> 'BILLETERA_DIGITAL' AND account_number_hash IS NOT NULL)
            );
    END IF;
END $$;

COMMENT ON COLUMN customer_bank_accounts.cci IS
    'DEPRECADA (BE-CUS-09, D4): no almacenar CCI en claro; usar account_number_encrypted, account_number_hash y last4.';
COMMENT ON COLUMN customer_bank_accounts.account_number_masked IS
    'DEPRECADA (BE-CUS-09): conservar solo últimos 4 dígitos enmascarados o consultar last4.';
