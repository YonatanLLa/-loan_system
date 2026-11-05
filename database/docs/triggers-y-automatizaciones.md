# Triggers, Automatizaciones e Índices de Rendimiento

> **Modelo:** Fintech 100% Digital — Sin cajas físicas, sin arqueo ciego, sin módulo de tesorería de efectivo.

Este documento describe la lógica procedimental implementada en PostgreSQL (PL/pgSQL), los disparadores (*triggers*) de integridad y auditoría, y la arquitectura de indexación de alto rendimiento.

---

## 1. Funciones y Triggers PL/pgSQL

Todas las funciones residen en `database/functions/fn_updated_at.sql` y se cargan al inicio del despliegue:

### 1.1 Marca Temporal Automática (`fn_set_updated_at`)
- **Propósito:** Actualiza automáticamente el campo `updated_at` al valor `NOW()` en cada sentencia `UPDATE`, garantizando trazabilidad sin depender de código en los controladores de la aplicación.
- **Implementación:**
  ```sql
  CREATE OR REPLACE FUNCTION fn_set_updated_at()
  RETURNS TRIGGER LANGUAGE plpgsql AS $$
  BEGIN
      NEW.updated_at = NOW();
      RETURN NEW;
  END;
  $$;
  ```
- **Tablas con este trigger (`BEFORE UPDATE`):**
  1. `company`
  2. `company_bank_accounts`
  3. `users`
  4. `customers`
  5. `customer_bank_accounts`
  6. `loan_products`
  7. `loan_applications`
  8. `loans`
  9. `collection_actions`

> [!NOTE] Se eliminó `branches` y `cash_boxes` de esta lista. No existen en el modelo digital.

---

### 1.2 Inmutabilidad Absoluta de Auditoría (`fn_audit_log_immutable`)
- **Propósito:** Cumplir el requerimiento **RF-03.3** que exige que la bitácora `audit_logs` sea estrictamente de solo inserción (*Append-Only*). Bloquea cualquier intento de alteración o supresión de registros de auditoría a nivel de motor de base de datos.
- **Implementación:**
  ```sql
  CREATE OR REPLACE FUNCTION fn_audit_log_immutable()
  RETURNS TRIGGER LANGUAGE plpgsql AS $$
  BEGIN
      RAISE EXCEPTION
          'audit_logs es append-only: las operaciones UPDATE y DELETE están prohibidas.';
  END;
  $$;
  ```
- **Triggers asociados:**
  - `tg_audit_log_no_update`: `BEFORE UPDATE ON audit_logs FOR EACH ROW`
  - `tg_audit_log_no_delete`: `BEFORE DELETE ON audit_logs FOR EACH ROW`

---

### 1.3 Generación de Recibo Digital Correlativo (`fn_generate_receipt_number`)
- **Propósito:** Atiende el requerimiento **RNF-13** generando de forma segura y correlativa un número de recibo oficial único para pagos normales digitales.
- **Formato:** `REC-YYYYMMDD-NNNNNN` (ej. `REC-20261104-000001`).
- **Implementación:**
  ```sql
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
  ```
- **Trigger asociado:** `tg_payments_receipt_number` (`BEFORE INSERT ON payments FOR EACH ROW`).

---

### 1.4 Control de Versión Activa de Cronograma (`fn_enforce_single_active_schedule`)
- **Propósito:** Asegura que en reestructuraciones (US-31) solo un cronograma por crédito posea la bandera `is_active = TRUE`. Al activarse una nueva versión (v2), las anteriores pasan automáticamente a `is_active = FALSE`.
- **Implementación:**
  ```sql
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
  ```
- **Trigger asociado:** `tg_schedules_single_active` (`BEFORE INSERT OR UPDATE ON schedules FOR EACH ROW`).

---

