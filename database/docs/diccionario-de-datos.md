# Diccionario de Datos del Sistema

Este documento proporciona la especificación exhaustiva de todos los tipos enumerados (`ENUM`), tablas, columnas, tipos de datos, valores por defecto y restricciones de integridad.

---

## 1. Catálogo de Tipos Enumerados (`ENUM`)

| Tipo ENUM | Valores Permitidos | Dominio / Contexto |
|---|---|---|
| `person_type` | `'NATURAL'`, `'JURIDICA'` | Clasificación legal del cliente |
| `document_type` | `'DNI'`, `'CE'`, `'RUC'`, `'PASAPORTE'` | Tipo de documento de identidad |
| `kyc_status_type` | `'PENDING'`, `'VERIFIED'`, `'REJECTED'`, `'EXPIRED'` | Estado de cumplimiento KYC |
| `user_role` | `'ROLE_ADMIN'`, `'ROLE_ANALYST'`, `'ROLE_CASHIER'`, `'ROLE_COLLECTOR'`, `'ROLE_AUDITOR'`, `'ROLE_CLIENT'` | Roles de acceso en RBAC |
| `user_status_type` | `'ACTIVE'`, `'INACTIVE'`, `'LOCKED'` | Estado de la cuenta de usuario |
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
| `payment_status` | `'APPLIED'`, `'REVERSED'`, `'PENDING_CONFIRMATION'` | Estado del cobro asentado |
| `payment_kind` | `'NORMAL'`, `'REVERSAL'` | Clasificación contable del pago |
| `payment_method_type`| `'EFECTIVO'`, `'TRANSFERENCIA'`, `'YAPE'`, `'PLIN'`, `'TARJETA'`, `'DEPOSITO_VENTANILLA'` | Canal o medio de recaudación |
| `transaction_type` | `'DISBURSEMENT'`, `'PAYMENT'`, `'REVERSAL'`, `'CUSTOMER_CREDIT_BALANCE'`, `'WRITE_OFF'`, `'ADJUSTMENT'`, `'CASH_TRANSFER'`, `'CASH_EXPENSE'`, `'CASH_INCOME'` | Movimientos del Libro Mayor |
| `transaction_dir` | `'INFLOW'`, `'OUTFLOW'` | Sentido del flujo monetario |
| `cashbox_status` | `'ABIERTA'`, `'CERRADA'` | Estado operativo de la caja |
| `cash_movement_type`| `'INGRESO_EXTRAORDINARIO'`, `'EGRESO_GASTO'` | Tipo de flujo misceláneo en caja |
| `transfer_status` | `'EN_TRANSITO'`, `'COMPLETED'`, `'CANCELLED'` | Estado de remesa entre cajas |
| `arqueo_result` | `'CUADRADO'`, `'SOBRANTE'`, `'FALTANTE'` | Balance en el cierre ciego |
| `collection_action_type`| `'LLAMADA'`, `'WHATSAPP'`, `'VISITA_DOMICILIARIA'`, `'NOTIFICACION_ESCRITA'`, `'PROMESA_DE_PAGO'` | Tipo de gestión de cobranza |
| `collection_action_status`| `'ACTIVA'`, `'CUMPLIDA'`, `'PROMESA_INCUMPLIDA'`, `'CERRADA'` | Estado del compromiso de cobro |
| `integration_event_status`| `'RECEIVED'`, `'PROCESSED'`, `'FAILED'`, `'DEAD_LETTER'`, `'FAILED_NEEDS_REVIEW'` | Cola de webhooks bancarios |
| `reconciliation_status`| `'CONCILIADO_AUTOMATICO'`, `'PENDIENTE_CONCILIACION'`, `'DESCUADRADO'`, `'RECONCILED'` | Estado de conciliación bancaria |
| `notification_channel`| `'EMAIL'`, `'SMS'`, `'WHATSAPP'`, `'PUSH'` | Canal de mensajería al cliente |
| `notification_status`| `'PENDING'`, `'SENT'`, `'FAILED'` | Estado de despacho de mensaje |
| `holiday_scope` | `'NACIONAL'`, `'SUCURSAL'` | Cobertura del feriado |

---

## 2. Diccionario de Tablas por Dominio

### Módulo 1: Administración y Catálogos Maestros

#### `company`
Datos institucionales, fiscales y corporativos (US-39).
| Columna | Tipo | Nulo | Restricción / Default | Descripción |
|---|---|:---:|---|---|
| `id` | `UUID` | No | `PRIMARY KEY DEFAULT gen_random_uuid()` | Identificador único |
| `legal_name` | `VARCHAR(200)` | No | | Razón social formal |
| `trade_name` | `VARCHAR(200)` | Sí | | Nombre comercial |
| `tax_id` | `VARCHAR(20)` | No | `UNIQUE` | RUC / NIT / RFC / CIF |
| `legal_address` | `TEXT` | No | | Domicilio fiscal |
| `support_phone` | `VARCHAR(20)` | Sí | | Teléfono de soporte |
| `support_email` | `VARCHAR(150)`| Sí | | Correo oficial |
| `legal_rep_name`| `VARCHAR(200)`| Sí | | Nombre del representante legal |
| `legal_rep_doc` | `VARCHAR(30)` | Sí | | Documento de identidad del representante |
| `logo_url` | `TEXT` | Sí | | URL del isotipo institucional en Object Storage |
| `default_currency_id`| `UUID` | Sí | `REFERENCES currencies(id)` | Moneda principal del sistema |
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

#### `branches`
Sucursales y agencias físicas (US-40).
| Columna | Tipo | Nulo | Restricción / Default | Descripción |
|---|---|:---:|---|---|
| `id` | `UUID` | No | `PRIMARY KEY DEFAULT gen_random_uuid()` | Identificador único |
| `code` | `VARCHAR(20)` | No | `UNIQUE` | Código operativo interno (ej. SUC-01) |
| `name` | `VARCHAR(150)`| No | | Nombre descriptivo de la agencia |
| `address` | `TEXT` | Sí | | Ubicación física |
| `district` | `VARCHAR(100)`| Sí | | Distrito o localidad |
| `is_active` | `BOOLEAN` | No | `DEFAULT TRUE` | Estado operativo |
| `created_at` | `TIMESTAMPTZ` | No | `DEFAULT NOW()` | Fecha de alta |
| `updated_at` | `TIMESTAMPTZ` | No | `DEFAULT NOW()` | Última actualización |

