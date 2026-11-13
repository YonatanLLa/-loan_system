# Diccionario de Datos del Sistema

> **Modelo:** Fintech 100% Digital — Sin sucursales físicas, sin cajas de efectivo, sin cobradores de campo.

Este documento proporciona la especificación exhaustiva de todos los tipos enumerados (`ENUM`), tablas, columnas, tipos de datos, valores por defecto y restricciones de integridad.

---

## 1. Catálogo de Tipos Enumerados (`ENUM`)

| Tipo ENUM | Valores Permitidos | Dominio / Contexto |
|---|---|---|
| `person_type` | `'NATURAL'`, `'JURIDICA'` | Clasificación legal del cliente |
| `document_type` | `'DNI'`, `'CE'`, `'RUC'`, `'PASAPORTE'` | Tipo de documento de identidad |
| `kyc_status_type` | `'PENDING'`, `'VERIFIED'`, `'REJECTED'`, `'EXPIRED'` | Estado de cumplimiento KYC digital |
| `user_role` | `'ROLE_ADMIN'`, `'ROLE_ANALYST'`, `'ROLE_OPERATOR'`, `'ROLE_COLLECTOR'`, `'ROLE_AUDITOR'`, `'ROLE_CLIENT'` | Roles de acceso RBAC (sin CASHIER físico) |
| `user_status_type` | `'ACTIVE'`, `'INACTIVE'`, `'LOCKED'` | Estado de la cuenta de usuario |
| `bank_account_type` | `'CUENTA_CORRIENTE'`, `'CUENTA_AHORRO'`, `'TARJETA_DEBITO'`, `'BILLETERA_DIGITAL'` | Tipo de cuenta bancaria del cliente |
| `bank_account_status` | `'ACTIVE'`, `'INACTIVE'`, `'PENDING_VERIFICATION'` | Estado de verificación de la cuenta |
| `disbursement_status` | `'PENDING'`, `'PROCESSING'`, `'COMPLETED'`, `'FAILED'`, `'REVERSED'` | Estado de la transferencia bancaria al cliente |
| `disbursement_channel` | `'TRANSFERENCIA_BANCARIA'`, `'YAPE'`, `'PLIN'`, `'DEPOSITO_CCI'` | Canal digital de desembolso |
| `payment_frequency` | `'DIARIA'`, `'SEMANAL'`, `'QUINCENAL'`, `'MENSUAL'` | Periodicidad de las cuotas |
| `amortization_method` | `'FRANCES'`, `'FLAT'`, `'ALEMAN'`, `'AMERICANO'` | Método matemático financiero |
| `rate_type` | `'NOMINAL'`, `'EFECTIVA'` | Régimen de la tasa de interés |
| `mora_base_type` | `'SALDO_CAPITAL_VENCIDO'`, `'CUOTA_TOTAL_VENCIDA'` | Base para la fórmula de mora |
| `commission_mode` | `'DEDUCTIVA'`, `'FINANCIADA'` | Modo de cobro de apertura |
| `product_status` | `'ACTIVO'`, `'INACTIVO'` | Estado del producto crediticio |
| `application_status` | `'SOLICITADO'`, `'EN_EVALUACION'`, `'APROBADO'`, `'APROBADO_CON_CONDICIONES'`, `'RECHAZADO'`, `'ANULADO'` | Ciclo de originación |
| `credit_decision` | `'APROBABLE'`, `'REVISION_MANUAL'`, `'RECHAZO_SUGERIDO'` | Recomendación del motor DTI |
| `loan_purpose_type` | `'CONSUMO'`, `'CAPITAL_TRABAJO'`, `'VEHICULO'`, `'VIVIENDA'`, `'REFINANCING'`, `'CONSOLIDATION'`, `'OTRO'` | Destino de los fondos |
| `loan_status` | `'VIGENTE'`, `'EN_MORA'`, `'PAGADO'`, `'CASTIGADO'`, `'CANCELADO'`, `'DESEMBOLSADO'` | Estado del préstamo formalizado |
| `loan_close_reason` | `'PAGO_NORMAL'`, `'LIQUIDACION_ANTICIPADA'`, `'REFINANCIAMIENTO'`, `'CASTIGO'` | Causa de finalización |
| `installment_status` | `'PENDING'`, `'PARTIAL'`, `'PAID'`, `'OVERDUE'`, `'CANCELLED_BY_PREPAYMENT'` | Estado de exigibilidad de cuota |
| `payment_status` | `'PENDING_CONFIRMATION'`, `'APPLIED'`, `'REVERSED'` | Estado del cobro digital |
| `payment_kind` | `'NORMAL'`, `'REVERSAL'` | Clasificación contable del pago |
| `payment_method_type` | `'TRANSFERENCIA_BANCARIA'`, `'YAPE'`, `'PLIN'`, `'TARJETA_DEBITO'`, `'TARJETA_CREDITO'`, `'PASARELA_PAGO'` | Canal digital de recaudación (sin efectivo) |
| `transaction_type` | `'DISBURSEMENT'`, `'PAYMENT'`, `'REVERSAL'`, `'CUSTOMER_CREDIT_BALANCE'`, `'WRITE_OFF'`, `'ADJUSTMENT'` | Movimientos del Libro Mayor (sin CASH_TRANSFER, CASH_EXPENSE, CASH_INCOME) |
| `transaction_dir` | `'INFLOW'`, `'OUTFLOW'` | Sentido del flujo monetario |
| `collection_action_type` | `'LLAMADA_TELEFONICA'`, `'WHATSAPP'`, `'EMAIL'`, `'SMS'`, `'NOTIFICACION_PUSH'`, `'PROMESA_DE_PAGO'`, `'ACUERDO_DE_PAGO_DIGITAL'` | Gestión de cobranza digital (sin visita domiciliaria) |
| `collection_action_status` | `'ACTIVA'`, `'CUMPLIDA'`, `'PROMESA_INCUMPLIDA'`, `'CERRADA'` | Estado del compromiso de cobro |
| `integration_event_status` | `'RECEIVED'`, `'PROCESSED'`, `'FAILED'`, `'DEAD_LETTER'`, `'FAILED_NEEDS_REVIEW'` | Cola de webhooks bancarios |
| `reconciliation_status` | `'CONCILIADO_AUTOMATICO'`, `'PENDIENTE_CONCILIACION'`, `'DESCUADRADO'`, `'RECONCILED'` | Estado de conciliación bancaria |
| `notification_channel` | `'EMAIL'`, `'SMS'`, `'WHATSAPP'`, `'PUSH'` | Canal de mensajería al cliente |
| `notification_status` | `'PENDING'`, `'SENT'`, `'FAILED'` | Estado de despacho de mensaje |

---

## 2. Diccionario de Tablas por Dominio

### Módulo 1: Administración y Catálogos Maestros