### 1.5 Actualización de Estado del Desembolso por Webhook (`fn_update_disbursement_status`)
- **Propósito:** Cuando el banco confirma o rechaza una transferencia a través de un webhook, el `integration_event` procesado dispara la actualización automática del `disbursement_status` en la tabla `disbursements`.
- **Reemplaza:** La función `fn_update_cashbox_balance_on_movement` del modelo físico de cajas de efectivo (eliminada de `functions/fn_updated_at.sql`).
- **Estado:** diseño documentado; **aún no implementada** en `database/functions/`.
- **Implementación:**
  ```sql
  CREATE OR REPLACE FUNCTION fn_update_disbursement_on_confirmation()
  RETURNS TRIGGER LANGUAGE plpgsql AS $$
  BEGIN
      -- Se invoca desde la capa de aplicación (.NET) al procesar el webhook,
      -- no directamente desde un trigger de BD, para mantener la lógica de
      -- mapeo de provider_reference → disbursement en la capa de Application.
      -- Este comentario documenta el flujo esperado:
      -- 1. Webhook entra → integration_events (status='RECEIVED')
      -- 2. Worker procesa → disbursements.status = 'COMPLETED' | 'FAILED'
      -- 3. Si COMPLETED → transactions INSERT (OUTFLOW, DISBURSEMENT)
      -- 4. audit_logs INSERT (entity='disbursements', action='CONFIRMED')
      RETURN NEW;
  END;
  $$;
  ```

---

## 2. Restricciones de Negocio a Nivel de Base de Datos (`CHECK` & `UNIQUE`)

Para garantizar robustez extrema contra errores de aplicación, se incorporaron las siguientes restricciones:

| Tabla | Restricción | Condición Validada | Regla de Negocio Protegida |
|---|---|---|---|
| `customers` | `chk_credit_balance_positive` | `credit_balance >= 0` | El saldo a favor nunca puede ser negativo. |
| `customer_bank_accounts` | `chk_one_primary_per_customer` | Índice `UNIQUE` parcial `idx_customer_bank_primary` (`WHERE is_primary AND status = 'ACTIVE'`) | Solo una cuenta primaria activa por cliente. |
| `loan_products` | `chk_product_amounts` | `max_amount >= min_amount` | Inconsistencia en rangos monetarios (CA-07.2). |
| `loan_products` | `chk_product_terms` | `max_term >= min_term` | Inconsistencia en límites de cuotas. |
| `loan_products` | `chk_product_positive_values` | `min_amount > 0 AND annual_rate > 0 AND min_term >= 1` | Valores financieros estrictamente positivos. |
| `loan_applications` | `chk_requested_amount_positive` | `requested_amount > 0 AND term >= 1` | Solicitudes consistentes. |
| `loans` | `chk_loan_principal_positive` | `principal > 0` | Un préstamo siempre debe tener capital positivo. |
| `disbursements` | `chk_disbursement_amount_positive` | `amount > 0` | La transferencia de desembolso siempre es positiva. |
| `installments` | `chk_installment_positive_values` | `principal >= 0 AND interest >= 0` | Los importes proyectados no admiten negativos. |
| `installments` | `chk_paid_positive_values` | `paid_principal >= 0 AND paid_interest >= 0 AND late_fee >= 0` | Los acumuladores de amortización siempre crecen o se mantienen en 0. |
| `payment_allocations` | `chk_alloc_values` | `to_mora >= 0 AND to_interest >= 0 AND to_fee >= 0 AND to_principal >= 0` | La prelación nunca imputa montos negativos. |
| `transactions` | `chk_transaction_amount_positive` | `amount > 0` | El importe contable siempre es positivo; el sentido lo define `direction`. |
| `bank_statements` | `uq_operation_per_account` | `UNIQUE (company_bank_account_id, operation_number)` | No se duplican movimientos del extracto bancario. |

> [!CAUTION] Las restricciones de `cash_movements.chk_movement_amount_positive` y `cash_transfers.chk_transfer_different_boxes` fueron **eliminadas** junto con sus tablas. No existen en el modelo digital.

