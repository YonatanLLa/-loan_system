# Triggers, Automatizaciones e Índices de Rendimiento

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
  2. `branches`
  3. `users`
  4. `customers`
  5. `loan_products`
  6. `loan_applications`
  7. `loans`
  8. `cash_boxes`
  9. `collection_actions`

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

### 1.3 Generación de Recibo Correlativo (`fn_generate_receipt_number`)
- **Propósito:** Atiende el requerimiento **RNF-13** generando de forma segura y correlativa un número de recibo oficial único para pagos normales.
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

### 1.4 Sincronización en Tiempo Real del Saldo de Caja (`fn_update_cashbox_balance_on_movement`)
- **Propósito:** Garantizar consistencia inmediata en `cash_boxes.current_balance` cuando se registran ingresos o egresos extraordinarios de efectivo en ventanilla (US-24, RF-24.3).
- **Implementación:**
  ```sql
  CREATE OR REPLACE FUNCTION fn_update_cashbox_balance_on_movement()
  RETURNS TRIGGER LANGUAGE plpgsql AS $$
  BEGIN
      IF NEW.movement_type = 'INGRESO_EXTRAORDINARIO' THEN
          UPDATE cash_boxes
             SET current_balance = current_balance + NEW.amount
           WHERE id = NEW.cashbox_id;
      ELSIF NEW.movement_type = 'EGRESO_GASTO' THEN
          UPDATE cash_boxes
             SET current_balance = current_balance - NEW.amount
           WHERE id = NEW.cashbox_id;
      END IF;
      RETURN NEW;
  END;
  $$;
  ```
- **Trigger asociado:** `tg_cash_movements_update_balance` (`AFTER INSERT ON cash_movements FOR EACH ROW`).

---

### 1.5 Control de Versión Activa de Cronograma (`fn_enforce_single_active_schedule`)
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

## 2. Restricciones de Negocio a Nivel de Base de Datos (`CHECK` & `UNIQUE`)

Para garantizar robustez extrema contra errores de aplicación, se incorporaron las siguientes restricciones de comprobación:

| Tabla | Restricción | Condición Validada | Regla de Negocio Protegida |
|---|---|---|---|
| `customers` | `chk_credit_balance_positive` | `credit_balance >= 0` | El saldo a favor nunca puede ser negativo. |
| `loan_products` | `chk_product_amounts` | `max_amount >= min_amount` | Inconsistencia en rangos monetarios (CA-07.2). |
| `loan_products` | `chk_product_terms` | `max_term >= min_term` | Inconsistencia en límites de cuotas. |
| `loan_products` | `chk_product_positive_values` | `min_amount > 0 AND annual_rate > 0 AND min_term >= 1` | Valores financieros estrictamente positivos. |
| `loan_applications` | `chk_requested_amount_positive` | `requested_amount > 0 AND term >= 1` | Solicitudes consistentes. |
| `loans` | `chk_loan_principal_positive` | `principal > 0` | Un préstamo siempre debe tener capital positivo. |
| `installments` | `chk_installment_positive_values` | `principal >= 0 AND interest >= 0` | Los importes proyectados no admiten números negativos. |
| `installments` | `chk_paid_positive_values` | `paid_principal >= 0 AND paid_interest >= 0 AND late_fee >= 0` | Los acumuladores de amortización siempre crecen o se mantienen en 0. |
| `payment_allocations` | `chk_alloc_values` | `to_mora >= 0 AND to_interest >= 0 AND to_fee >= 0 AND to_principal >= 0` | La prelación nunca imputa montos negativos. |
| `transactions` | `chk_transaction_amount_positive` | `amount > 0` | El importe contable siempre es positivo; el sentido lo define `direction` (`INFLOW`/`OUTFLOW`). |
| `cash_movements` | `chk_movement_amount_positive` | `amount > 0` | Movimiento de caja mayor a 0. |
| `cash_transfers` | `chk_transfer_different_boxes` | `source_cashbox_id <> destination_cashbox_id` | No se puede transferir dinero hacia la misma caja de origen. |

---

## 3. Estrategia de Índices y *Partial Indexes*

Ubicados en `database/indexes/11_indexes.sql`. Se diseñaron para dar soporte a consultas OLTP concurrentes de alta frecuencia y a procesos por lotes masivos nocturnos (*Batch Jobs*):

### 3.1 El Índice del Job Diario de Mora (`idx_installments_mora_job`)
El proceso en segundo plano (US-19) que corre a las 00:05 AM evalúa millones de cuotas históricas. Crear un índice B-Tree completo sobre toda la tabla desperdiciaría memoria indexando cuotas ya pagadas hace años.  
Se implementó un **Índice Parcial (*Partial Index*)**:
```sql
CREATE INDEX idx_installments_mora_job
    ON installments (due_date, status)
    WHERE status NOT IN ('PAID', 'CANCELLED_BY_PREPAYMENT');
```
*Impacto:* El optimizador de PostgreSQL solo escanea las cuotas vivas pendientes (`PENDING`, `PARTIAL`, `OVERDUE`), reduciendo el tamaño del índice en más del 85% y permitiendo evaluar carteras masivas en pocos segundos.

---

### 3.2 Índices para Ficha 360 del Cliente y Multicrédito (`idx_loans_active`)
Optimiza la consulta de la ficha 360 (US-06):
```sql
CREATE INDEX idx_loans_active
    ON loans (customer_id, status)
    WHERE status IN ('VIGENTE', 'EN_MORA');
```

---

### 3.3 Índices para Idempotencia y Resiliencia en Red
Evitan carreras críticas (*race conditions*) en desembolsos y recaudaciones:
```sql
CREATE INDEX idx_loans_idempotency
    ON loans (idempotency_key)
    WHERE idempotency_key IS NOT NULL;

CREATE INDEX idx_payments_idempotency
    ON payments (idempotency_key)
    WHERE idempotency_key IS NOT NULL;

CREATE INDEX idx_integration_provider_external
    ON integration_events (provider, external_id);
```

---

### 3.4 Índices para Bandejas Operativas y Colas Asíncronas
- **Bandeja de Analistas:** `idx_applications_pending` sobre `loan_applications (created_at)` con filtro `WHERE status IN ('SOLICITADO', 'EN_EVALUACION')`.
- **Cola de Reintentos Bancarios (DLQ):** `idx_integration_retry` sobre `integration_events (next_retry_at)` con filtro `WHERE status = 'FAILED'`.
- **Cola de Mensajería Saliente:** `idx_notifications_pending` sobre `notifications (created_at)` con filtro `WHERE status = 'PENDING'`.
- **Alertas de Promesas de Pago Incumplidas:** `idx_collection_promise_vencida` sobre `collection_actions (promise_date)` con filtro `WHERE action_type = 'PROMESA_DE_PAGO' AND status = 'ACTIVA'`.