#### `company`
Datos institucionales, fiscales y corporativos de la Fintech (US-39).
| Columna | Tipo | Nulo | Restricción / Default | Descripción |
|---|---|:---:|---|---|
| `id` | `UUID` | No | `PRIMARY KEY DEFAULT gen_random_uuid()` | Identificador único |
| `legal_name` | `VARCHAR(200)` | No | | Razón social formal |
| `trade_name` | `VARCHAR(200)` | Sí | | Nombre comercial / marca |
| `tax_id` | `VARCHAR(20)` | No | `UNIQUE` | RUC / NIT / RFC / CIF |
| `legal_address` | `TEXT` | No | | Domicilio fiscal |
| `support_phone` | `VARCHAR(20)` | Sí | | Teléfono de soporte digital |
| `support_email` | `VARCHAR(150)` | Sí | | Correo oficial de atención |
| `website_url` | `VARCHAR(255)` | Sí | | URL del portal web de la Fintech |
| `legal_rep_name` | `VARCHAR(200)` | Sí | | Nombre del representante legal |
| `legal_rep_doc` | `VARCHAR(30)` | Sí | | Documento del representante |
| `logo_url` | `TEXT` | Sí | | URL del logo en Object Storage |
| `default_currency_id` | `UUID` | Sí | `REFERENCES currencies(id)` | Moneda principal del sistema |
| `created_at` | `TIMESTAMPTZ` | No | `DEFAULT NOW()` | Fecha de registro |
| `updated_at` | `TIMESTAMPTZ` | No | `DEFAULT NOW()` | Última actualización |

#### `currencies`
Catálogo de monedas admitidas (ISO 4217).
| Columna | Tipo | Nulo | Restricción / Default | Descripción |
|---|---|:---:|---|---|
| `id` | `UUID` | No | `PRIMARY KEY DEFAULT gen_random_uuid()` | Identificador único |
| `code` | `CHAR(3)` | No | `UNIQUE` | Código internacional (ej. PEN, USD) |
| `name` | `VARCHAR(60)` | No | | Denominación completa |
| `symbol` | `VARCHAR(5)` | No | | Símbolo gráfico (ej. S/, $) |
| `is_active` | `BOOLEAN` | No | `DEFAULT TRUE` | Estado de habilitación |
| `created_at` | `TIMESTAMPTZ` | No | `DEFAULT NOW()` | Fecha de creación |

#### `company_bank_accounts`
Cuentas bancarias de la Fintech. Reemplaza completamente a `branches` y `cash_boxes` del modelo físico.
| Columna | Tipo | Nulo | Restricción / Default | Descripción |
|---|---|:---:|---|---|
| `id` | `UUID` | No | `PRIMARY KEY DEFAULT gen_random_uuid()` | Identificador único |
| `bank_name` | `VARCHAR(100)` | No | | Banco (BCP, Interbank, BBVA, etc.) |
| `account_number_masked` | `VARCHAR(30)` | No | | Número enmascarado (últimos 4 dígitos visibles) |
| `cci` | `VARCHAR(30)` | Sí | | Código de Cuenta Interbancario (Perú) |
| `currency_id` | `UUID` | No | `REFERENCES currencies(id)` | Moneda de la cuenta |
| `is_disbursement` | `BOOLEAN` | No | `DEFAULT TRUE` | ¿Se usa para transferir dinero a clientes? |
| `is_collection` | `BOOLEAN` | No | `DEFAULT TRUE` | ¿Recibe cobros de clientes? |
| `is_active` | `BOOLEAN` | No | `DEFAULT TRUE` | Estado operativo |
| `created_at` | `TIMESTAMPTZ` | No | `DEFAULT NOW()` | Fecha de alta |
| `updated_at` | `TIMESTAMPTZ` | No | `DEFAULT NOW()` | Última actualización |

#### `holidays`
Calendario de feriados nacionales para corrimiento de vencimientos (US-41). Sin scope de sucursal.
| Columna | Tipo | Nulo | Restricción / Default | Descripción |
|---|---|:---:|---|---|
| `id` | `UUID` | No | `PRIMARY KEY DEFAULT gen_random_uuid()` | Identificador único |
| `holiday_date` | `DATE` | No | `UNIQUE` | Fecha del día festivo |
| `name` | `VARCHAR(150)` | No | | Motivo del feriado |
| `is_recurring` | `BOOLEAN` | No | `DEFAULT FALSE` | Si se repite cada año (ej. Navidad) |
| `created_at` | `TIMESTAMPTZ` | No | `DEFAULT NOW()` | Registro |

#### `document_types`
Catálogo configurable de documentos de identidad con regex de validación (US-42).
| Columna | Tipo | Nulo | Restricción / Default | Descripción |
|---|---|:---:|---|---|
| `id` | `UUID` | No | `PRIMARY KEY DEFAULT gen_random_uuid()` | Identificador único |
| `code` | `VARCHAR(10)` | No | `UNIQUE` | Nemotécnico (DNI, CE, RUC, PAS) |
| `name` | `VARCHAR(60)` | No | | Nombre descriptivo |
| `char_length` | `SMALLINT` | Sí | | Longitud exacta de caracteres |
| `validation_regex` | `VARCHAR(100)` | Sí | | Expresión regular de validación |
| `is_active` | `BOOLEAN` | No | `DEFAULT TRUE` | Disponibilidad |

#### `payment_methods`
Canales digitales de recepción de abonos (US-42). Sin efectivo ni ventanilla física.
| Columna | Tipo | Nulo | Restricción / Default | Descripción |
|---|---|:---:|---|---|
| `id` | `UUID` | No | `PRIMARY KEY DEFAULT gen_random_uuid()` | Identificador |
| `name` | `VARCHAR(80)` | No | | Nombre del método (ej. 'Yape', 'Transferencia BCP') |
| `method_type` | `payment_method_type` | No | | Clasificación operativa digital |
| `provider_code` | `VARCHAR(50)` | Sí | | Identificador técnico del proveedor/pasarela |
| `webhook_enabled` | `BOOLEAN` | No | `DEFAULT FALSE` | ¿Tiene confirmación automática por webhook? |
| `is_active` | `BOOLEAN` | No | `DEFAULT TRUE` | Estado |
| `created_at` | `TIMESTAMPTZ` | No | `DEFAULT NOW()` | Fecha de alta |

#### `legal_templates`
Plantillas versionadas de contratos firmados digitalmente (US-42, US-15).
| Columna | Tipo | Nulo | Restricción / Default | Descripción |
|---|---|:---:|---|---|
| `id` | `UUID` | No | `PRIMARY KEY DEFAULT gen_random_uuid()` | Identificador |
| `code` | `VARCHAR(50)` | No | | Código de plantilla (ej. CONTRATO_PRESTAMO) |
| `version` | `SMALLINT` | No | `DEFAULT 1` | Versión incremental del documento |
| `content` | `TEXT` | No | | Contenido HTML/Markdown con placeholders `{{}}` |
| `is_current` | `BOOLEAN` | No | `DEFAULT TRUE` | Si es la versión activa |
| `created_by` | `UUID` | Sí | `REFERENCES users(id)` | Usuario creador |
| `created_at` | `TIMESTAMPTZ` | No | `DEFAULT NOW()` | Fecha de registro |

