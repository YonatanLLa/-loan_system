# Requerimientos de Base de Datos — Sistema de Préstamos (Fintech 100% Digital)

Requerimientos del esquema PostgreSQL, contrastados con lo que **ya existe** en [`database/`](../database/docs/README.md) (`run_all.sql`, migraciones 00–10, índices y seeds). Se derivan de las historias de `docs/01` a `docs/13`, del [`README.md`](../README.md) y de [`ARQUITECTURA_GENERAL.md`](../ARQUITECTURA_GENERAL.md).

Documentos hermanos: [Frontend](REQUERIMIENTOS_FRONTEND.md) · [Backend](REQUERIMIENTOS_BACKEND.md)

**Convenciones**

| Campo | Valores |
|---|---|
| **ID** | `DB-NN` |
| **Origen** | Historia o requerimiento de `docs/` (US / RF / RNF) o sección del README |
| **Fase** | `MVP`, `F2` (operación estable) o `F3` (integraciones enterprise) |
| **Estado** | **Existe**: implementado en el esquema actual · **Parcial**: existe pero incompleto · **Falta**: no está en el esquema |

---

## 1. Reglas generales del esquema

| ID | Requerimiento | Origen | Estado |
|---|---|---|---|
| DB-01 | PostgreSQL 15 o superior (recomendado 17/18); extensiones `pgcrypto` y `unaccent` | README §9 | Existe |
| DB-02 | Montos `NUMERIC(18,4)` y tasas `NUMERIC(12,6)`; nunca `FLOAT` | README §8, RF-08.3 | Existe |
| DB-03 | Claves `UUID`; `BIGSERIAL` en `transactions` y `audit_logs` para orden cronológico | database/docs | Existe |
| DB-04 | Fechas con hora en `TIMESTAMPTZ` (UTC); fechas de negocio en `DATE` | README §8 | Existe |
| DB-05 | Instalación atómica: `run_all.sql` con `ON_ERROR_STOP` y `BEGIN/COMMIT` | — | Existe |
| DB-06 | Migraciones versionadas con un runner (DbUp o Flyway); el SQL es la fuente de verdad y `database.sql` se genera a partir de él | ARQUITECTURA_GENERAL §7 | Parcial (falta el runner) |
| DB-07 | Cada tabla tiene un módulo dueño (único que escribe en ella) | ARQUITECTURA_GENERAL §4 | Parcial (documentado; sin esquemas por módulo) |
| DB-08 | Sin cajas, sucursales, zonas ni efectivo en todo el modelo | Modelo 100% digital | Existe |

## 2. Inmutabilidad y auditoría

| ID | Requerimiento | Origen | Fase | Estado |
|---|---|---|---|---|
| DB-09 | `audit_logs` de solo inserción **impuesto por la BD**: la función `fn_audit_log_immutable()` existe, pero **no hay ningún trigger** que la asocie a `audit_logs` | RF-03.3, CA-03.2 | MVP | **Parcial (incumple RF-03.3)** |
| DB-10 | `audit_logs.user_agent` | RF-03.1 | MVP | Falta |
| DB-11 | Prohibir `UPDATE`/`DELETE` en `payments` y `payment_allocations` mediante trigger o permisos del rol de la aplicación | RF-18.1 | MVP | Falta |
| DB-12 | Prohibir `UPDATE`/`DELETE` en `transactions` (libro mayor) | US-13, US-16 | MVP | Falta |
| DB-13 | Columnas proyectadas de `installments` (`due_date`, `principal`, `interest`, `fee`, `total_due`) inmutables tras crearse; solo cambian `paid_*`, `late_fee`, `status` y `days_late` | RNF-11, CA-14.2 | MVP | Falta |
| DB-14 | Un pago solo puede revertirse una vez: `UNIQUE (reversal_of_id)` parcial donde no sea nulo | CA-18.2 | MVP | Falta |
| DB-15 | Una sola versión activa de cronograma por préstamo | RF-31.3 | MVP | Existe (trigger `fn_enforce_single_active_schedule`) |
| DB-16 | Una sola plantilla legal vigente por código: `UNIQUE (code)` parcial donde `is_current` | RF-42.3 | MVP | Falta |