#### `zones`
Zonas territoriales de asignación de asesores y cobranza (US-40).
| Columna | Tipo | Nulo | Restricción / Default | Descripción |
|---|---|:---:|---|---|
| `id` | `UUID` | No | `PRIMARY KEY DEFAULT gen_random_uuid()` | Identificador único |
| `name` | `VARCHAR(100)`| No | | Nombre de la zona |
| `description` | `TEXT` | Sí | | Descripción o cuadrante |
| `branch_id` | `UUID` | Sí | `REFERENCES branches(id)` | Sucursal cabecera |
| `is_active` | `BOOLEAN` | No | `DEFAULT TRUE` | Estado de la zona |
| `created_at` | `TIMESTAMPTZ` | No | `DEFAULT NOW()` | Fecha de creación |

#### `holidays`
Calendario de feriados y días inhábiles para corrimiento de vencimientos (US-41).
| Columna | Tipo | Nulo | Restricción / Default | Descripción |
|---|---|:---:|---|---|
| `id` | `UUID` | No | `PRIMARY KEY DEFAULT gen_random_uuid()` | Identificador único |
| `holiday_date`| `DATE` | No | | Fecha del día festivo |
| `name` | `VARCHAR(150)`| No | | Motivo del feriado |
| `scope` | `holiday_scope`| No | `DEFAULT 'NACIONAL'` | Ámbito de aplicación |
| `branch_id` | `UUID` | Sí | `REFERENCES branches(id)` | Sucursal específica (si scope=SUCURSAL) |
| `is_recurring`| `BOOLEAN` | No | `DEFAULT FALSE` | Si se repite cada año |
| `created_at` | `TIMESTAMPTZ` | No | `DEFAULT NOW()` | Registro |
*Restricción:* `UNIQUE (holiday_date, scope, branch_id)`.

#### `document_types`
Catálogo configurable de documentos de identidad con regex de validación (US-42).
| Columna | Tipo | Nulo | Restricción / Default | Descripción |
|---|---|:---:|---|---|
| `id` | `UUID` | No | `PRIMARY KEY DEFAULT gen_random_uuid()` | Identificador único |
| `code` | `VARCHAR(10)` | No | `UNIQUE` | Nemotécnico (DNI, CE, RUC, PAS) |
| `name` | `VARCHAR(60)` | No | | Nombre descriptivo |
| `char_length` | `SMALLINT` | Sí | | Longitud exacta de caracteres |
| `validation_regex`| `VARCHAR(100)`| Sí | | Expresión regular de validación |
| `is_active` | `BOOLEAN` | No | `DEFAULT TRUE` | Disponibilidad |

#### `payment_methods`
Canales de recepción de abonos admitidos (US-42).
| Columna | Tipo | Nulo | Restricción / Default | Descripción |
|---|---|:---:|---|---|
| `id` | `UUID` | No | `PRIMARY KEY DEFAULT gen_random_uuid()` | Identificador |
| `name` | `VARCHAR(80)` | No | | Nombre del método |
| `method_type`| `payment_method_type`| No | | Clasificación operativa |
| `provider_code`| `VARCHAR(50)`| Sí | | Identificador del proveedor técnico |
| `is_active` | `BOOLEAN` | No | `DEFAULT TRUE` | Estado |
| `created_at` | `TIMESTAMPTZ` | No | `DEFAULT NOW()` | Fecha de alta |

#### `legal_templates`
Plantillas versionadas de contratos, pagarés y hojas resumen con variables dinámicas (US-42, US-15).
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
Cuentas de usuario de backoffice y portal cliente (US-01, US-02, US-43).
| Columna | Tipo | Nulo | Restricción / Default | Descripción |
|---|---|:---:|---|---|
| `id` | `UUID` | No | `PRIMARY KEY DEFAULT gen_random_uuid()` | Identificador |
| `email` | `VARCHAR(150)`| No | `UNIQUE` | Correo corporativo o personal |
| `password_hash`| `VARCHAR(255)`| No | | Hash criptográfico (BCrypt / Argon2id) |
| `full_name` | `VARCHAR(200)`| No | | Nombres y apellidos |
| `phone` | `VARCHAR(20)` | Sí | | Teléfono de contacto |
| `role` | `user_role` | No | | Rol asignado en RBAC |
| `status` | `user_status_type`| No | `DEFAULT 'ACTIVE'` | Estado de la cuenta |
| `branch_id` | `UUID` | Sí | `REFERENCES branches(id)` | Sucursal base asignada |
| `failed_login_count`| `SMALLINT`| No | `DEFAULT 0` | Contador de reintentos fallidos |
| `locked_until`| `TIMESTAMPTZ`| Sí | | Bloqueo temporal por fuerza bruta |
| `last_login_at`| `TIMESTAMPTZ`| Sí | | Marca temporal del último acceso |
| `invitation_token`| `VARCHAR(255)`| Sí | | Token de invitación |
| `invitation_sent_at`| `TIMESTAMPTZ`| Sí | | Envío de invitación |
| `password_changed_at`| `TIMESTAMPTZ`| Sí | | Registro de cambio de credencial |
| `created_at` | `TIMESTAMPTZ` | No | `DEFAULT NOW()` | Fecha de alta |
| `updated_at` | `TIMESTAMPTZ` | No | `DEFAULT NOW()` | Modificación |

#### `refresh_tokens`
Tokens rotativos para renovación de sesiones JWT (US-01).
| Columna | Tipo | Nulo | Restricción / Default | Descripción |
|---|---|:---:|---|---|
| `id` | `UUID` | No | `PRIMARY KEY DEFAULT gen_random_uuid()` | Identificador |
| `user_id` | `UUID` | No | `REFERENCES users(id) ON DELETE CASCADE`| Propietario del token |
| `token_hash` | `VARCHAR(255)`| No | `UNIQUE` | Hash del refresh token |
| `issued_at` | `TIMESTAMPTZ` | No | `DEFAULT NOW()` | Emisión |
| `expires_at` | `TIMESTAMPTZ` | No | | Expiración |
| `revoked_at` | `TIMESTAMPTZ` | Sí | | Revocación explícita (logout) |
| `user_ip` | `INET` | Sí | | IP de origen |
| `user_agent` | `TEXT` | Sí | | Agente de navegación |

