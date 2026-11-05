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