## 3. Seguridad y clientes

| ID | Requerimiento | Origen | Fase | Estado |
|---|---|---|---|---|
| DB-17 | `users`: email único, hash, rol, estado, intentos fallidos, bloqueo e invitación | US-01, US-43 | MVP | Existe |
| DB-18 | `users`: secreto TOTP cifrado y bandera de MFA activo | ARQUITECTURA_GENERAL §9 | MVP | Falta |
| DB-19 | `refresh_tokens` revocables | RF-01.2 | MVP | Existe |
| DB-20 | `customers`: unicidad del documento, KYC, saldo a favor ≥ 0 y vínculo con el usuario del portal | US-04, US-05, US-17 | MVP | Existe |
| DB-21 | `customers`: nivel de riesgo AML y bandera PEP | README §3.11, US-05 | MVP | Falta |
| DB-22 | Tabla de **consentimientos** por finalidad y versión del texto (fecha, IP, revocación); hoy solo existe `consent_given_at` | RF-05.4 | MVP | Parcial |
| DB-23 | Tabla de **verificaciones KYC** (tipo, proveedor, resultado, score, respuesta cruda, fecha) | US-05, RF-26.4 | MVP | Falta |
| DB-24 | `customer_documents` con URI y SHA-256 | RNF-04 | MVP | Existe |
| DB-25 | `customer_bank_accounts` con CCI o billetera, verificación y **una sola primaria activa** (índice único parcial) | Modelo digital | MVP | Existe |
| DB-26 | `customer_bank_accounts`: token de la pasarela para tarjetas (nunca el número) y fecha desde la que la cuenta puede recibir desembolsos (periodo de espera) | ARQUITECTURA_GENERAL §9 | MVP | Falta |
| DB-27 | `otp_verifications`: propósito (login, contrato, cambio de cuenta), intentos y referencia a la entidad que se firma | RF-26.2 | MVP | Parcial |

## 4. Productos y originación

| ID | Requerimiento | Origen | Fase | Estado |
|---|---|---|---|---|
| DB-28 | `loan_products` con montos, plazos, frecuencias, método, TEA, comisiones, gracia, mora, anatocismo, penalidad, elegibilidad y ajuste por feriados | US-07 a US-09 | MVP | Existe |
| DB-29 | `loan_products.payment_day` (día de pago) | README §3.1 | MVP | Falta |
| DB-30 | `loan_products.min_work_seniority_months` | README §3.1 | MVP | Falta |
| DB-31 | `loan_products.max_dti_auto` y `max_dti_manual` (hoy los umbrales 35/50 están fijos en la lógica) | RF-12.2, README §3.10 | MVP | Falta |
| DB-32 | Separar el cargo periódico en comisión de administración y seguro (fijo o % del saldo) | README §3.1 | F2 | Parcial (solo `periodic_fee`) |
| DB-33 | `loan_applications` con DTI, score, recomendación, dictamen, notas, monto aprobado y canal de origen | US-10, US-12 | MVP | Existe |
| DB-34 | Agregar `DESEMBOLSADO` a `application_status` (lo usan RF-13.4 y la línea de tiempo del portal, RF-27.3) | RF-13.4, RF-27.3 | MVP | Falta |
| DB-35 | `loan_applications.decision` como ENUM (hoy `VARCHAR(30)`) y una restricción que exija notas cuando la decisión es `RECHAZADO` | RF-12.3, CA-12.2 | MVP | Parcial |
| DB-36 | Insumos del scoring congelados (`JSONB` con cuota, otras cuotas, ingreso y versión de reglas) | RNF-09 | MVP | Falta |
| DB-37 | TCEA guardada en la solicitud y en el préstamo | Transparencia (validar) | MVP | Falta |

## 5. Préstamos, desembolsos y cronogramas