#### `user_zones`
Asociación N:M entre gestores y zonas territoriales de cobertura (US-40).
| Columna | Tipo | Nulo | Restricción / Default | Descripción |
|---|---|:---:|---|---|
| `user_id` | `UUID` | No | `REFERENCES users(id) ON DELETE CASCADE` | Colaborador asignado |
| `zone_id` | `UUID` | No | `REFERENCES zones(id) ON DELETE CASCADE` | Zona geográfica |
*Llave Primaria Compuesta:* `PRIMARY KEY (user_id, zone_id)`.

#### `audit_logs`
Pista inmutable de auditoría *append-only* (US-03).
| Columna | Tipo | Nulo | Restricción / Default | Descripción |
|---|---|:---:|---|---|
| `id` | `BIGSERIAL` | No | `PRIMARY KEY` | Secuencia incremental |
| `entity_name` | `VARCHAR(100)`| No | | Entidad afectada (ej. LoanApplication) |
| `entity_id` | `VARCHAR(50)` | No | | ID del registro intervenido |
| `action` | `VARCHAR(30)` | No | | Operación (CREATE, UPDATE, APPROVE, etc.) |
| `state_before`| `JSONB` | Sí | | Instantánea previa de los datos |
| `state_after` | `JSONB` | Sí | | Instantánea posterior de los datos |
| `user_id` | `UUID` | Sí | `REFERENCES users(id)` | Usuario responsable |
| `user_ip` | `INET` | Sí | | Dirección IP |
| `user_agent` | `TEXT` | Sí | | Dispositivo de origen |
| `created_at` | `TIMESTAMPTZ` | No | `DEFAULT NOW()` | Fecha exacta |

---

### Módulo 3: Clientes y Expedientes KYC

#### `customers`
Padrón maestro de clientes prestatarios (US-04, US-06).
| Columna | Tipo | Nulo | Restricción / Default | Descripción |
|---|---|:---:|---|---|
| `id` | `UUID` | No | `PRIMARY KEY DEFAULT gen_random_uuid()` | Identificador |
| `person_type` | `person_type` | No | `DEFAULT 'NATURAL'` | Tipo de persona |
| `doc_type` | `document_type`| No | | Tipo de documento |
| `doc_number` | `VARCHAR(20)` | No | | Número de documento (DNI, RUC, etc.) |
| `full_name` | `VARCHAR(200)`| No | | Nombres o Razón Social |
| `email` | `VARCHAR(150)`| Sí | | Correo del cliente |
| `phone_primary`| `VARCHAR(20)`| Sí | | Teléfono principal |
| `phone_secondary`| `VARCHAR(20)`| Sí | | Teléfono alternativo |
| `address` | `TEXT` | Sí | | Dirección domiciliaria |
| `district` | `VARCHAR(100)`| Sí | | Distrito |
| `birth_date` | `DATE` | Sí | | Fecha de nacimiento para control de edad |
| `monthly_net_income`| `NUMERIC(18,4)`| No | `DEFAULT 0` | Ingreso mensual neto sustentado |
| `occupation` | `VARCHAR(150)`| Sí | | Ocupación o cargo |
| `employer_name`| `VARCHAR(200)`| Sí | | Centro laboral |
| `work_seniority_months`| `SMALLINT`| No | `DEFAULT 0` | Antigüedad laboral en meses |
| `kyc_status` | `kyc_status_type`| No| `DEFAULT 'PENDING'` | Estado de cumplimiento |
| `kyc_verified_by`| `UUID` | Sí | `REFERENCES users(id)` | Analista que verificó el KYC |
| `kyc_verified_at`| `TIMESTAMPTZ`| Sí | | Fecha de dictamen KYC |
| `kyc_notes` | `TEXT` | Sí | | Observaciones de cumplimiento |
| `kyc_expires_at`| `TIMESTAMPTZ`| Sí | | Caducidad de la vigencia KYC |
| `credit_balance`| `NUMERIC(18,4)`| No | `DEFAULT 0 CHECK (credit_balance >= 0)` | Saldo a favor por sobrepagos |
| `portal_user_id`| `UUID` | Sí | `REFERENCES users(id)` | Cuenta de acceso al portal cliente |
| `consent_given_at`| `TIMESTAMPTZ`| Sí | | Consentimiento LPDP/GDPR |
| `consent_ip` | `INET` | Sí | | IP donde otorgó consentimiento |
| `created_by` | `UUID` | Sí | `REFERENCES users(id)` | Asesor que registró |
| `created_at` | `TIMESTAMPTZ` | No | `DEFAULT NOW()` | Creación |
| `updated_at` | `TIMESTAMPTZ` | No | `DEFAULT NOW()` | Actualización |
*Restricción:* `UNIQUE (doc_type, doc_number)`.

#### `customer_documents`
Expediente documental probatorio KYC (US-05).
| Columna | Tipo | Nulo | Restricción / Default | Descripción |
|---|---|:---:|---|---|
| `id` | `UUID` | No | `PRIMARY KEY DEFAULT gen_random_uuid()` | Identificador |
| `customer_id` | `UUID` | No | `REFERENCES customers(id) ON DELETE CASCADE`| Cliente titular |
| `doc_category`| `VARCHAR(60)` | No | | DNI_FRONTAL, BOLETA_PAGO, SELFIE, etc. |
| `file_hash` | `VARCHAR(100)`| No | | Hash SHA-256 del contenido |
| `storage_uri` | `TEXT` | No | | Localizador en almacenamiento de objetos |
| `mime_type` | `VARCHAR(60)` | Sí | | Formato MIME (image/jpeg, application/pdf) |
| `uploaded_by` | `UUID` | Sí | `REFERENCES users(id)` | Usuario o cliente que subió el archivo |
| `uploaded_at` | `TIMESTAMPTZ` | No | `DEFAULT NOW()` | Fecha de carga |

#### `otp_verifications`
Códigos de un solo uso para validación telefónica/correo en el onboarding digital (US-26).
| Columna | Tipo | Nulo | Restricción / Default | Descripción |
|---|---|:---:|---|---|
| `id` | `UUID` | No | `PRIMARY KEY DEFAULT gen_random_uuid()` | Identificador |
| `customer_id` | `UUID` | No | `REFERENCES customers(id) ON DELETE CASCADE`| Cliente asociado |
| `target` | `VARCHAR(150)`| No | | Destino del envío (email o teléfono) |
| `channel` | `notification_channel`| No | | Canal de despacho (SMS, WhatsApp, Email) |
| `otp_hash` | `VARCHAR(255)`| No | | Hash del código numérico |
| `expires_at` | `TIMESTAMPTZ` | No | | Vencimiento del código |
| `verified_at`| `TIMESTAMPTZ` | Sí | | Momento de validación exitosa |
| `created_at` | `TIMESTAMPTZ` | No | `DEFAULT NOW()` | Emisión |