*Restricción:* `UNIQUE (code, version)`.

---

### Módulo 2: Seguridad, Accesos y Auditoría

#### `users`
Cuentas de usuario de backoffice y portal cliente (US-01, US-02, US-43). Sin `branch_id`.
| Columna | Tipo | Nulo | Restricción / Default | Descripción |
|---|---|:---:|---|---|
| `id` | `UUID` | No | `PRIMARY KEY DEFAULT gen_random_uuid()` | Identificador |
| `email` | `VARCHAR(150)` | No | `UNIQUE` | Correo corporativo o personal |
| `password_hash` | `VARCHAR(255)` | No | | Hash criptográfico (BCrypt / Argon2id) |
| `full_name` | `VARCHAR(200)` | No | | Nombres y apellidos |
| `phone` | `VARCHAR(20)` | Sí | | Teléfono de contacto |
| `role` | `user_role` | No | | Rol asignado en RBAC |
| `status` | `user_status_type` | No | `DEFAULT 'ACTIVE'` | Estado de la cuenta |
| `failed_login_count` | `SMALLINT` | No | `DEFAULT 0` | Contador de reintentos fallidos |
| `locked_until` | `TIMESTAMPTZ` | Sí | | Bloqueo temporal por fuerza bruta |
| `last_login_at` | `TIMESTAMPTZ` | Sí | | Marca temporal del último acceso |
| `invitation_token` | `VARCHAR(255)` | Sí | | Token de invitación para onboarding de operador |
| `invitation_sent_at` | `TIMESTAMPTZ` | Sí | | Envío de invitación |
| `password_changed_at` | `TIMESTAMPTZ` | Sí | | Registro de cambio de credencial |
| `created_at` | `TIMESTAMPTZ` | No | `DEFAULT NOW()` | Fecha de alta |
| `updated_at` | `TIMESTAMPTZ` | No | `DEFAULT NOW()` | Modificación |

> [!NOTE] Se eliminó `branch_id`. La operación es 100% digital centralizada; no existe asignación a sucursal física.

#### `refresh_tokens`
Tokens rotativos para renovación de sesiones JWT por dispositivo (US-01).
| Columna | Tipo | Nulo | Restricción / Default | Descripción |
|---|---|:---:|---|---|
| `id` | `UUID` | No | `PRIMARY KEY DEFAULT gen_random_uuid()` | Identificador |
| `user_id` | `UUID` | No | `REFERENCES users(id) ON DELETE CASCADE` | Propietario del token |
| `token_hash` | `VARCHAR(255)` | No | `UNIQUE` | Hash del refresh token |
| `issued_at` | `TIMESTAMPTZ` | No | `DEFAULT NOW()` | Emisión |
| `expires_at` | `TIMESTAMPTZ` | No | | Expiración |
| `revoked_at` | `TIMESTAMPTZ` | Sí | | Revocación explícita (logout o robo de dispositivo) |
| `user_ip` | `INET` | Sí | | IP de origen |
| `user_agent` | `TEXT` | Sí | | Agente de navegación o app móvil |
| `family_id` | `UUID` | No | Índice `idx_refresh_tokens_family_id` (migración 16) | Familia (sesión): id del token del login, heredado en cada rotación. Si se reúsa un token ya rotado se revoca la familia completa |
| `replaced_by_token_id` | `UUID` | Sí | `REFERENCES refresh_tokens(id) ON DELETE SET NULL DEFERRABLE INITIALLY DEFERRED` (migración 16) | Token emitido al rotar este. `NULL` = nunca rotado |

#### `audit_logs`
Pista inmutable de auditoría *append-only* (US-03).
| Columna | Tipo | Nulo | Restricción / Default | Descripción |
|---|---|:---:|---|---|
| `id` | `BIGSERIAL` | No | `PRIMARY KEY` | Secuencia incremental |
| `entity_name` | `VARCHAR(100)` | No | | Entidad afectada (ej. `loans`, `payments`) |
| `entity_id` | `VARCHAR(100)` | No | | ID del registro intervenido |
| `action` | `VARCHAR(50)` | No | | Operación (CREATE, UPDATE, APPROVE, DISBURSE, REVERSE) |
| `state_before` | `JSONB` | Sí | | Instantánea previa de los datos |
| `state_after` | `JSONB` | Sí | | Instantánea posterior de los datos |
| `user_id` | `UUID` | Sí | `REFERENCES users(id)` | Usuario responsable |
| `user_ip` | `INET` | Sí | | Dirección IP de la acción |
| `created_at` | `TIMESTAMPTZ` | No | `DEFAULT NOW()` | Fecha exacta |

---

### Módulo 3: Clientes, KYC y Cuentas Bancarias

#### `customers`
Padrón maestro de clientes prestatarios (US-04, US-06).
| Columna | Tipo | Nulo | Restricción / Default | Descripción |
|---|---|:---:|---|---|
| `id` | `UUID` | No | `PRIMARY KEY DEFAULT gen_random_uuid()` | Identificador |
| `person_type` | `person_type` | No | `DEFAULT 'NATURAL'` | Tipo de persona |
| `doc_type` | `document_type` | No | | Tipo de documento |
| `doc_number` | `VARCHAR(20)` | No | | Número de documento (DNI, RUC, etc.) |
| `full_name` | `VARCHAR(200)` | No | | Nombres o Razón Social |
| `email` | `VARCHAR(150)` | Sí | | Correo del cliente |
| `phone_primary` | `VARCHAR(20)` | Sí | | Teléfono principal |
| `phone_secondary` | `VARCHAR(20)` | Sí | | Teléfono alternativo |
| `address` | `TEXT` | Sí | | Dirección domiciliaria |
| `district` | `VARCHAR(100)` | Sí | | Distrito |
| `birth_date` | `DATE` | Sí | | Fecha de nacimiento para control de edad |
| `monthly_net_income` | `NUMERIC(18,4)` | No | `DEFAULT 0` | Ingreso mensual neto sustentado |
| `occupation` | `VARCHAR(150)` | Sí | | Ocupación o cargo |
| `employer_name` | `VARCHAR(200)` | Sí | | Centro laboral |
| `work_seniority_months` | `SMALLINT` | No | `DEFAULT 0` | Antigüedad laboral en meses |
| `kyc_status` | `kyc_status_type` | No | `DEFAULT 'PENDING'` | Estado de cumplimiento digital |
| `kyc_verified_by` | `UUID` | Sí | `REFERENCES users(id)` | Analista que verificó el KYC digitalmente |
| `kyc_verified_at` | `TIMESTAMPTZ` | Sí | | Fecha de dictamen KYC |
| `kyc_notes` | `TEXT` | Sí | | Observaciones de cumplimiento |
| `kyc_expires_at` | `TIMESTAMPTZ` | Sí | | Caducidad de la vigencia KYC |
| `credit_balance` | `NUMERIC(18,4)` | No | `DEFAULT 0 CHECK (>= 0)` | Saldo a favor por sobrepagos |
| `portal_user_id` | `UUID` | Sí | `REFERENCES users(id)` | Cuenta de acceso al portal cliente |
| `consent_given_at` | `TIMESTAMPTZ` | Sí | | Consentimiento LPDP/GDPR digital |
| `consent_ip` | `INET` | Sí | | IP donde otorgó consentimiento |
| `created_by` | `UUID` | Sí | `REFERENCES users(id)` | Asesor que registró |
| `created_at` | `TIMESTAMPTZ` | No | `DEFAULT NOW()` | Creación |
| `updated_at` | `TIMESTAMPTZ` | No | `DEFAULT NOW()` | Actualización |