---

## 3. Estrategia de Índices y *Partial Indexes*

Ubicados en `database/indexes/11_indexes.sql`. Diseñados para soportar consultas OLTP concurrentes de alta frecuencia y procesos por lotes masivos nocturnos:

### 3.1 El Índice del Job Diario de Mora (`idx_installments_mora_job`)
El proceso en segundo plano (US-19) que corre a las 00:05 AM evalúa millones de cuotas históricas. Se implementó un **Índice Parcial (*Partial Index*)** para no indexar cuotas ya pagadas:
```sql
CREATE INDEX idx_installments_mora_job
    ON installments (due_date, status)
    WHERE status NOT IN ('PAID', 'CANCELLED_BY_PREPAYMENT');
```
*Impacto:* El optimizador de PostgreSQL solo escanea cuotas vivas (`PENDING`, `PARTIAL`, `OVERDUE`), reduciendo el tamaño del índice en más del 85%.

---

### 3.2 Índices para Ficha 360 del Cliente y Multicrédito (`idx_loans_active`)
Optimiza la consulta de la ficha 360 del cliente (US-06):
```sql
CREATE INDEX idx_loans_active
    ON loans (customer_id, status)
    WHERE status IN ('VIGENTE', 'EN_MORA');
```

---

### 3.3 Índices para Idempotencia y Resiliencia en Red
Evitan carreras críticas (*race conditions*) en desembolsos y recaudaciones digitales:
```sql
CREATE INDEX idx_loans_idempotency
    ON loans (idempotency_key)
    WHERE idempotency_key IS NOT NULL;

CREATE INDEX idx_payments_idempotency
    ON payments (idempotency_key)
    WHERE idempotency_key IS NOT NULL;

CREATE INDEX idx_disbursements_idempotency
    ON disbursements (idempotency_key)
    WHERE idempotency_key IS NOT NULL;

CREATE INDEX idx_integration_provider_external
    ON integration_events (provider, external_id);
```

---

### 3.4 Índices para Conciliación Bancaria Automática
Búsquedas rápidas en extractos por referencia bancaria para el proceso de conciliación:
```sql
-- Conciliación por referencia externa del banco
CREATE INDEX idx_bank_statements_pending
    ON bank_statements (company_bank_account_id, reconciliation_status)
    WHERE reconciliation_status = 'PENDIENTE_CONCILIACION';

-- Búsqueda de pagos por gateway_reference (para match con extracto)
CREATE INDEX idx_payments_gateway_ref
    ON payments (gateway_reference)
    WHERE gateway_reference IS NOT NULL;

-- Desembolsos pendientes de confirmación bancaria
CREATE INDEX idx_disbursements_pending
    ON disbursements (status, initiated_at)
    WHERE status IN ('PENDING', 'PROCESSING');
```

---

### 3.5 Índices para Bandejas Operativas y Colas Asíncronas
- **Bandeja de Analistas:** `idx_applications_pending` sobre `loan_applications (created_at)` con filtro `WHERE status IN ('SOLICITADO', 'EN_EVALUACION')`.
- **Cola de Reintentos Bancarios (DLQ):** `idx_integration_retry` sobre `integration_events (next_retry_at)` con filtro `WHERE status = 'FAILED'`.
- **Cola de Mensajería Saliente:** `idx_notifications_pending` sobre `notifications (created_at)` con filtro `WHERE status = 'PENDING'`.
- **Alertas de Promesas de Pago Incumplidas:** `idx_collection_promise_vencida` sobre `collection_actions (promise_date)` con filtro `WHERE action_type = 'PROMESA_DE_PAGO' AND status = 'ACTIVA'`.
- **Cuentas bancarias primarias del cliente:** `idx_customer_bank_primary` (`UNIQUE` parcial) sobre `customer_bank_accounts (customer_id)` con filtro `WHERE is_primary = TRUE AND status = 'ACTIVE'`.