---

### Módulo 4: Productos de Crédito y Originación

#### `loan_products`
Plantillas de productos financieros y reglas de amortización (US-07, US-08, US-09).
| Columna | Tipo | Nulo | Restricción / Default | Descripción |
|---|---|:---:|---|---|
| `id` | `UUID` | No | `PRIMARY KEY DEFAULT gen_random_uuid()` | Identificador |
| `name` | `VARCHAR(150)`| No | | Nombre comercial del producto |
| `description` | `TEXT` | Sí | | Descripción detallada |
| `currency_id` | `UUID` | No | `REFERENCES currencies(id)` | Moneda de operación |
| `min_amount` | `NUMERIC(18,4)`| No | `CHECK (min_amount > 0)` | Monto mínimo prestable |
| `max_amount` | `NUMERIC(18,4)`| No | | Monto máximo prestable |
| `min_term` | `SMALLINT` | No | `CHECK (min_term >= 1)` | Plazo mínimo en cuotas |
| `max_term` | `SMALLINT` | No | | Plazo máximo en cuotas |
| `allowed_frequencies`| `payment_frequency[]`| No| `DEFAULT '{MENSUAL}'` | Periodicidades admitidas |
| `amortization_method`| `amortization_method`| No| `DEFAULT 'FRANCES'` | Sistema de amortización |
| `rate_type` | `rate_type` | No | `DEFAULT 'EFECTIVA'` | Tipo de tasa |
| `annual_rate` | `NUMERIC(12,6)`| No | `CHECK (annual_rate > 0)` | Tasa Efectiva Anual (TEA en decimal) |
| `opening_fee_rate`| `NUMERIC(12,6)`| No| `DEFAULT 0` | Comisión de apertura porcentual |
| `opening_fee_fixed`| `NUMERIC(18,4)`| No| `DEFAULT 0` | Comisión fija de apertura |
| `opening_fee_mode`| `commission_mode`| No| `DEFAULT 'DEDUCTIVA'` | Descontada o financiada |
| `periodic_fee`| `NUMERIC(18,4)`| No | `DEFAULT 0` | Gasto administrativo o seguro por cuota |
| `grace_days` | `SMALLINT` | No | `DEFAULT 0` | Días de tolerancia antes de computar mora |
| `mora_rate_per_day`| `NUMERIC(12,6)`| No| `DEFAULT 0` | Tasa diaria moratoria |
| `mora_base` | `mora_base_type`| No | `DEFAULT 'SALDO_CAPITAL_VENCIDO'` | Base de cálculo de penalidad |
| `allows_anatocismo`| `BOOLEAN` | No | `DEFAULT FALSE` | Capitalización de mora (anatocismo) |
| `prepayment_penalty_rate`| `NUMERIC(12,6)`| No| `DEFAULT 0` | Penalidad porcentual por cancelación anticipada |
| `min_age` | `SMALLINT` | No | `DEFAULT 18` | Edad mínima requerida |
| `max_age` | `SMALLINT` | No | `DEFAULT 70` | Edad máxima admisible |
| `min_monthly_income`| `NUMERIC(18,4)`| No| `DEFAULT 0` | Ingreso mensual mínimo |
| `holiday_shift_direction`| `VARCHAR(4)`| No| `DEFAULT 'NEXT' CHECK (IN ('NEXT','PREV'))`| Desplazamiento por día inhábil |
| `status` | `product_status`| No| `DEFAULT 'ACTIVO'` | Estado de oferta |
| `created_by` | `UUID` | Sí | `REFERENCES users(id)` | Creador |
| `created_at` | `TIMESTAMPTZ` | No | `DEFAULT NOW()` | Fecha de alta |
| `updated_at` | `TIMESTAMPTZ` | No | `DEFAULT NOW()` | Modificación |
*Restricciones Check:* `max_amount >= min_amount`, `max_term >= min_term`.

#### `loan_applications`
Solicitudes formales de crédito con cálculo de ratio DTI y dictamen (US-10, US-12).
| Columna | Tipo | Nulo | Restricción / Default | Descripción |
|---|---|:---:|---|---|
| `id` | `UUID` | No | `PRIMARY KEY DEFAULT gen_random_uuid()` | Identificador |
| `customer_id` | `UUID` | No | `REFERENCES customers(id)` | Solicitante |
| `product_id` | `UUID` | No | `REFERENCES loan_products(id)` | Producto postulado |
| `requested_amount`| `NUMERIC(18,4)`| No | `CHECK (requested_amount > 0)` | Importe solicitado |
| `approved_amount` | `NUMERIC(18,4)`| Sí | | Importe aprobado por el comité |
| `term` | `SMALLINT` | No | `CHECK (term >= 1)` | Número de cuotas |
| `frequency` | `payment_frequency`| No| | Frecuencia de pago elegida |
| `destination` | `loan_purpose_type`| No| `DEFAULT 'CONSUMO'` | Destino del préstamo |
| `status` | `application_status`| No| `DEFAULT 'SOLICITADO'` | Estado de evaluación |
| `dti_ratio` | `NUMERIC(8,4)` | Sí | | Ratio Cuota-Ingreso (Debt-to-Income %) |
| `score` | `NUMERIC(8,4)` | Sí | | Puntuación crediticia |
| `system_recommendation`| `credit_decision`| Sí| | Recomendación del motor de scoring |
| `decision` | `VARCHAR(30)` | Sí | | Veredicto del analista |
| `decision_notes`| `TEXT` | Sí | | Justificación técnica obligatoria |
| `decision_by` | `UUID` | Sí | `REFERENCES users(id)` | Analista dictaminador |
| `decided_at` | `TIMESTAMPTZ` | Sí | | Fecha del dictamen |
| `originated_from`| `VARCHAR(20)` | No | `DEFAULT 'BACKOFFICE'` | Canal (BACKOFFICE o PORTAL) |
| `created_by` | `UUID` | Sí | `REFERENCES users(id)` | Usuario gestor |
| `created_at` | `TIMESTAMPTZ` | No | `DEFAULT NOW()` | Creación |
| `updated_at` | `TIMESTAMPTZ` | No | `DEFAULT NOW()` | Modificación |

