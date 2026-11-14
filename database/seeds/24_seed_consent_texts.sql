-- =============================================================================
-- SEED 24 — TEXTOS VIGENTES DE CONSENTIMIENTO (V1) POR FINALIDAD
-- Modelo: Fintech 100% Digital
-- Dominio: BE-CUS-07 (Ley N° 29733 — Protección de Datos Personales)
-- Dependencias: 21_customer_consents.sql, 00_extensions_and_enums.sql (pgcrypto)
-- Idempotente: ON CONFLICT (purpose, version) DO NOTHING
-- =============================================================================

INSERT INTO consent_texts (purpose, version, body, sha256, is_current, published_at)
SELECT v.purpose::consent_purpose,
       v.version,
       v.body,
       encode(digest(convert_to(v.body, 'UTF8'), 'sha256'), 'hex'),
       TRUE,
       NOW()
  FROM (
      VALUES
          (
              'DATA_PROCESSING',
              'v1',
              'Autorizo a la entidad financiera a recopilar, almacenar, verificar y tratar mis datos personales y documentación KYC conforme a la Ley N° 29733 (Ley de Protección de Datos Personales) y su reglamento, con la finalidad exclusiva de evaluar solicitudes, originar y administrar productos crediticios.'
          ),
          (
              'CREDIT_BUREAU_QUERY',
              'v1',
              'Autorizo expresamente a consultar y reportar mi historial crediticio, comportamiento de pago y obligaciones financieras ante las centrales de riesgo privadas y la Superintendencia de Banca, Seguros y AFP (SBS) durante la evaluación y vigencia de mis créditos.'
          ),
          (
              'MARKETING',
              'v1',
              'Autorizo el envío de comunicaciones comerciales, promociones, ofertas de productos financieros y encuestas de satisfacción por canales digitales (correo electrónico, SMS o WhatsApp). Este consentimiento es opcional y puede revocarse en cualquier momento.'
          )
  ) AS v(purpose, version, body)
ON CONFLICT (purpose, version) DO NOTHING;
