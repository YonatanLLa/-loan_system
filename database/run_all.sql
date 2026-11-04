-- =============================================================================
-- ARCHIVO MAESTRO — Ejecuta todo el esquema en el orden correcto
-- Base de Datos: PostgreSQL 15+
--
-- USO:
--   psql -U postgres -d nombre_bd -f database/run_all.sql
--
-- O desde psql interactivo:
--   \i database/run_all.sql
-- =============================================================================

\echo '============================================================'
\echo '  Sistema de Préstamos — Instalación del esquema completo'
\echo '============================================================'

-- ── PASO 1: Extensiones y ENUMs ──────────────────────────────────────────────
\echo '[1/7] Extensiones y tipos enumerados...'
\ir migrations/00_extensions_and_enums.sql

-- ── PASO 2: Funciones y triggers reutilizables ───────────────────────────────
\echo '[2/7] Funciones y triggers...'
\ir functions/fn_updated_at.sql

-- ── PASO 3: Migraciones — tablas en orden de dependencia ────────────────────
\echo '[3/7] Migraciones de tablas...'
\ir migrations/01_admin_config.sql
\ir migrations/02_security_users.sql
\ir migrations/03_customers_kyc.sql
\ir migrations/04_loan_products.sql
\ir migrations/05_loan_applications.sql
\ir migrations/06_loans_schedules.sql
\ir migrations/07_payments_ledger.sql
\ir migrations/08_treasury_cashbox.sql
\ir migrations/09_collections_notifications.sql
\ir migrations/10_integrations_reconciliation.sql

-- ── PASO 4: Índices ──────────────────────────────────────────────────────────
\echo '[4/7] Índices de rendimiento...'
\ir indexes/11_indexes.sql

-- ── PASO 5: Datos semilla ────────────────────────────────────────────────────
\echo '[5/7] Datos iniciales (seeds)...'
\ir seeds/12_seed_data.sql

\echo '============================================================'
\echo '  ✓ Esquema instalado correctamente.'
\echo '  ⚠ Recuerda actualizar el password_hash del admin en users.'
\echo '============================================================'