---

### Módulo 5: Préstamos, Cronogramas y Cuotas (Core Transaccional)

#### `loans`
Créditos formalizados y desembolsados con condiciones financieras congeladas (US-13, US-31).
| Columna | Tipo | Nulo | Restricción / Default | Descripción |
|---|---|:---:|---|---|
| `id` | `UUID` | No | `PRIMARY KEY DEFAULT gen_random_uuid()` | Identificador |
| `application_id`| `UUID` | Sí | `UNIQUE REFERENCES loan_applications(id)`| Solicitud generadora |
| `customer_id` | `UUID` | No | `REFERENCES customers(id)` | Deudor titular |
| `product_id` | `UUID` | No | `REFERENCES loan_products(id)` | Producto base |
| `principal` | `NUMERIC(18,4)`| No | `CHECK (principal > 0)` | Capital contractual otorgado |
| `disbursed_amount`| `NUMERIC(18,4)`| No| | Importe neto entregado |
| `annual_rate` | `NUMERIC(12,6)`| No | | TEA congelada al desembolsar |
| `amortization_method`| `amortization_method`| No| | Método pactado |
| `frequency` | `payment_frequency`| No| | Frecuencia de pago |
| `term` | `SMALLINT` | No | | Número de cuotas |
| `grace_days` | `SMALLINT` | No | `DEFAULT 0` | Días de gracia congelados |
| `mora_rate_per_day`| `NUMERIC(12,6)`| No| `DEFAULT 0` | Tasa diaria moratoria pactada |
| `mora_base` | `mora_base_type`| No | `DEFAULT 'SALDO_CAPITAL_VENCIDO'`| Base de cálculo de mora |
| `opening_fee_amount`| `NUMERIC(18,4)`| No| `DEFAULT 0` | Importe de comisión de apertura cobrado |
| `periodic_fee`| `NUMERIC(18,4)`| No | `DEFAULT 0` | Cargo administrativo periódico |
| `prepayment_penalty_rate`| `NUMERIC(12,6)`| No| `DEFAULT 0` | Tasa de penalidad por prepago |
| `disbursement_date`| `DATE` | No | | Fecha de entrega de fondos |
| `first_due_date`| `DATE` | No | | Fecha de vencimiento de la cuota 1 |
| `total_scheduled_interest`| `NUMERIC(18,4)`| No| `DEFAULT 0` | Interés proyectado total congelado |
| `total_scheduled_fees`| `NUMERIC(18,4)`| No| `DEFAULT 0` | Comisiones proyectadas totales |
| `total_paid_principal`| `NUMERIC(18,4)`| No| `DEFAULT 0` | Capital amortizado a la fecha |
| `total_paid_interest`| `NUMERIC(18,4)`| No| `DEFAULT 0` | Interés amortizado a la fecha |
| `total_paid_fees`| `NUMERIC(18,4)`| No | `DEFAULT 0` | Comisiones amortizadas |
| `total_paid_mora`| `NUMERIC(18,4)`| No | `DEFAULT 0` | Mora cancelada acumulada |
| `status` | `loan_status` | No | `DEFAULT 'VIGENTE'` | Estado del crédito |
| `close_reason`| `loan_close_reason`| Sí | | Motivo de extinción de la deuda |
| `closed_at` | `TIMESTAMPTZ` | Sí | | Fecha de cierre definitivo |
| `purpose_type`| `loan_purpose_type`| No| `DEFAULT 'CONSUMO'` | Destino de la operación |
| `refinanced_from_loan_id`| `UUID` | Sí | `REFERENCES loans(id)` | Préstamo anterior liquidado por reestructuración |
| `cashbox_id` | `UUID` | Sí | `REFERENCES cash_boxes(id)` | Caja física donde se entregó efectivo |
| `disbursed_by`| `UUID` | Sí | `REFERENCES users(id)` | Cajero que ejecutó el desembolso |
| `idempotency_key`| `VARCHAR(100)`| Sí | `UNIQUE` | Clave UUID de protección de reintentos |
| `created_at` | `TIMESTAMPTZ` | No | `DEFAULT NOW()` | Creación |
| `updated_at` | `TIMESTAMPTZ` | No | `DEFAULT NOW()` | Modificación |

#### `schedules`
Versiones inmutables de los cronogramas de amortización (US-14, US-31).
| Columna | Tipo | Nulo | Restricción / Default | Descripción |
|---|---|:---:|---|---|
| `id` | `UUID` | No | `PRIMARY KEY DEFAULT gen_random_uuid()` | Identificador |
| `loan_id` | `UUID` | No | `REFERENCES loans(id)` | Préstamo asociado |
| `snapshot_version`| `SMALLINT` | No | `DEFAULT 1` | Versión (1=original, 2+=reestructurado) |
| `generated_at`| `TIMESTAMPTZ` | No | `DEFAULT NOW()` | Momento del cálculo y congelamiento |
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
| `installment_number`| `SMALLINT`| No | | Número de cuota correlativo (1..N) |
| `due_date` | `DATE` | No | | Fecha de vencimiento pactada |
| `principal` | `NUMERIC(18,4)`| No | `CHECK (principal >= 0)` | Capital nominal proyectado (INMUTABLE) |
| `interest` | `NUMERIC(18,4)`| No | `CHECK (interest >= 0)` | Interés nominal proyectado (INMUTABLE) |
| `fee` | `NUMERIC(18,4)`| No | `DEFAULT 0` | Cargo fijo proyectado (INMUTABLE) |
| `total_due` | `NUMERIC(18,4)`| No | | Importe de cuota proyectado (`principal + interest + fee`) |
| `paid_principal`| `NUMERIC(18,4)`| No | `DEFAULT 0 CHECK (paid_principal >= 0)` | Capital cancelado acumulado |
| `paid_interest` | `NUMERIC(18,4)`| No | `DEFAULT 0 CHECK (paid_interest >= 0)` | Interés cancelado acumulado |
| `paid_fee` | `NUMERIC(18,4)`| No | `DEFAULT 0 CHECK (paid_fee >= 0)` | Comisiones canceladas acumuladas |
| `late_fee` | `NUMERIC(18,4)`| No | `DEFAULT 0 CHECK (late_fee >= 0)` | Mora monetaria liquidada por el job nocturno |
| `paid_late_fee`| `NUMERIC(18,4)`| No | `DEFAULT 0 CHECK (paid_late_fee >= 0)` | Mora pagada acumulada |
| `status` | `installment_status`| No| `DEFAULT 'PENDING'` | Estado de exigibilidad |
| `days_late` | `SMALLINT` | No | `DEFAULT 0` | Días de atraso calculados |
| `last_mora_calc_date`| `DATE` | Sí | | Fecha del último cálculo de mora |
*Restricción:* `UNIQUE (loan_id, installment_number)`.