| ID | Requerimiento | Origen | Fase | Estado |
|---|---|---|---|---|
| DB-38 | `loans` con parámetros congelados, totales, estado, motivo de cierre, refinanciamiento e idempotencia | US-13, RNF-06 | MVP | Existe |
| DB-39 | Estado previo a la confirmación del desembolso en `loan_status` (`PENDIENTE_DESEMBOLSO`); hoy el préstamo nace `VIGENTE` | RF-13.4, RF-13.4.1 | MVP | Falta |
| DB-40 | `disbursements` con cuentas, canal, estados, referencias bancarias e idempotencia | US-25 | MVP | Existe |
| DB-41 | Reemplazar `disbursements.loan_id UNIQUE` por un índice único parcial sobre `PENDING`, `PROCESSING` y `COMPLETED` para permitir reintentos tras un `FAILED` | RF-25.3 | MVP | Falta |
| DB-42 | Garantizar con FK compuesta `(customer_id, id)` que la cuenta destino del desembolso pertenece al cliente del préstamo | RF-25.5 | MVP | Falta |
| DB-43 | `schedules` versionados e `installments` con proyectados y acumuladores | US-14, US-31 | MVP | Existe |
| DB-44 | `prepayment_quotes` con vigencia | US-30 | MVP | Existe |
| DB-45 | **Evidencia de firma electrónica** del contrato: plantilla y versión, hash del PDF, URI, OTP, IP, user-agent y fecha | RF-27.4, US-15 | MVP | Falta |
| DB-46 | Asignación de gestor de cobranza al préstamo (o una tabla de asignaciones con historial) | RF-20.2 | F2 | Falta |

## 6. Pagos, libro mayor y tesorería

| ID | Requerimiento | Origen | Fase | Estado |
|---|---|---|---|---|
| DB-47 | `payments` con método, cuentas de origen y destino, referencias, recibo correlativo, idempotencia y reverso | US-16, US-18 | MVP | Existe |
| DB-48 | `payments.status` con `DEFAULT 'PENDING_CONFIRMATION'` (hoy `'APPLIED'`) | RF-16.2.1 | MVP | Falta |
| DB-49 | `UNIQUE (payment_method_id, gateway_reference)` en `payments` contra webhooks duplicados | RF-36.4 | MVP | Falta |
| DB-50 | Tabla de **intenciones de pago** (préstamo, monto, vencimiento, id de orden de la pasarela, estado) | RF-29.1–29.2 | MVP | Falta |
| DB-51 | Tipos de transacción `REFUND`, `CHARGEBACK` y `GATEWAY_FEE` | RF-17.3 | F2 | Falta |
| DB-52 | Tabla de devoluciones del saldo a favor (cuenta destino, estado y vínculo con el desembolso de la devolución) | RF-17.3 | F2 | Falta |
| DB-53 | `payment_allocations` con importes no negativos | RF-16.5 | MVP | Existe |
| DB-54 | `transactions` con monto > 0, dirección, cuenta bancaria y vínculo con el pago o el desembolso | US-13, US-16 | MVP | Existe |
| DB-55 | `company_bank_accounts` con uso (desembolso o cobro) | US-23 | MVP | Existe |
| DB-56 | `bank_statements` sin duplicados por cuenta y número de operación, con estado de conciliación | US-37 | MVP | Existe |
| DB-57 | Liquidaciones en lote: tabla de lotes de la pasarela y relación N pagos ↔ 1 movimiento del extracto | Modelo digital | F2 | Falta (hoy la relación es 1:1) |
| DB-58 | Registro del cuadre diario cerrado por cuenta y fecha (inmutable) | US-24, RNF-17 | F2 | Falta |
| DB-59 | Comprobantes electrónicos SUNAT emitidos (serie, número, estado ante el OSE o PSE y enlace al pago) | Normativa tributaria (validar) | MVP | Falta |

## 7. Cobranza, notificaciones, integraciones y administración