*Restricción:* `UNIQUE (doc_type, doc_number)`.

#### `customer_bank_accounts`
Cuentas bancarias, tarjetas y billeteras digitales del cliente. Destino del desembolso.
| Columna | Tipo | Nulo | Restricción / Default | Descripción |
|---|---|:---:|---|---|
| `id` | `UUID` | No | `PRIMARY KEY DEFAULT gen_random_uuid()` | Identificador |
| `customer_id` | `UUID` | No | `REFERENCES customers(id) ON DELETE CASCADE` | Cliente titular |
| `bank_name` | `VARCHAR(100)` | No | | Banco o billetera (BCP, BBVA, Yape, Plin) |
| `account_type` | `bank_account_type` | No | | Tipo: CUENTA_AHORRO, TARJETA_DEBITO, BILLETERA_DIGITAL |
| `account_number_masked` | `VARCHAR(30)` | No | | Número enmascarado (últimos 4 dígitos) |
| `cci` | `VARCHAR(30)` | Sí | | Código Interbancario de 20 dígitos (transferencias) |
| `phone_wallet` | `VARCHAR(20)` | Sí | | Teléfono vinculado a Yape/Plin |
| `is_verified` | `BOOLEAN` | No | `DEFAULT FALSE` | ¿La titularidad fue verificada? |
| `verified_by` | `UUID` | Sí | `REFERENCES users(id)` | Analista que verificó |
| `verified_at` | `TIMESTAMPTZ` | Sí | | Fecha de verificación |
| `is_primary` | `BOOLEAN` | No | `DEFAULT FALSE` | Cuenta principal para desembolsos |
| `status` | `bank_account_status` | No | `DEFAULT 'PENDING_VERIFICATION'` | Estado de la cuenta |
| `created_at` | `TIMESTAMPTZ` | No | `DEFAULT NOW()` | Fecha de alta |
| `updated_at` | `TIMESTAMPTZ` | No | `DEFAULT NOW()` | Modificación |

#### `customer_documents`
Expediente documental probatorio KYC digital (US-05).
| Columna | Tipo | Nulo | Restricción / Default | Descripción |
|---|---|:---:|---|---|
| `id` | `UUID` | No | `PRIMARY KEY DEFAULT gen_random_uuid()` | Identificador |
| `customer_id` | `UUID` | No | `REFERENCES customers(id) ON DELETE CASCADE` | Cliente titular |
| `doc_category` | `VARCHAR(60)` | No | | DNI_FRONTAL, BOLETA_PAGO, SELFIE, ESTADO_CUENTA_BANCO, etc. |
| `file_hash` | `VARCHAR(100)` | No | | Hash SHA-256 del contenido para integridad |
| `storage_uri` | `TEXT` | No | | Localizador en Object Storage (S3/Azure Blob) |
| `mime_type` | `VARCHAR(60)` | Sí | | Formato MIME (image/jpeg, application/pdf) |
| `uploaded_by` | `UUID` | Sí | `REFERENCES users(id)` | Usuario o cliente que subió el archivo |
| `uploaded_at` | `TIMESTAMPTZ` | No | `DEFAULT NOW()` | Fecha de carga |

#### `otp_verifications`
Códigos de un solo uso para validación en el onboarding y operaciones sensibles (US-26).
| Columna | Tipo | Nulo | Restricción / Default | Descripción |
|---|---|:---:|---|---|
| `id` | `UUID` | No | `PRIMARY KEY DEFAULT gen_random_uuid()` | Identificador |
| `customer_id` | `UUID` | No | `REFERENCES customers(id) ON DELETE CASCADE` | Cliente asociado |
| `target` | `VARCHAR(150)` | No | | Destino del envío (email o teléfono) |
| `channel` | `notification_channel` | No | | Canal de despacho (SMS, WhatsApp, Email) |
| `otp_hash` | `VARCHAR(255)` | No | | Hash del código numérico |
| `expires_at` | `TIMESTAMPTZ` | No | | Vencimiento del código |
| `verified_at` | `TIMESTAMPTZ` | Sí | | Momento de validación exitosa |
| `created_at` | `TIMESTAMPTZ` | No | `DEFAULT NOW()` | Emisión |

---

### Módulo 4: Productos de Crédito y Originación