#### `prepayment_quotes`
Cotizaciones de liquidación anticipada con exoneración de intereses no devengados (US-30).
| Columna | Tipo | Nulo | Restricción / Default | Descripción |
|---|---|:---:|---|---|
| `id` | `UUID` | No | `PRIMARY KEY DEFAULT gen_random_uuid()` | Identificador |
| `loan_id` | `UUID` | No | `REFERENCES loans(id)` | Préstamo a cancelar |
| `as_of_date` | `DATE` | No | | Fecha de proyección del corte |
| `outstanding_principal`| `NUMERIC(18,4)`| No| | Capital insoluto restante |
| `accrued_interest`| `NUMERIC(18,4)`| No| `DEFAULT 0` | Interés devengado al día del corte |
| `accrued_mora`| `NUMERIC(18,4)`| No | `DEFAULT 0` | Mora acumulada al día |
| `prepayment_penalty`| `NUMERIC(18,4)`| No| `DEFAULT 0` | Recargo por prepago pactado |
| `total_to_pay`| `NUMERIC(18,4)`| No | | Importe cancelatorio líquido |
| `quote_valid_until`| `DATE` | No | | Fecha límite de vigencia de la cotización |
| `is_applied` | `BOOLEAN` | No | `DEFAULT FALSE` | Si la cotización fue pagada |
| `generated_by`| `UUID` | Sí | `REFERENCES users(id)` | Operador que emitió la cotización |
| `created_at` | `TIMESTAMPTZ` | No | `DEFAULT NOW()` | Fecha de generación |

---

### Módulo 6: Pagos, Prelación y Libro Mayor

#### `payments`
Registro inmutable de recaudación y cobros (US-16, US-18).
| Columna | Tipo | Nulo | Restricción / Default | Descripción |
|---|---|:---:|---|---|
| `id` | `UUID` | No | `PRIMARY KEY DEFAULT gen_random_uuid()` | Identificador |
| `loan_id` | `UUID` | No | `REFERENCES loans(id)` | Préstamo acreditado |
| `payment_kind`| `payment_kind` | No | `DEFAULT 'NORMAL'` | NORMAL o REVERSAL (contra-asiento) |
| `amount` | `NUMERIC(18,4)`| No | | Monto monetario del abono |
| `payment_method_id`| `UUID` | Sí | `REFERENCES payment_methods(id)`| Método de pago |
| `reference` | `VARCHAR(150)`| Sí | | Número de operación bancaria o voucher |
| `paid_at` | `TIMESTAMPTZ` | No | | Fecha/hora efectiva del cobro |
| `status` | `payment_status`| No | `DEFAULT 'APPLIED'` | Estado |
| `reversal_of_id`| `UUID` | Sí | `REFERENCES payments(id)` | Pago original revertido |
| `reversal_reason`| `TEXT` | Sí | | Causa de anulación obligatoria |
| `receipt_number`| `VARCHAR(30)` | Sí | `UNIQUE` | Número correlativo único de recibo |
| `cashbox_id` | `UUID` | Sí | `REFERENCES cash_boxes(id)` | Caja física donde ingresó |
| `registered_by`| `UUID` | Sí | `REFERENCES users(id)` | Cajero o proceso receptor |
| `idempotency_key`| `VARCHAR(100)`| Sí | `UNIQUE` | Clave de idempotencia del abono |
| `created_at` | `TIMESTAMPTZ` | No | `DEFAULT NOW()` | Timestamp de registro en base de datos |

#### `payment_allocations`
Desglose de la cascada legal de prelación por cuota intervenida (US-16, RF-16.4).
| Columna | Tipo | Nulo | Restricción / Default | Descripción |
|---|---|:---:|---|---|
| `id` | `UUID` | No | `PRIMARY KEY DEFAULT gen_random_uuid()` | Identificador |
| `payment_id` | `UUID` | No | `REFERENCES payments(id)` | Pago matriz |
| `installment_id`| `UUID` | No | `REFERENCES installments(id)` | Cuota amortizada |
| `to_mora` | `NUMERIC(18,4)`| No | `DEFAULT 0 CHECK (to_mora >= 0)` | Amortización a mora (Prelación 1°) |
| `to_interest` | `NUMERIC(18,4)`| No | `DEFAULT 0 CHECK (to_interest >= 0)` | Amortización a interés (Prelación 2°) |
| `to_fee` | `NUMERIC(18,4)`| No | `DEFAULT 0 CHECK (to_fee >= 0)` | Amortización a comisiones (Prelación 3°) |
| `to_principal`| `NUMERIC(18,4)`| No | `DEFAULT 0 CHECK (to_principal >= 0)` | Amortización a capital (Prelación 4°) |
| `created_at` | `TIMESTAMPTZ` | No | `DEFAULT NOW()` | Fecha de imputación |

#### `transactions`
Libro Mayor Financiero (Ledger) inmutable para contabilidad y conciliación (US-13, US-16).
| Columna | Tipo | Nulo | Restricción / Default | Descripción |
|---|---|:---:|---|---|
| `id` | `BIGSERIAL` | No | `PRIMARY KEY` | Secuencia correlativa inmutable |
| `loan_id` | `UUID` | Sí | `REFERENCES loans(id)` | Préstamo vinculado |
| `payment_id` | `UUID` | Sí | `REFERENCES payments(id)` | Pago vinculado |
| `cashbox_id` | `UUID` | Sí | `REFERENCES cash_boxes(id)` | Caja intervenida |
| `transaction_type`| `transaction_type`| No| | Concepto del movimiento |
| `direction` | `transaction_dir` | No | | `INFLOW` (ingreso) o `OUTFLOW` (egreso) |
| `amount` | `NUMERIC(18,4)`| No | `CHECK (amount > 0)` | Importe monetario positivo |
| `currency_id` | `UUID` | No | `REFERENCES currencies(id)` | Moneda |
| `reference` | `VARCHAR(150)`| Sí | | Documento o comprobante |
| `notes` | `TEXT` | Sí | | Glosa o detalle contable |
| `created_by` | `UUID` | Sí | `REFERENCES users(id)` | Usuario responsable |
| `created_at` | `TIMESTAMPTZ` | No | `DEFAULT NOW()` | Registro cronológico |