| ID | Requerimiento | Origen | Fase | Estado |
|---|---|---|---|---|
| DB-60 | `collection_actions` con tipos digitales, promesa de pago y próxima acción | US-21 | MVP | Existe |
| DB-61 | `notifications` como cola con canal, plantilla, payload y estado | RNF-15 | MVP | Existe |
| DB-62 | Catálogo de **plantillas de notificación** versionadas (hoy `template_code` no apunta a ninguna tabla) | RNF-15 | MVP | Falta |
| DB-63 | Preferencias y consentimiento por canal del cliente | Ley 29733 | MVP | Falta |
| DB-64 | `integration_events` con deduplicación y reintentos | US-36, US-38 | MVP | Existe |
| DB-65 | Tabla `outbox_messages` (tipo, payload, estado, intentos, próxima ejecución) | RNF-10, ARQUITECTURA_GENERAL §7 | MVP | Falta |
| DB-66 | Tabla de **aprobaciones de 4 ojos** (operación, solicitante, aprobador distinto, estado y motivo) | RNF-26, RF-22.2, RNF-20 | F2 | Falta |
| DB-67 | Tabla de **parámetros globales** clave-valor tipados y auditados | README §6.1 (módulo 11) | F2 | Falta |
| DB-68 | **Libro de Reclamaciones**: correlativo, cliente, detalle, respuesta y plazos | Normativa de consumo (validar) | MVP | Falta |
| DB-69 | Catálogos `company`, `currencies`, `holidays`, `document_types`, `payment_methods` y `legal_templates` | US-39 a US-42 | MVP | Existe |
| DB-70 | `payment_methods.name` único (los seeds usan `ON CONFLICT DO NOTHING` sin clave que lo active) | US-42 | MVP | Falta |

## 8. Índices y rendimiento

| ID | Requerimiento | Origen | Estado |
|---|---|---|---|
| DB-71 | Índice parcial del job de mora sobre cuotas no pagadas ni canceladas | RNF-14 | Existe |
| DB-72 | Índices de bandejas: solicitudes pendientes, desembolsos en tránsito, pagos pendientes de confirmación, extractos sin conciliar, reintentos de integración y notificaciones pendientes | US-12, US-25, US-37, US-38 | Existe |
| DB-73 | Vistas materializadas o réplica para reportes de cartera y aging | RNF-22 | Falta |
| DB-74 | Índice para la ficha 360 (préstamos activos por cliente) | US-06 | Existe |

---

## 9. Brechas frente al negocio (base de datos)

**Bloquean un requisito ya especificado (corregir primero):**
1. **DB-09:** la bitácora no es inmutable en la BD; falta el trigger que asocie `fn_audit_log_immutable()` a `audit_logs` (RF-03.3).
2. **DB-34:** `application_status` no tiene `DESEMBOLSADO`, pero RF-13.4 y el portal lo usan.
3. **DB-39, DB-41 y DB-48:** el préstamo nace `VIGENTE` y el pago `APPLIED`, y no se puede reintentar un desembolso fallido. Contradicen el flujo digital ya especificado (RF-13.4, RF-16.2.1 y RF-25.3).
4. **DB-11 a DB-14:** pagos, libro mayor y cronogramas no tienen inmutabilidad impuesta por la BD; hoy dependen solo del código.

**Necesarios para operar 100% digital (MVP):**
- Intención de pago (DB-50).
- Evidencia de firma (DB-45).
- Verificaciones KYC (DB-23).
- Consentimientos (DB-22).
- Outbox (DB-65).
- Unicidad de `gateway_reference` (DB-49).
- FK compuesta de la cuenta destino (DB-42).
- Token y periodo de espera de la cuenta (DB-26).
- Propósito del OTP (DB-27).
- MFA (DB-18).

**Reglas de negocio del README sin columna:**
- Día de pago (DB-29).
- Antigüedad laboral (DB-30).
- DTI configurable (DB-31).
- Comisión de administración y seguro separados (DB-32).
- TCEA (DB-37).

**Cumplimiento (validar con asesoría):**
- Comprobantes SUNAT (DB-59).
- Libro de Reclamaciones (DB-68).
- Datos PEP y AML (DB-21).