#### `loan_products`
Plantillas de productos financieros y reglas de amortización (US-07, US-08, US-09).
| Columna | Tipo | Nulo | Restricción / Default | Descripción |
|---|---|:---:|---|---|
| `id` | `UUID` | No | `PRIMARY KEY DEFAULT gen_random_uuid()` | Identificador |
| `name` | `VARCHAR(150)` | No | | Nombre comercial del producto |
| `description` | `TEXT` | Sí | | Descripción detallada |
| `currency_id` | `UUID` | No | `REFERENCES currencies(id)` | Moneda de operación |
| `min_amount` | `NUMERIC(18,4)` | No | `CHECK (min_amount > 0)` | Monto mínimo prestable |
| `max_amount` | `NUMERIC(18,4)` | No | | Monto máximo prestable |
| `min_term` | `SMALLINT` | No | `CHECK (min_term >= 1)` | Plazo mínimo en cuotas |
| `max_term` | `SMALLINT` | No | | Plazo máximo en cuotas |
| `allowed_frequencies` | `payment_frequency[]` | No | `DEFAULT '{MENSUAL}'` | Periodicidades admitidas |
| `amortization_method` | `amortization_method` | No | `DEFAULT 'FRANCES'` | Sistema de amortización |
| `rate_type` | `rate_type` | No | `DEFAULT 'EFECTIVA'` | Tipo de tasa |
| `annual_rate` | `NUMERIC(12,6)` | No | `CHECK (annual_rate > 0)` | Tasa Efectiva Anual (TEA en decimal) |
| `opening_fee_rate` | `NUMERIC(12,6)` | No | `DEFAULT 0` | Comisión de apertura porcentual |
| `opening_fee_fixed` | `NUMERIC(18,4)` | No | `DEFAULT 0` | Comisión fija de apertura |
| `opening_fee_mode` | `commission_mode` | No | `DEFAULT 'DEDUCTIVA'` | Descontada o financiada |
| `periodic_fee` | `NUMERIC(18,4)` | No | `DEFAULT 0` | Gasto administrativo o seguro por cuota |
| `grace_days` | `SMALLINT` | No | `DEFAULT 0` | Días de tolerancia antes de computar mora |
| `mora_rate_per_day` | `NUMERIC(12,6)` | No | `DEFAULT 0` | Tasa diaria moratoria |
| `mora_base` | `mora_base_type` | No | `DEFAULT 'SALDO_CAPITAL_VENCIDO'` | Base de cálculo de penalidad |
| `allows_anatocismo` | `BOOLEAN` | No | `DEFAULT FALSE` | Capitalización de mora (anatocismo) |
| `prepayment_penalty_rate` | `NUMERIC(12,6)` | No | `DEFAULT 0` | Penalidad porcentual por cancelación anticipada |
| `min_age` | `SMALLINT` | No | `DEFAULT 18` | Edad mínima requerida |
| `max_age` | `SMALLINT` | No | `DEFAULT 70` | Edad máxima admisible |
| `min_monthly_income` | `NUMERIC(18,4)` | No | `DEFAULT 0` | Ingreso mensual mínimo |
| `holiday_shift_direction` | `VARCHAR(4)` | No | `DEFAULT 'NEXT'` | Desplazamiento por día inhábil (NEXT/PREV) |
| `status` | `product_status` | No | `DEFAULT 'ACTIVO'` | Estado de oferta |
| `created_by` | `UUID` | Sí | `REFERENCES users(id)` | Creador |
| `created_at` | `TIMESTAMPTZ` | No | `DEFAULT NOW()` | Fecha de alta |
| `updated_at` | `TIMESTAMPTZ` | No | `DEFAULT NOW()` | Modificación |

#### `loan_applications`
Solicitudes formales de crédito con cálculo de ratio DTI y dictamen (US-10, US-12).
| Columna | Tipo | Nulo | Restricción / Default | Descripción |
|---|---|:---:|---|---|
| `id` | `UUID` | No | `PRIMARY KEY DEFAULT gen_random_uuid()` | Identificador |
| `customer_id` | `UUID` | No | `REFERENCES customers(id)` | Solicitante |
| `product_id` | `UUID` | No | `REFERENCES loan_products(id)` | Producto postulado |
| `requested_amount` | `NUMERIC(18,4)` | No | `CHECK (> 0)` | Importe solicitado |
| `approved_amount` | `NUMERIC(18,4)` | Sí | | Importe aprobado por el comité |
| `term` | `SMALLINT` | No | `CHECK (>= 1)` | Número de cuotas |
| `frequency` | `payment_frequency` | No | | Frecuencia de pago elegida |
| `destination` | `loan_purpose_type` | No | `DEFAULT 'CONSUMO'` | Destino del préstamo |
| `status` | `application_status` | No | `DEFAULT 'SOLICITADO'` | Estado de evaluación |
| `dti_ratio` | `NUMERIC(8,4)` | Sí | | Ratio Cuota-Ingreso (Debt-to-Income %) |
| `score` | `NUMERIC(8,4)` | Sí | | Puntuación crediticia |
| `system_recommendation` | `credit_decision` | Sí | | Recomendación del motor de scoring |
| `decision` | `VARCHAR(30)` | Sí | | Veredicto del analista |
| `decision_notes` | `TEXT` | Sí | | Justificación técnica obligatoria |
| `decision_by` | `UUID` | Sí | `REFERENCES users(id)` | Analista dictaminador |
| `decided_at` | `TIMESTAMPTZ` | Sí | | Fecha del dictamen |
| `originated_from` | `VARCHAR(20)` | No | `DEFAULT 'BACKOFFICE'` | Canal (BACKOFFICE o PORTAL) |
| `created_by` | `UUID` | Sí | `REFERENCES users(id)` | Usuario gestor |
| `created_at` | `TIMESTAMPTZ` | No | `DEFAULT NOW()` | Creación |
| `updated_at` | `TIMESTAMPTZ` | No | `DEFAULT NOW()` | Modificación |

---

### Módulo 5: Préstamos, Desembolsos Digitales y Cronogramas

#### `loans`
Créditos formalizados y desembolsados digitalmente con condiciones financieras congeladas (US-13, US-31).
| Columna | Tipo | Nulo | Restricción / Default | Descripción |
|---|---|:---:|---|---|
| `id` | `UUID` | No | `PRIMARY KEY DEFAULT gen_random_uuid()` | Identificador |
| `application_id` | `UUID` | Sí | `UNIQUE REFERENCES loan_applications(id)` | Solicitud generadora |
| `customer_id` | `UUID` | No | `REFERENCES customers(id)` | Deudor titular |
| `product_id` | `UUID` | No | `REFERENCES loan_products(id)` | Producto base |
| `principal` | `NUMERIC(18,4)` | No | `CHECK (> 0)` | Capital contractual otorgado |
| `disbursed_amount` | `NUMERIC(18,4)` | No | | Importe neto transferido al cliente |
| `annual_rate` | `NUMERIC(12,6)` | No | | TEA congelada al desembolsar |
| `amortization_method` | `amortization_method` | No | | Método pactado |
| `frequency` | `payment_frequency` | No | | Frecuencia de pago |
| `term` | `SMALLINT` | No | | Número de cuotas |
| `grace_days` | `SMALLINT` | No | `DEFAULT 0` | Días de gracia congelados |
| `mora_rate_per_day` | `NUMERIC(12,6)` | No | `DEFAULT 0` | Tasa diaria moratoria pactada |
| `mora_base` | `mora_base_type` | No | `DEFAULT 'SALDO_CAPITAL_VENCIDO'` | Base de cálculo de mora |
| `opening_fee_amount` | `NUMERIC(18,4)` | No | `DEFAULT 0` | Importe de comisión de apertura |
| `periodic_fee` | `NUMERIC(18,4)` | No | `DEFAULT 0` | Cargo administrativo periódico |
| `prepayment_penalty_rate` | `NUMERIC(12,6)` | No | `DEFAULT 0` | Tasa de penalidad por prepago |
| `disbursement_date` | `DATE` | No | | Fecha de transferencia de fondos |
| `first_due_date` | `DATE` | No | | Fecha de vencimiento de la cuota 1 |
| `total_scheduled_interest` | `NUMERIC(18,4)` | No | `DEFAULT 0` | Interés proyectado total congelado |
| `total_scheduled_fees` | `NUMERIC(18,4)` | No | `DEFAULT 0` | Comisiones proyectadas totales |
| `total_paid_principal` | `NUMERIC(18,4)` | No | `DEFAULT 0` | Capital amortizado a la fecha |
| `total_paid_interest` | `NUMERIC(18,4)` | No | `DEFAULT 0` | Interés amortizado a la fecha |
| `total_paid_fees` | `NUMERIC(18,4)` | No | `DEFAULT 0` | Comisiones amortizadas |
| `total_paid_mora` | `NUMERIC(18,4)` | No | `DEFAULT 0` | Mora cancelada acumulada |
| `status` | `loan_status` | No | `DEFAULT 'VIGENTE'` | Estado del crédito |
| `close_reason` | `loan_close_reason` | Sí | | Motivo de extinción de la deuda |
| `closed_at` | `TIMESTAMPTZ` | Sí | | Fecha de cierre definitivo |
| `purpose_type` | `loan_purpose_type` | No | `DEFAULT 'CONSUMO'` | Destino de la operación |
| `refinanced_from_loan_id` | `UUID` | Sí | `REFERENCES loans(id)` | Préstamo anterior liquidado por reestructuración |
| `disbursement_bank_account_id` | `UUID` | Sí | `REFERENCES company_bank_accounts(id)` | Cuenta de la Fintech desde donde salió la transferencia |
| `customer_bank_account_id` | `UUID` | Sí | `REFERENCES customer_bank_accounts(id)` | Cuenta del cliente que recibió el depósito |
| `disbursed_by` | `UUID` | Sí | `REFERENCES users(id)` | Operador que aprobó la transferencia |
| `idempotency_key` | `VARCHAR(100)` | Sí | `UNIQUE` | Clave UUID de protección de reintentos |
| `created_at` | `TIMESTAMPTZ` | No | `DEFAULT NOW()` | Creación |
| `updated_at` | `TIMESTAMPTZ` | No | `DEFAULT NOW()` | Modificación |