---

### Módulo 7: Caja y Tesorería

#### `cash_boxes`
Cajas físicas de ventanilla asignadas a sucursales (US-23).
| Columna | Tipo | Nulo | Restricción / Default | Descripción |
|---|---|:---:|---|---|
| `id` | `UUID` | No | `PRIMARY KEY DEFAULT gen_random_uuid()` | Identificador |
| `name` | `VARCHAR(100)`| No | | Denominación (ej. Ventanilla 1) |
| `branch_id` | `UUID` | No | `REFERENCES branches(id)` | Sucursal física |
| `currency_id` | `UUID` | No | `REFERENCES currencies(id)` | Divisa administrada |
| `responsible_user_id`| `UUID` | Sí | `REFERENCES users(id)` | Cajero titular |
| `current_balance`| `NUMERIC(18,4)`| No| `DEFAULT 0` | Saldo disponible en tiempo real |
| `status` | `cashbox_status`| No | `DEFAULT 'CERRADA'` | Estado (ABIERTA / CERRADA) |
| `created_at` | `TIMESTAMPTZ` | No | `DEFAULT NOW()` | Alta |
| `updated_at` | `TIMESTAMPTZ` | No | `DEFAULT NOW()` | Modificación |

#### `cash_sessions`
Sesiones de apertura y cierre con arqueo ciego de ventanilla (US-23).
| Columna | Tipo | Nulo | Restricción / Default | Descripción |
|---|---|:---:|---|---|
| `id` | `UUID` | No | `PRIMARY KEY DEFAULT gen_random_uuid()` | Identificador |
| `cashbox_id` | `UUID` | No | `REFERENCES cash_boxes(id)` | Caja operada |
| `opened_by` | `UUID` | No | `REFERENCES users(id)` | Cajero de apertura |
| `opening_balance`| `NUMERIC(18,4)`| No| `DEFAULT 0` | Fondo inicial de caja |
| `opened_at` | `TIMESTAMPTZ` | No | `DEFAULT NOW()` | Hora de apertura |
| `closed_by` | `UUID` | Sí | `REFERENCES users(id)` | Supervisor o cajero de cierre |
| `closed_at` | `TIMESTAMPTZ` | Sí | | Hora de cierre |
| `counted_amount`| `NUMERIC(18,4)`| Sí | | Dinero físico contado por el cajero |
| `expected_amount`| `NUMERIC(18,4)`| Sí | | Saldo teórico calculado por el sistema |
| `difference` | `NUMERIC(18,4)`| Sí | | Discrepancia (`counted - expected`) |
| `arqueo_result`| `arqueo_result`| Sí | | `CUADRADO`, `SOBRANTE` o `FALTANTE` |
| `arqueo_detail`| `JSONB` | Sí | | Desglose por denominación de billetes y monedas |
| `report_url` | `TEXT` | Sí | | Acta de arqueo firmada en PDF |

#### `cash_movements`
Entradas y salidas de dinero extraordinarias en ventanilla (gastos operativos / aportes) (US-24).
| Columna | Tipo | Nulo | Restricción / Default | Descripción |
|---|---|:---:|---|---|
| `id` | `UUID` | No | `PRIMARY KEY DEFAULT gen_random_uuid()` | Identificador |
| `cashbox_id` | `UUID` | No | `REFERENCES cash_boxes(id)` | Caja intervenida |
| `session_id` | `UUID` | Sí | `REFERENCES cash_sessions(id)` | Sesión activa |
| `movement_type`| `cash_movement_type`| No| | `INGRESO_EXTRAORDINARIO` o `EGRESO_GASTO` |
| `concept` | `VARCHAR(200)`| No | | Glosa o descripción del gasto/ingreso |
| `amount` | `NUMERIC(18,4)`| No | `CHECK (amount > 0)` | Importe positivo |
| `voucher_number`| `VARCHAR(60)`| Sí | | Factura o boleta de sustento |
| `created_by` | `UUID` | No | `REFERENCES users(id)` | Operador responsable |
| `created_at` | `TIMESTAMPTZ` | No | `DEFAULT NOW()` | Fecha y hora |

#### `cash_transfers`
Remesas y transferencias en dos fases entre ventanillas y bóvedas (US-25).
| Columna | Tipo | Nulo | Restricción / Default | Descripción |
|---|---|:---:|---|---|
| `id` | `UUID` | No | `PRIMARY KEY DEFAULT gen_random_uuid()` | Identificador |
| `source_cashbox_id`| `UUID` | No | `REFERENCES cash_boxes(id)` | Caja emisora |
| `destination_cashbox_id`| `UUID` | No | `REFERENCES cash_boxes(id)` | Caja receptora |
| `amount` | `NUMERIC(18,4)`| No | `CHECK (amount > 0)` | Importe transferido |
| `notes` | `TEXT` | Sí | | Motivo del traslado de fondos |
| `status` | `transfer_status`| No| `DEFAULT 'EN_TRANSITO'` | `EN_TRANSITO`, `COMPLETED`, `CANCELLED` |
| `initiated_by`| `UUID` | No | `REFERENCES users(id)` | Cajero emisor |
| `initiated_at`| `TIMESTAMPTZ` | No | `DEFAULT NOW()` | Despacho de la remesa |
| `accepted_by` | `UUID` | Sí | `REFERENCES users(id)` | Cajero que acepta la recepción física |
| `accepted_at` | `TIMESTAMPTZ` | Sí | | Recepción y conformidad |
*Restricción Check:* `source_cashbox_id <> destination_cashbox_id`.

