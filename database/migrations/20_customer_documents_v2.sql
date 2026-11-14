-- =============================================================================
-- MIGRACIÓN 20 — CUSTOMER DOCUMENTS V2 (CATEGORÍA ENUM Y VERSIONADO VIGENTE)
-- Modelo: Fintech 100% Digital
-- Dominio: US-05, BE-CUS-03, RF-05.1
-- Dependencias: 03_customers_kyc.sql
-- =============================================================================

DO $$
BEGIN
    CREATE TYPE customer_document_category AS ENUM (
        'DNI_FRONTAL',
        'DNI_REVERSO',
        'SELFIE',
        'BOLETA_PAGO',
        'RECIBO_SERVICIOS',
        'ESTADO_CUENTA_BANCO',
        'FICHA_RUC',
        'OTRO'
    );
EXCEPTION
    WHEN duplicate_object THEN NULL;
END $$;

ALTER TABLE customer_documents
    ALTER COLUMN doc_category TYPE customer_document_category
        USING doc_category::text::customer_document_category,
    ADD COLUMN IF NOT EXISTS size_bytes    BIGINT      NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS original_name VARCHAR(255),
    ADD COLUMN IF NOT EXISTS is_current    BOOLEAN     NOT NULL DEFAULT TRUE,   -- FALSE cuando se reemplaza
    ADD COLUMN IF NOT EXISTS replaced_at   TIMESTAMPTZ;

-- Solo un documento vigente por categoría (excepto OTRO y BOLETA_PAGO, que admiten varios)
CREATE UNIQUE INDEX IF NOT EXISTS ux_customer_documents_current
    ON customer_documents (customer_id, doc_category)
    WHERE is_current AND doc_category NOT IN ('OTRO', 'BOLETA_PAGO');

COMMENT ON COLUMN customer_documents.is_current IS
    'Indica si es la versión vigente en su categoría. Los documentos no se borran; reemplazar marca is_current = FALSE.';