#### `disbursements`
Registro del estado de cada transferencia bancaria de desembolso (US-13).
| Columna | Tipo | Nulo | Restricción / Default | Descripción |
|---|---|:---:|---|---|
| `id` | `UUID` | No | `PRIMARY KEY DEFAULT gen_random_uuid()` | Identificador |
| `loan_id` | `UUID` | No | `UNIQUE REFERENCES loans(id)` | Préstamo desembolsado |
| `company_bank_account_id` | `UUID` | No | `REFERENCES company_bank_accounts(id)` | Cuenta de la Fintech de origen |
| `customer_bank_account_id` | `UUID` | No | `REFERENCES customer_bank_accounts(id)` | Cuenta del cliente de destino |
| `amount` | `NUMERIC(18,4)` | No | `CHECK (> 0)` | Monto transferido |
| `currency_id` | `UUID` | No | `REFERENCES currencies(id)` | Moneda |
| `channel` | `disbursement_channel` | No | | Canal: TRANSFERENCIA_BANCARIA, YAPE, PLIN, DEPOSITO_CCI |
| `status` | `disbursement_status` | No | `DEFAULT 'PENDING'` | Estado del proceso bancario |
| `operation_number` | `VARCHAR(100)` | Sí | | Número de operación del banco (llega por webhook) |
| `external_reference` | `VARCHAR(150)` | Sí | | Referencia de la pasarela o banco |
| `bank_response_code` | `VARCHAR(20)` | Sí | | Código de respuesta del banco |
| `bank_response_message` | `TEXT` | Sí | | Mensaje de error si status=FAILED |
| `initiated_at` | `TIMESTAMPTZ` | No | `DEFAULT NOW()` | Cuando se inició la transferencia |
| `confirmed_at` | `TIMESTAMPTZ` | Sí | | Cuando el banco confirmó |
| `failed_at` | `TIMESTAMPTZ` | Sí | | Cuando el banco rechazó |
| `idempotency_key` | `VARCHAR(100)` | Sí | `UNIQUE` | Protección contra reintentos duplicados |
| `initiated_by` | `UUID` | Sí | `REFERENCES users(id)` | Operador que procesó el desembolso |

#### `schedules`
Versiones inmutables de los cronogramas de amortización (US-14, US-31).
| Columna | Tipo | Nulo | Restricción / Default | Descripción |
|---|---|:---:|---|---|
| `id` | `UUID` | No | `PRIMARY KEY DEFAULT gen_random_uuid()` | Identificador |
| `loan_id` | `UUID` | No | `REFERENCES loans(id)` | Préstamo asociado |
| `snapshot_version` | `SMALLINT` | No | `DEFAULT 1` | Versión (1=original, 2+=reestructurado) |
| `generated_at` | `TIMESTAMPTZ` | No | `DEFAULT NOW()` | Momento del cálculo y congelamiento |
| `is_active` | `BOOLEAN` | No | `DEFAULT TRUE` | Indica si es el cronograma vigente |
| `notes` | `TEXT` | Sí | | Motivo del recálculo |

*Restricción:* `UNIQUE (loan_id, snapshot_version)`.

#### `installments`
Cuotas del plan de pagos con separación estricta entre proyección inmutable y acumuladores de cobro (US-14, US-16).
| Columna | Tipo | Nulo | Restricción / Default | Descripción |
|---|---|:---:|---|---|
| `id` | `UUID` | No | `PRIMARY KEY DEFAULT gen_random_uuid()` | Identificador |
| `schedule_id` | `UUID` | No | `REFERENCES schedules(id)` | Versión de cronograma |
| `loan_id` | `UUID` | No | `REFERENCES loans(id)` | Préstamo padre |
| `installment_number` | `SMALLINT` | No | | Número de cuota correlativo (1..N) |
| `due_date` | `DATE` | No | | Fecha de vencimiento pactada |
| `principal` | `NUMERIC(18,4)` | No | `CHECK (>= 0)` | Capital nominal proyectado (INMUTABLE) |
| `interest` | `NUMERIC(18,4)` | No | `CHECK (>= 0)` | Interés nominal proyectado (INMUTABLE) |
| `fee` | `NUMERIC(18,4)` | No | `DEFAULT 0` | Cargo fijo proyectado (INMUTABLE) |
| `total_due` | `NUMERIC(18,4)` | No | | Importe de cuota (`principal + interest + fee`) |
| `paid_principal` | `NUMERIC(18,4)` | No | `DEFAULT 0` | Capital cancelado acumulado |
| `paid_interest` | `NUMERIC(18,4)` | No | `DEFAULT 0` | Interés cancelado acumulado |
| `paid_fee` | `NUMERIC(18,4)` | No | `DEFAULT 0` | Comisiones canceladas acumuladas |
| `late_fee` | `NUMERIC(18,4)` | No | `DEFAULT 0` | Mora devengada por el job nocturno |
| `paid_late_fee` | `NUMERIC(18,4)` | No | `DEFAULT 0` | Mora pagada acumulada |
| `status` | `installment_status` | No | `DEFAULT 'PENDING'` | Estado de exigibilidad |
| `days_late` | `SMALLINT` | No | `DEFAULT 0` | Días de atraso calculados |
| `last_mora_calc_date` | `DATE` | Sí | | Fecha del último cálculo de mora |