#### `bank_accounts`
Cuentas bancarias de la entidad para conciliación financiera (US-37).
| Columna | Tipo | Nulo | Restricción / Default | Descripción |
|---|---|:---:|---|---|
| `id` | `UUID` | No | `PRIMARY KEY DEFAULT gen_random_uuid()` | Identificador |
| `bank_name` | `VARCHAR(100)`| No | | Banco (BCP, BBVA, Interbank, etc.) |
| `account_number_masked`| `VARCHAR(30)`| No| | Número de cuenta con máscara de seguridad |
| `currency_id` | `UUID` | No | `REFERENCES currencies(id)` | Moneda de la cuenta |
| `is_active` | `BOOLEAN` | No | `DEFAULT TRUE` | Estado |
| `created_at` | `TIMESTAMPTZ` | No | `DEFAULT NOW()` | Registro |

---

### Módulo 8: Mora, Cobranzas y Notificaciones

#### `collection_actions`
Bitácora de contactos, visitas domiciliarias y promesas de pago (US-21).
| Columna | Tipo | Nulo | Restricción / Default | Descripción |
|---|---|:---:|---|---|
| `id` | `UUID` | No | `PRIMARY KEY DEFAULT gen_random_uuid()` | Identificador |
| `loan_id` | `UUID` | No | `REFERENCES loans(id)` | Crédito en gestión |
| `installment_id`| `UUID` | Sí | `REFERENCES installments(id)` | Cuota específica reclamada |
| `action_type` | `collection_action_type`| No| | Tipo de gestión (LLAMADA, VISITA, etc.) |
| `note` | `TEXT` | Sí | | Argumentos y respuestas del deudor |
| `status` | `collection_action_status`| No| `DEFAULT 'ACTIVA'` | `ACTIVA`, `CUMPLIDA`, `PROMESA_INCUMPLIDA` |
| `next_action_at`| `TIMESTAMPTZ`| Sí | | Fecha del siguiente contacto agendado |
| `promise_amount`| `NUMERIC(18,4)`| Sí | | Importe comprometido a abonar |
| `promise_date`| `DATE` | Sí | | Fecha límite comprometida para el pago |
| `created_by` | `UUID` | No | `REFERENCES users(id)` | Gestor de cobranzas |
| `created_at` | `TIMESTAMPTZ` | No | `DEFAULT NOW()` | Fecha de gestión |
| `updated_at` | `TIMESTAMPTZ` | No | `DEFAULT NOW()` | Actualización |

#### `notifications`
Cola de mensajería saliente omnicanal al cliente (US-19, US-21).
| Columna | Tipo | Nulo | Restricción / Default | Descripción |
|---|---|:---:|---|---|
| `id` | `UUID` | No | `PRIMARY KEY DEFAULT gen_random_uuid()` | Identificador |
| `customer_id` | `UUID` | No | `REFERENCES customers(id)` | Destinatario |
| `channel` | `notification_channel`| No | | Canal (EMAIL, SMS, WHATSAPP, PUSH) |
| `template_code`| `VARCHAR(80)` | No | | Código de plantilla de mensaje |
| `payload` | `JSONB` | Sí | | Diccionario de variables dinámicas |
| `status` | `notification_status`| No | `DEFAULT 'PENDING'` | `PENDING`, `SENT`, `FAILED` |
| `sent_at` | `TIMESTAMPTZ` | Sí | | Momento de despacho confirmado |
| `error_detail`| `TEXT` | Sí | | Causa de fallo técnico |
| `created_at` | `TIMESTAMPTZ` | No | `DEFAULT NOW()` | Encolamiento |

---

### Módulo 9: Integraciones Externas y Conciliación

#### `integration_events`
Bandeja de recepción de notificaciones webhook de bancos y pasarelas de pago (US-36, US-38).
| Columna | Tipo | Nulo | Restricción / Default | Descripción |
|---|---|:---:|---|---|
| `id` | `UUID` | No | `PRIMARY KEY DEFAULT gen_random_uuid()` | Identificador |
| `provider` | `VARCHAR(50)` | No | | Proveedor (bcp, bbva, yape, plin, niubiz) |
| `event_type` | `VARCHAR(80)` | No | | Nombre del evento bancario |
| `external_id`| `VARCHAR(150)`| No | | Código de operación del banco |
| `payload` | `JSONB` | No | | Cuerpo crudo enviado por la entidad bancaria |
| `status` | `integration_event_status`| No| `DEFAULT 'RECEIVED'` | `RECEIVED`, `PROCESSED`, `DEAD_LETTER` |
| `error_detail`| `TEXT` | Sí | | Registro del error |
| `retry_count` | `SMALLINT` | No | `DEFAULT 0` | Número de reintentos ejecutados (0..4) |
| `next_retry_at`| `TIMESTAMPTZ`| Sí | | Próximo reintento con backoff exponencial |
| `processed_at`| `TIMESTAMPTZ`| Sí | | Momento de procesamiento exitoso |
| `created_at` | `TIMESTAMPTZ` | No | `DEFAULT NOW()` | Momento de recepción del webhook |
*Restricción:* `UNIQUE (provider, external_id)`.

#### `reconciliations`
Conciliación entre extractos bancarios y cobros asentados en el sistema (US-37).
| Columna | Tipo | Nulo | Restricción / Default | Descripción |
|---|---|:---:|---|---|
| `id` | `UUID` | No | `PRIMARY KEY DEFAULT gen_random_uuid()` | Identificador |
| `bank_account_id`| `UUID` | Sí | `REFERENCES bank_accounts(id)` | Cuenta bancaria |
| `source` | `VARCHAR(50)` | No | | Origen (BCP_EXTRACTO, YAPE_FILE, etc.) |
| `external_reference`| `VARCHAR(150)`| Sí | | Identificador del movimiento bancario |
| `bank_date` | `DATE` | Sí | | Fecha valor bancaria |
| `bank_description`| `TEXT` | Sí | | Glosa del extracto |
| `bank_amount`| `NUMERIC(18,4)`| Sí | | Monto monetario en el banco |
| `loan_id` | `UUID` | Sí | `REFERENCES loans(id)` | Crédito emparejado |
| `payment_id` | `UUID` | Sí | `REFERENCES payments(id)` | Pago interno emparejado |
| `status` | `reconciliation_status`| No| `DEFAULT 'PENDIENTE_CONCILIACION'`| Estado de conciliación |
| `reconciled_by`| `UUID` | Sí | `REFERENCES users(id)` | Analista que autorizó la conciliación |
| `reconciled_at`| `TIMESTAMPTZ`| Sí | | Fecha de confirmación |
| `created_at` | `TIMESTAMPTZ` | No | `DEFAULT NOW()` | Creación |