*Restricción:* `UNIQUE (loan_id, installment_number)`.

#### `prepayment_quotes`
Cotizaciones de liquidación anticipada (US-30).
| Columna | Tipo | Nulo | Restricción / Default | Descripción |
|---|---|:---:|---|---|
| `id` | `UUID` | No | `PRIMARY KEY DEFAULT gen_random_uuid()` | Identificador |
| `loan_id` | `UUID` | No | `REFERENCES loans(id)` | Préstamo a cancelar |
| `as_of_date` | `DATE` | No | | Fecha de proyección del corte |
| `outstanding_principal` | `NUMERIC(18,4)` | No | | Capital insoluto restante |
| `accrued_interest` | `NUMERIC(18,4)` | No | `DEFAULT 0` | Interés devengado al día del corte |
| `accrued_mora` | `NUMERIC(18,4)` | No | `DEFAULT 0` | Mora acumulada al día |
| `prepayment_penalty` | `NUMERIC(18,4)` | No | `DEFAULT 0` | Recargo por prepago pactado |
| `total_to_pay` | `NUMERIC(18,4)` | No | | Importe cancelatorio líquido |
| `quote_valid_until` | `DATE` | No | | Fecha límite de vigencia de la cotización |
| `is_applied` | `BOOLEAN` | No | `DEFAULT FALSE` | Si la cotización fue pagada |
| `generated_by` | `UUID` | Sí | `REFERENCES users(id)` | Operador que emitió la cotización |
| `created_at` | `TIMESTAMPTZ` | No | `DEFAULT NOW()` | Fecha de generación |

---

### Módulo 6: Pagos Digitales, Prelación y Libro Mayor

#### `payments`
Registro inmutable de recaudación digital (US-16, US-18). Sin `cashbox_id`.
| Columna | Tipo | Nulo | Restricción / Default | Descripción |
|---|---|:---:|---|---|
| `id` | `UUID` | No | `PRIMARY KEY DEFAULT gen_random_uuid()` | Identificador |
| `loan_id` | `UUID` | No | `REFERENCES loans(id)` | Préstamo acreditado |
| `payment_kind` | `payment_kind` | No | `DEFAULT 'NORMAL'` | NORMAL o REVERSAL (contra-asiento) |
| `amount` | `NUMERIC(18,4)` | No | | Monto monetario del abono |
| `payment_method_id` | `UUID` | Sí | `REFERENCES payment_methods(id)` | Canal digital de pago |
| `customer_bank_account_id` | `UUID` | Sí | `REFERENCES customer_bank_accounts(id)` | Cuenta del cliente desde donde pagó |
| `company_bank_account_id` | `UUID` | Sí | `REFERENCES company_bank_accounts(id)` | Cuenta de la Fintech donde llegó el pago |
| `gateway_reference` | `VARCHAR(150)` | Sí | | Referencia de la pasarela (Yape/Plin/Niubiz/webhook) |
| `external_reference` | `VARCHAR(150)` | Sí | | Número de operación del banco externo |
| `paid_at` | `TIMESTAMPTZ` | No | | Fecha/hora efectiva del cobro digital |
| `status` | `payment_status` | No | `DEFAULT 'APPLIED'` | Estado del pago |
| `reversal_of_id` | `UUID` | Sí | `REFERENCES payments(id)` | Pago original revertido |
| `reversal_reason` | `TEXT` | Sí | | Causa de anulación obligatoria |
| `receipt_number` | `VARCHAR(30)` | Sí | `UNIQUE` | Número correlativo único de recibo digital |
| `receipt_url` | `TEXT` | Sí | | URL del PDF del recibo en Object Storage |
| `registered_by` | `UUID` | Sí | `REFERENCES users(id)` | Operador o proceso automático receptor |
| `idempotency_key` | `VARCHAR(100)` | Sí | `UNIQUE` | Clave de idempotencia del abono |
| `created_at` | `TIMESTAMPTZ` | No | `DEFAULT NOW()` | Timestamp de registro |

#### `payment_allocations`
Desglose de la cascada legal de prelación por cuota intervenida (US-16, RF-16.4).
| Columna | Tipo | Nulo | Restricción / Default | Descripción |
|---|---|:---:|---|---|
| `id` | `UUID` | No | `PRIMARY KEY DEFAULT gen_random_uuid()` | Identificador |
| `payment_id` | `UUID` | No | `REFERENCES payments(id)` | Pago matriz |
| `installment_id` | `UUID` | No | `REFERENCES installments(id)` | Cuota amortizada |
| `to_mora` | `NUMERIC(18,4)` | No | `DEFAULT 0 CHECK (>= 0)` | Amortización a mora (Prelación 1°) |
| `to_interest` | `NUMERIC(18,4)` | No | `DEFAULT 0 CHECK (>= 0)` | Amortización a interés (Prelación 2°) |
| `to_fee` | `NUMERIC(18,4)` | No | `DEFAULT 0 CHECK (>= 0)` | Amortización a comisiones (Prelación 3°) |
| `to_principal` | `NUMERIC(18,4)` | No | `DEFAULT 0 CHECK (>= 0)` | Amortización a capital (Prelación 4°) |
| `created_at` | `TIMESTAMPTZ` | No | `DEFAULT NOW()` | Fecha de imputación |

#### `transactions`
Libro Mayor Financiero (Ledger) inmutable (US-13, US-16). Sin `cashbox_id`.
| Columna | Tipo | Nulo | Restricción / Default | Descripción |
|---|---|:---:|---|---|
| `id` | `BIGSERIAL` | No | `PRIMARY KEY` | Secuencia correlativa inmutable |
| `loan_id` | `UUID` | Sí | `REFERENCES loans(id)` | Préstamo vinculado |
| `payment_id` | `UUID` | Sí | `REFERENCES payments(id)` | Pago vinculado |
| `disbursement_id` | `UUID` | Sí | `REFERENCES disbursements(id)` | Desembolso bancario vinculado |
| `company_bank_account_id` | `UUID` | Sí | `REFERENCES company_bank_accounts(id)` | Cuenta de la Fintech involucrada |
| `transaction_type` | `transaction_type` | No | | Concepto del movimiento |
| `direction` | `transaction_dir` | No | | INFLOW (cobro) o OUTFLOW (desembolso) |
| `amount` | `NUMERIC(18,4)` | No | `CHECK (> 0)` | Importe monetario positivo |
| `currency_id` | `UUID` | No | `REFERENCES currencies(id)` | Moneda |
| `reference` | `VARCHAR(150)` | Sí | | Referencia bancaria / número de operación |
| `notes` | `TEXT` | Sí | | Glosa o detalle contable |
| `created_by` | `UUID` | Sí | `REFERENCES users(id)` | Usuario responsable |
| `created_at` | `TIMESTAMPTZ` | No | `DEFAULT NOW()` | Registro cronológico |

---

### Módulo 7: Tesorería Digital y Conciliación Bancaria

#### `bank_statements`
Movimientos importados del extracto bancario. Base de la conciliación automática (US-37).
| Columna | Tipo | Nulo | Restricción / Default | Descripción |
|---|---|:---:|---|---|
| `id` | `UUID` | No | `PRIMARY KEY DEFAULT gen_random_uuid()` | Identificador |
| `company_bank_account_id` | `UUID` | No | `REFERENCES company_bank_accounts(id)` | Cuenta bancaria de la Fintech |
| `statement_date` | `DATE` | No | | Período del extracto |
| `operation_number` | `VARCHAR(100)` | No | | Número de operación del banco |
| `value_date` | `DATE` | No | | Fecha valor del movimiento |
| `amount` | `NUMERIC(18,4)` | No | | Positivo=ingreso, Negativo=egreso |
| `currency_id` | `UUID` | No | `REFERENCES currencies(id)` | Moneda |
| `description` | `TEXT` | Sí | | Concepto/descripción del banco |
| `external_reference` | `VARCHAR(150)` | Sí | | Referencia externa del pagador |
| `reconciliation_status` | `reconciliation_status` | No | `DEFAULT 'PENDIENTE_CONCILIACION'` | Estado de conciliación |
| `reconciled_payment_id` | `UUID` | Sí | `REFERENCES payments(id)` | Pago interno emparejado |
| `reconciled_disbursement_id` | `UUID` | Sí | `REFERENCES disbursements(id)` | Desembolso emparejado |
| `reconciled_at` | `TIMESTAMPTZ` | Sí | | Fecha de confirmación |
| `reconciled_by` | `UUID` | Sí | `REFERENCES users(id)` | Analista que autorizó |
| `imported_at` | `TIMESTAMPTZ` | No | `DEFAULT NOW()` | Fecha de importación |
| `imported_by` | `UUID` | Sí | `REFERENCES users(id)` | Operador que importó |
| `import_source` | `VARCHAR(50)` | Sí | | ARCHIVO_CSV, API_BANCO, WEBHOOK, MANUAL |

*Restricción:* `UNIQUE (company_bank_account_id, operation_number)`.

---

### Módulo 8: Cobranza Digital y Notificaciones

#### `collection_actions`
Bitácora de gestiones digitales de cobranza (US-21). Sin visitas domiciliarias.
| Columna | Tipo | Nulo | Restricción / Default | Descripción |
|---|---|:---:|---|---|
| `id` | `UUID` | No | `PRIMARY KEY DEFAULT gen_random_uuid()` | Identificador |
| `loan_id` | `UUID` | No | `REFERENCES loans(id)` | Crédito en gestión |
| `installment_id` | `UUID` | Sí | `REFERENCES installments(id)` | Cuota específica reclamada |
| `action_type` | `collection_action_type` | No | | Tipo de gestión digital |
| `note` | `TEXT` | Sí | | Argumentos y respuestas del deudor |
| `status` | `collection_action_status` | No | `DEFAULT 'ACTIVA'` | Estado del compromiso |
| `next_action_at` | `TIMESTAMPTZ` | Sí | | Fecha del siguiente contacto agendado |
| `promise_amount` | `NUMERIC(18,4)` | Sí | | Importe comprometido a abonar |
| `promise_date` | `DATE` | Sí | | Fecha límite comprometida para el pago |
| `created_by` | `UUID` | No | `REFERENCES users(id)` | Gestor de cobranza digital |
| `created_at` | `TIMESTAMPTZ` | No | `DEFAULT NOW()` | Fecha de gestión |
| `updated_at` | `TIMESTAMPTZ` | No | `DEFAULT NOW()` | Actualización |

#### `notifications`
Cola de mensajería saliente omnicanal al cliente (US-19, US-21).
| Columna | Tipo | Nulo | Restricción / Default | Descripción |
|---|---|:---:|---|---|
| `id` | `UUID` | No | `PRIMARY KEY DEFAULT gen_random_uuid()` | Identificador |
| `customer_id` | `UUID` | No | `REFERENCES customers(id)` | Destinatario |
| `channel` | `notification_channel` | No | | Canal (EMAIL, SMS, WHATSAPP, PUSH) |
| `template_code` | `VARCHAR(80)` | No | | Código de plantilla de mensaje |
| `payload` | `JSONB` | Sí | | Diccionario de variables dinámicas |
| `status` | `notification_status` | No | `DEFAULT 'PENDING'` | PENDING, SENT, FAILED |
| `sent_at` | `TIMESTAMPTZ` | Sí | | Momento de despacho confirmado |
| `error_detail` | `TEXT` | Sí | | Causa de fallo técnico |
| `created_at` | `TIMESTAMPTZ` | No | `DEFAULT NOW()` | Encolamiento |

---

### Módulo 9: Integraciones Externas

#### `integration_events`
Bandeja de recepción de notificaciones webhook de bancos y pasarelas de pago (US-36, US-38).
| Columna | Tipo | Nulo | Restricción / Default | Descripción |
|---|---|:---:|---|---|
| `id` | `UUID` | No | `PRIMARY KEY DEFAULT gen_random_uuid()` | Identificador |
| `provider` | `VARCHAR(50)` | No | | Proveedor (bcp, bbva, yape, plin, niubiz) |
| `event_type` | `VARCHAR(80)` | No | | Nombre del evento bancario |
| `external_id` | `VARCHAR(150)` | No | | Código de operación del banco |
| `payload` | `JSONB` | No | | Cuerpo crudo enviado por la entidad bancaria |
| `status` | `integration_event_status` | No | `DEFAULT 'RECEIVED'` | RECEIVED, PROCESSED, DEAD_LETTER |
| `error_detail` | `TEXT` | Sí | | Registro del error |
| `retry_count` | `SMALLINT` | No | `DEFAULT 0` | Número de reintentos ejecutados (0..4) |
| `next_retry_at` | `TIMESTAMPTZ` | Sí | | Próximo reintento con backoff exponencial |
| `processed_at` | `TIMESTAMPTZ` | Sí | | Momento de procesamiento exitoso |
| `created_at` | `TIMESTAMPTZ` | No | `DEFAULT NOW()` | Momento de recepción del webhook |

*Restricción:* `UNIQUE (provider, external_id)`.
