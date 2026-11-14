# Plan de implementación — Módulo Customers (clientes, KYC y cuentas)

> Documento de ejecución para el desarrollador. Desglosa la sección **2. Customers** de
> [`REQUERIMIENTOS_BACKEND.md`](REQUERIMIENTOS_BACKEND.md) (BE-CUS-01 … BE-CUS-11) en tareas concretas:
> migraciones, clases por capa, endpoints, reglas, códigos de error, pruebas y criterio de "terminado".
>
> Historias de origen: [`02-gestion-de-clientes-y-kyc.md`](02-gestion-de-clientes-y-kyc.md) (US-04, US-05, US-06) ·
> BD: DB-20 … DB-26, DB-42 de [`REQUERIMIENTOS_BDD.md`](REQUERIMIENTOS_BDD.md) ·
> Frontend que lo consume: FE-BO-07 … FE-BO-09, FE-CP-03 de [`REQUERIMIENTOS_FRONTEND.md`](REQUERIMIENTOS_FRONTEND.md).

---

## Índice

0. [Cómo usar este documento](#0-cómo-usar-este-documento)
1. [Alcance](#1-alcance)
2. [Estado actual del código](#2-estado-actual-del-código)
3. [Decisiones de diseño (cerradas)](#3-decisiones-de-diseño-cerradas)
4. [Orden de ejecución y dependencias](#4-orden-de-ejecución-y-dependencias)
5. [Fase A — Base de datos](#5-fase-a--base-de-datos)
6. [Fase B — Estructura del módulo](#6-fase-b--estructura-del-módulo)
7. [Ítems por requerimiento (BE-CUS-01 … BE-CUS-11)](#7-ítems-por-requerimiento)
8. [Catálogo de errores](#8-catálogo-de-errores)
9. [Resumen de endpoints y permisos](#9-resumen-de-endpoints-y-permisos)
10. [Eventos de integración](#10-eventos-de-integración)
11. [Configuración (`appsettings`)](#11-configuración-appsettings)
12. [Plan de pruebas](#12-plan-de-pruebas)
13. [Definition of Done](#13-definition-of-done)
14. [Riesgos y preguntas abiertas](#14-riesgos-y-preguntas-abiertas)

---

## 0. Cómo usar este documento

- Cada ítem tiene un ID `CUS-XX.Y`. **Una rama / PR por requerimiento** (`feat/BE-CUS-01`, …), igual que se hizo con `feat/BE-SEC-05`.
- Las casillas `- [ ]` son la lista de trabajo; marcarlas en el PR.
- Cada requerimiento termina con **Criterios de aceptación** (Dado / Cuando / Entonces). El PR no se aprueba si alguno no tiene prueba automatizada.
- Si algo de este plan choca con el código existente, gana el patrón del código existente y se actualiza este documento en el mismo PR.

**Reglas transversales que aplican a todo el módulo** (no se repiten en cada ítem):

| Regla | Detalle |
|---|---|
| Montos | `decimal` / `Money`, redondeo `MidpointRounding.AwayFromZero` a 2 decimales. Nunca `float`/`double` (lo verifica `FloatingPointIlScanner`). |
| Fechas | Se guardan en UTC (`TIMESTAMPTZ`); fechas de negocio con `IClock.LimaToday`. Prohibido `DateTime.Now`. |
| Errores | Casos de negocio devuelven `Result`/`Result<T>` con `Error.*`; el endpoint usa `.ToHttpResult()` → `ProblemDetails`. Sin excepciones para flujo normal. |
| Auditoría | El `DbContext` ya usa `.UseAuditLog(sp)`; cada caso de uso que escribe llama `_audit.SetAction("<ACCION>", "<tabla>")`. |
| Visibilidad | Clases de dominio, aplicación y adaptadores `internal sealed`. Solo `Contracts/` y `Ports/Inbound/` son `public`. |
| Migraciones | SQL-First con DbUp en `database/migrations/` (nunca migraciones EF). Última usada: `18_approval_requests.sql`. |
| PII | Nunca loguear documento, CCI, teléfono ni correo completos (usar enmascarado en logs). |

---

## 1. Alcance

| ID | Requerimiento | Fase | En este plan |
|---|---|---|---|
| BE-CUS-01 | Alta, consulta, búsqueda y edición de clientes; unicidad documento (`409`); validación con regex del catálogo | MVP | ✅ |
| BE-CUS-02 | Historial auditado de cambios en ingresos, dirección y teléfono | MVP | ✅ |
| BE-CUS-03 | Documentos KYC en Object Storage (URI + SHA-256 en BD), URLs firmadas temporales | MVP | ✅ |
| BE-CUS-04 | Máquina de estados KYC con vencimiento | MVP | ✅ |
| BE-CUS-05 | Verificación automática (DNI, biometría, PEP/AML) vía `IIdentityVerifier` | MVP | ✅ (con adaptador *fake*; proveedor real en F2) |
| BE-CUS-06 | Bloqueo de desembolso si KYC ≠ `VERIFIED` (`422`) | MVP | ✅ (contrato público; Loans lo consume) |
| BE-CUS-07 | Consentimientos por finalidad y versión; revocar marketing | MVP | ✅ |
| BE-CUS-08 | Ficha 360 | MVP | ✅ (con lector de cartera; datos reales cuando existan Loans/Origination) |
| BE-CUS-09 | Cuentas del cliente (CCI / billetera), titularidad, una sola primaria | MVP | ✅ |
| BE-CUS-10 | Cambio de cuenta de desembolso con OTP + periodo de espera | MVP | ✅ |
| BE-CUS-11 | OTP con propósito, expiración, intentos y bloqueo | MVP | ✅ |
| — | Auto-registro desde el portal del cliente (US-26) | MVP | ❌ Otro plan (docs/09). Este módulo expone los casos de uso que el portal reutilizará. |
| — | Proveedor real RENIEC / biometría / listas (contratos comerciales) | F2 | ❌ Solo el puerto y un adaptador *fake* |
| — | Verificación de titularidad automática por "penny test" bancario | F2 | ❌ MVP = verificación manual por operador |
| — | Personas jurídicas: representantes legales y beneficiarios finales | F2 | ⚠️ Solo se registra la empresa con RUC |

---

## 2. Estado actual del código

**Existe:**

- `backend/src/Modules/Customers/LoanSystem.Modules.Customers.Core/Ports/Outbound/ICustomersUnitOfWork.cs` e `ICustomersOutbox.cs`.
- `backend/src/Modules/Customers/LoanSystem.Modules.Customers.Adapters/CustomersModule.cs` → registra `CustomersDbContext` con auditoría.
- `.../Adapters/Inbound/Http/CustomersEndpoints.cs` → solo `GET /api/customers/ping`.
- `.../Adapters/Outbound/Persistence/CustomersDbContext.cs` → sin entidades mapeadas.
- BD (`database/migrations/03_customers_kyc.sql`): tablas `customers`, `customer_bank_accounts`, `customer_documents`, `otp_verifications`; enums `person_type`, `document_type`, `kyc_status_type`, `bank_account_type`, `bank_account_status`; catálogo `document_types` (en `01_admin_config.sql`).
- Permiso `customers.read` en `RolePermissions` para Admin, Analyst, Operator, Collector y Auditor.

**Falta (lo que cubre este plan):** dominio, casos de uso, repositorios, endpoints, almacenamiento de archivos, verificación de identidad, consentimientos, OTP con propósito, periodo de espera de cuentas, job de vencimiento de KYC y pruebas.

**Patrón a replicar** (el mismo de Security):

```
Contracts/XxxContracts.cs (record público)
  → Ports/Inbound/IXxxUseCase : IUseCase<TReq, TRes>
  → Application/XxxUseCase (internal sealed, devuelve Result<T>)
  → Adapters/Inbound/Http/CustomersEndpoints: group.MapPost(...).ToHttpResult()
  → CustomersModule.AddCustomersModule: services.AddScoped<IXxxUseCase, XxxUseCase>()
```

Ejemplo de referencia: `Modules/Security/LoanSystem.Modules.Security.Core/Application/SuspendUserUseCase.cs`.

---

## 3. Decisiones de diseño (cerradas)

| # | Decisión | Motivo |
|---|---|---|
| D1 | Rutas bajo `/api/customers` (sin `/v1`), igual que el código actual y los requerimientos. | Consistencia con `/api/auth`, `/api/security`. |
| D2 | `GET /api/customers/{id}` devuelve la ficha básica; la **ficha 360** va en `GET /api/customers/{id}/overview`. | La 360 consulta otros módulos y es costosa; la ficha básica se usa en muchas pantallas. CA-06.1 se cumple contra `/overview`. |
| D3 | El número de documento **no se edita** tras el alta. Si está mal, se corrige con una operación de administrador auditada (fuera de MVP). | La unicidad y el KYC dependen de él. |
| D4 | El CCI / número de cuenta se guarda **cifrado** + **hash** (para unicidad) + **últimos 4** (para mostrar). La API **nunca** devuelve el número completo. | RNF-05. |
| D5 | Los archivos KYC van detrás de un puerto `IDocumentStorage`. MVP: adaptador de disco local cifrado para desarrollo y adaptador S3-compatible para entornos. URL firmada con TTL de **5 min**. | RNF-04; permite cambiar de proveedor sin tocar el núcleo. |
| D6 | La verificación automática es **asíncrona**: se encola un evento por outbox y un handler llama al proveedor. El analista ve los resultados cuando llegan. | Nunca llamar a un proveedor externo dentro de una transacción de BD (BE-NF-03). |
| D7 | El bloqueo de desembolso se expone como **contrato público** `ICustomerDisbursementEligibility` en `Customers.Core/Contracts`. Loans/Treasury lo llaman; Customers no conoce Loans. | Límites de módulo (BE-NF-08). |
| D8 | OTP: código de 6 dígitos, se guarda solo el hash (HMAC-SHA256 con pepper de configuración). | No reversible; igual criterio que refresh tokens. |
| D9 | El error por exceso de intentos de OTP usa `Error.Locked` → `423`, igual que el bloqueo de login (BE-SEC-02). | No existe `ErrorType` para `429`. |
| D10 | El historial de cambios (BE-CUS-02) se lee desde `audit_logs` (ya captura antes/después). No se crea tabla nueva. | Evita duplicar auditoría. |

---

## 4. Orden de ejecución y dependencias

```
Fase A  BD (migraciones 19–23) ─────────────┐
Fase B  Estructura del módulo + dominio base ┤
                                             ▼
Sprint 1:  CUS-01 Alta/consulta/edición ──► CUS-02 Historial ──► CUS-07 Consentimientos
Sprint 2:  CUS-11 OTP ──► CUS-03 Documentos ──► CUS-04 Estados KYC ──► CUS-05 Verificación automática
Sprint 3:  CUS-09 Cuentas ──► CUS-10 Cambio de cuenta (usa OTP) ──► CUS-06 Elegibilidad desembolso
Sprint 4:  CUS-08 Ficha 360 (cuando Loans/Origination expongan contratos de lectura)
```

| Requerimiento | Depende de | Estimación |
|---|---|---|
| Fase A + B | — | 2 d |
| CUS-01 | A, B | 3 d |
| CUS-02 | CUS-01 | 1 d |
| CUS-07 | CUS-01 | 1,5 d |
| CUS-11 | A | 2 d |
| CUS-03 | CUS-01 | 2,5 d |
| CUS-04 | CUS-03 | 2 d |
| CUS-05 | CUS-04 | 3 d |
| CUS-09 | CUS-01 | 2,5 d |
| CUS-10 | CUS-09, CUS-11 | 1,5 d |
| CUS-06 | CUS-04, CUS-09 | 1 d |
| CUS-08 | CUS-01 + contratos de Loans/Origination | 2 d |
| **Total** | | **≈ 24 d/dev** |

---

## 5. Fase A — Base de datos

Crear los scripts en `database/migrations/`. Cada uno es idempotente frente a DbUp (se ejecuta una vez) y lleva cabecera con dominio e IDs, igual que `03_customers_kyc.sql`.

### A1. `19_customers_kyc_extensions.sql` (DB-21, DB-23)

- [x] **A1.1** Columnas nuevas en `customers`:

```sql
CREATE TYPE aml_risk_level AS ENUM ('LOW', 'MEDIUM', 'HIGH');

ALTER TABLE customers
    ADD COLUMN is_pep              BOOLEAN        NOT NULL DEFAULT FALSE,
    ADD COLUMN aml_risk_level      aml_risk_level NOT NULL DEFAULT 'LOW',
    ADD COLUMN email_verified_at   TIMESTAMPTZ,
    ADD COLUMN phone_verified_at   TIMESTAMPTZ,
    ADD COLUMN kyc_rejected_reason VARCHAR(500),
    ADD COLUMN kyc_submitted_at    TIMESTAMPTZ,          -- cuándo el expediente quedó listo para revisión
    ADD COLUMN version             INTEGER        NOT NULL DEFAULT 0;  -- concurrencia optimista
```

- [x] **A1.2** Tabla de verificaciones automáticas:

```sql
CREATE TYPE kyc_check_type   AS ENUM ('IDENTITY_REGISTRY', 'BIOMETRIC_LIVENESS', 'FACE_MATCH', 'PEP_SCREENING', 'AML_SANCTIONS');
CREATE TYPE kyc_check_result AS ENUM ('PENDING', 'PASS', 'FAIL', 'REVIEW', 'ERROR');

CREATE TABLE kyc_verifications (
    id                  UUID              PRIMARY KEY DEFAULT gen_random_uuid(),
    customer_id         UUID              NOT NULL REFERENCES customers(id) ON DELETE CASCADE,
    check_type          kyc_check_type    NOT NULL,
    provider            VARCHAR(50)       NOT NULL,         -- 'FAKE', 'RENIEC', 'METAMAP', ...
    result              kyc_check_result  NOT NULL DEFAULT 'PENDING',
    score               NUMERIC(5,2),                       -- 0–100 cuando aplique
    external_reference  VARCHAR(100),
    raw_response        JSONB,                              -- respuesta cruda (sin imágenes)
    error_message       VARCHAR(500),
    requested_at        TIMESTAMPTZ       NOT NULL DEFAULT NOW(),
    completed_at        TIMESTAMPTZ,
    requested_by        UUID              REFERENCES users(id),
    UNIQUE (provider, external_reference)
);
CREATE INDEX ix_kyc_verifications_customer ON kyc_verifications (customer_id, check_type, requested_at DESC);
```

### A2. `20_customer_documents_v2.sql` (BE-CUS-03)

- [x] **A2.1**

```sql
CREATE TYPE customer_document_category AS ENUM (
    'DNI_FRONTAL', 'DNI_REVERSO', 'SELFIE', 'BOLETA_PAGO', 'RECIBO_SERVICIOS',
    'ESTADO_CUENTA_BANCO', 'FICHA_RUC', 'OTRO');

ALTER TABLE customer_documents
    ALTER COLUMN doc_category TYPE customer_document_category USING doc_category::customer_document_category,
    ADD COLUMN size_bytes    BIGINT      NOT NULL DEFAULT 0,
    ADD COLUMN original_name VARCHAR(255),
    ADD COLUMN is_current    BOOLEAN     NOT NULL DEFAULT TRUE,   -- FALSE cuando se reemplaza
    ADD COLUMN replaced_at   TIMESTAMPTZ;

-- Solo un documento vigente por categoría (excepto OTRO y BOLETA_PAGO, que admiten varios)
CREATE UNIQUE INDEX ux_customer_documents_current
    ON customer_documents (customer_id, doc_category)
    WHERE is_current AND doc_category NOT IN ('OTRO', 'BOLETA_PAGO');
```

> Los documentos **no se borran**: reemplazar = marcar el anterior `is_current = FALSE` + insertar uno nuevo.

### A3. `21_customer_consents.sql` (BE-CUS-07)

- [x] **A3.1**

```sql
CREATE TYPE consent_purpose AS ENUM ('DATA_PROCESSING', 'CREDIT_BUREAU_QUERY', 'MARKETING');

CREATE TABLE consent_texts (
    id          UUID            PRIMARY KEY DEFAULT gen_random_uuid(),
    purpose     consent_purpose NOT NULL,
    version     VARCHAR(20)     NOT NULL,       -- '2026-01', 'v3'...
    body        TEXT            NOT NULL,
    sha256      VARCHAR(64)     NOT NULL,
    is_current  BOOLEAN         NOT NULL DEFAULT FALSE,
    published_at TIMESTAMPTZ    NOT NULL DEFAULT NOW(),
    UNIQUE (purpose, version)
);
CREATE UNIQUE INDEX ux_consent_texts_current ON consent_texts (purpose) WHERE is_current;

CREATE TABLE customer_consents (
    id              UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    customer_id     UUID        NOT NULL REFERENCES customers(id) ON DELETE CASCADE,
    consent_text_id UUID        NOT NULL REFERENCES consent_texts(id),
    purpose         consent_purpose NOT NULL,
    granted_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    granted_ip      INET,
    user_agent      TEXT,
    channel         VARCHAR(20) NOT NULL,       -- 'BACKOFFICE' | 'PORTAL'
    revoked_at      TIMESTAMPTZ,
    revoked_ip      INET
);
CREATE UNIQUE INDEX ux_customer_consents_active
    ON customer_consents (customer_id, purpose) WHERE revoked_at IS NULL;
```

- [x] **A3.2** Seed (`database/seeds/`): un texto vigente `v1` por cada finalidad.
- [x] **A3.3** Marcar `customers.consent_given_at` y `consent_ip` como **deprecadas** (comentario SQL). No se borran.

### A4. `22_otp_purpose_and_attempts.sql` (BE-CUS-11)

- [x] **A4.1**

```sql
CREATE TYPE otp_purpose AS ENUM ('LOGIN', 'CONTACT_VERIFICATION', 'CONTRACT_SIGNATURE', 'ACCOUNT_CHANGE');

ALTER TABLE otp_verifications
    ADD COLUMN purpose        otp_purpose NOT NULL DEFAULT 'CONTACT_VERIFICATION',
    ADD COLUMN reference_id   UUID,                         -- p. ej. id de la cuenta a cambiar
    ADD COLUMN attempts       SMALLINT    NOT NULL DEFAULT 0,
    ADD COLUMN max_attempts   SMALLINT    NOT NULL DEFAULT 5,
    ADD COLUMN invalidated_at TIMESTAMPTZ,                  -- reemplazado por uno nuevo o bloqueado
    ADD COLUMN requester_ip   INET;

COMMENT ON COLUMN otp_verifications.verified_at IS
    'Momento en que el OTP se usó correctamente (un solo uso). NULL = no consumido.';

CREATE INDEX ix_otp_active ON otp_verifications (customer_id, purpose)
    WHERE verified_at IS NULL AND invalidated_at IS NULL;
```

> La columna existente `verified_at` se reutiliza como "consumido". En el código C# la propiedad se llama `ConsumedAt` y se mapea a `verified_at`.

### A5. `23_customer_bank_accounts_v2.sql` (BE-CUS-09, BE-CUS-10, DB-25, DB-26, DB-42)

- [x] **A5.1**

```sql
ALTER TABLE customer_bank_accounts
    ADD COLUMN bank_code               VARCHAR(3),              -- 3 primeros dígitos del CCI
    ADD COLUMN account_number_encrypted TEXT,                   -- CCI o número cifrado (D4)
    ADD COLUMN account_number_hash     VARCHAR(64),             -- HMAC para unicidad
    ADD COLUMN last4                   VARCHAR(4)  NOT NULL DEFAULT '',
    ADD COLUMN holder_name             VARCHAR(200),            -- nombre del titular declarado
    ADD COLUMN gateway_token           VARCHAR(200),            -- tarjetas: solo token de pasarela
    ADD COLUMN disbursement_enabled_from TIMESTAMPTZ,           -- fin del periodo de espera
    ADD COLUMN verification_method     VARCHAR(30),             -- 'MANUAL' | 'PENNY_TEST' (F2)
    ADD COLUMN verification_notes      VARCHAR(500),
    ADD COLUMN deactivated_at          TIMESTAMPTZ;

-- Una sola primaria activa por cliente
CREATE UNIQUE INDEX ux_customer_bank_accounts_primary
    ON customer_bank_accounts (customer_id) WHERE is_primary AND status = 'ACTIVE';

-- Una misma cuenta no puede registrarse dos veces para el mismo cliente
CREATE UNIQUE INDEX ux_customer_bank_accounts_number
    ON customer_bank_accounts (customer_id, account_number_hash) WHERE status <> 'INACTIVE';

-- DB-42: permite FK compuesta desde loans (customer_id, customer_bank_account_id)
ALTER TABLE customer_bank_accounts ADD CONSTRAINT uq_customer_bank_accounts_customer_id UNIQUE (customer_id, id);

ALTER TABLE customer_bank_accounts ADD CONSTRAINT ck_wallet_or_number CHECK (
    (account_type = 'BILLETERA_DIGITAL' AND phone_wallet IS NOT NULL)
 OR (account_type <> 'BILLETERA_DIGITAL' AND account_number_hash IS NOT NULL));
```

- [x] **A5.2** Las columnas `cci` y `account_number_masked` quedan deprecadas (comentario). Si hay datos de seed, migrarlos a las columnas nuevas en el mismo script.
- [ ] **A5.3** Ejecutar `run_all.sql` / `LoanSystem.DbMigrator` contra una BD limpia y contra una BD con los datos de `seeds/` — ambos deben pasar.

---

## 6. Fase B — Estructura del módulo

Crear carpetas y piezas base (sin lógica de negocio aún):

```
LoanSystem.Modules.Customers.Core/
├── Contracts/                 (public)  CustomerContracts.cs, KycContracts.cs, BankAccountContracts.cs,
│                                        ConsentContracts.cs, OtpContracts.cs, ICustomerDisbursementEligibility.cs,
│                                        IntegrationEvents/*.cs
├── Domain/                    (internal) Customer.cs, IdentityDocument.cs, PersonName.cs, ContactInfo.cs,
│                                        SocioeconomicProfile.cs, KycStatus.cs, CustomerDocument.cs,
│                                        KycVerification.cs, CustomerConsent.cs, CustomerBankAccount.cs,
│                                        OtpCode.cs, CustomersEnums.cs, Validators/*.cs
├── Application/               (internal) *UseCase.cs, CustomersOptions.cs, Handlers/*.cs
└── Ports/
    ├── Inbound/               (public)  I*UseCase.cs
    └── Outbound/              ICustomerRepository, IDocumentTypeCatalog, IDocumentStorage, IIdentityVerifier,
                               IOtpSender, IOtpHasher, IFieldProtector, ICustomerPortfolioReader,
                               IConsentTextRepository, ICustomersUnitOfWork (existe), ICustomersOutbox (existe)

LoanSystem.Modules.Customers.Adapters/
├── Inbound/Http/              CustomersEndpoints.cs, KycEndpoints.cs, BankAccountEndpoints.cs, ConsentEndpoints.cs, OtpEndpoints.cs
├── Inbound/Jobs/              ExpireKycJob.cs, CustomersJobs.cs
├── Inbound/Events/            RunKycChecksHandler.cs
└── Outbound/
    ├── Persistence/           CustomersDbContext.cs (editar), Configurations/*.cs, CustomerRepository.cs, ...
    ├── Storage/               LocalDocumentStorage.cs, S3DocumentStorage.cs
    ├── Identity/              FakeIdentityVerifier.cs
    ├── Otp/                   HmacOtpHasher.cs, OutboxOtpSender.cs
    └── Crypto/                AesGcmFieldProtector.cs
```

- [x] **B1** Crear `CustomersEnums.cs` con enums C# cuyos miembros sean **idénticos** a las etiquetas PG (`PENDING`, `VERIFIED`, `DNI_FRONTAL`…), mapeados con `NpgsqlNullNameTranslator` como en Security.
- [x] **B2** En `CustomersDbContext.OnModelCreating`: `ApplyConfigurationsFromAssembly(typeof(CustomersDbContext).Assembly)` y `MapEnum<...>()` por cada enum nuevo.
- [x] **B3** `CustomersOptions` (sección `Customers`, ver §11) con `ValidateOnStart()`.
- [x] **B4** Prueba de arquitectura: añadir a `LoanSystem.Architecture.Tests/ArchitectureTests.cs` que ningún otro módulo referencie `Customers.Core.Domain` ni `Customers.Adapters` (solo `Contracts` y `Ports.Inbound`).
- [x] **B5** Permisos nuevos en `Modules/Security/.../Domain/RolePermissions.cs`:

| Permiso | Admin | Analyst | Operator | Collector | Auditor | Client |
|---|:-:|:-:|:-:|:-:|:-:|:-:|
| `customers.read` (existe) | ✅ | ✅ | ✅ | ✅ | ✅ | — |
| `customers.write` | ✅ | ✅ | ✅ | — | — | — |
| `customers.kyc.review` | ✅ | ✅ | — | — | — | — |
| `customers.documents.read` | ✅ | ✅ | ✅ | — | ✅ | — |
| `customers.accounts.manage` | ✅ | — | ✅ | — | — | — |
| `customers.pii.unmasked` | ✅ | ✅ | ✅ | — | — | — |
| `client.portal` (existe) | — | — | — | — | — | ✅ |

> Los endpoints se protegen con políticas por rol (`SecurityPolicies`) como hoy; los permisos se devuelven en `GET /api/me` para que el frontend oculte acciones. Si se registra una política combinada (p. ej. `CustomersWrite` = Admin ∪ Analyst ∪ Operator), añadirla en `SecurityAuthenticationExtensions` con una prueba en `PolicyAuthorizationTests`.

---

## 7. Ítems por requerimiento

### BE-CUS-01 — Alta, consulta, búsqueda y edición de clientes

**Objetivo:** registrar personas naturales y jurídicas con documento único y válido, y consultarlas.

#### Dominio

- [x] **CUS-01.1** `IdentityDocument` (Value Object): `DocumentType Type`, `string Number`. Fábrica `Create(type, number, DocumentTypeRule rule)` → `Result<IdentityDocument>`:
  - Normaliza: `Trim()`, mayúsculas, sin espacios ni guiones.
  - Aplica `rule.CharLength` y `rule.ValidationRegex` del catálogo `document_types` (RF-04.1).
  - Validaciones fijas además del catálogo:

| Tipo | Regla | Ejemplo válido |
|---|---|---|
| `DNI` | `^\d{8}$` | `45879612` |
| `CE` | `^\d{9}$` (o el regex del catálogo) | `001234567` |
| `RUC` | `^(10\|15\|16\|17\|20)\d{9}$` + **dígito verificador módulo 11** (ver abajo) | `20100070970` |
| `PASAPORTE` | `^[A-Z0-9]{6,12}$` | `AB1234567` |

  Dígito verificador RUC: pesos `5,4,3,2,7,6,5,4,3,2` sobre los 10 primeros dígitos; `r = 11 - (suma % 11)`; si `r = 10` → `0`, si `r = 11` → `1`; debe ser igual al dígito 11.
- [x] **CUS-01.2** `Customer` (aggregate root, `Entity<Guid>`), propiedades con `private set` y fábricas:
  - `static Result<Customer> RegisterNatural(IdentityDocument doc, PersonName name, DateOnly birthDate, ContactInfo contact, SocioeconomicProfile profile, Guid createdBy, IClock clock)`.
  - `static Result<Customer> RegisterLegalEntity(IdentityDocument ruc, string businessName, ContactInfo contact, SocioeconomicProfile profile, Guid createdBy, IClock clock)`.
  - Estado inicial: `KycStatus = PENDING`, `AmlRiskLevel = LOW`, `IsPep = false`.
- [x] **CUS-01.3** Invariantes (cada una con prueba de dominio):
  - `NATURAL` → documento `DNI`, `CE` o `PASAPORTE`; `birthDate` obligatorio; edad ≥ 18 calculada con `clock.LimaToday`.
  - `JURIDICA` → documento `RUC` que empiece por `20`; `birthDate` nulo.
  - `FullName` 3–200 caracteres; se guarda en mayúsculas sin dobles espacios.
  - `Email` formato válido (≤150); `PhonePrimary` peruano `^9\d{8}$` (se admite prefijo `+51`, se guarda normalizado sin prefijo).
  - `MonthlyNetIncome` ≥ 0 (`Money`, PEN); `WorkSeniorityMonths` 0–600.
- [x] **CUS-01.4** Métodos de edición (no permiten cambiar documento ni tipo de persona — D3):
  - `UpdateContact(ContactInfo)` → si cambia el correo o el teléfono, limpia `EmailVerifiedAt` / `PhoneVerifiedAt`.
  - `UpdateAddress(string address, string district)`.
  - `UpdateSocioeconomicProfile(SocioeconomicProfile)`.
  - Los tres devuelven la lista de campos críticos modificados (se usa en CUS-02).

#### Aplicación

- [x] **CUS-01.5** `RegisterCustomerUseCase` (`IRegisterCustomerUseCase : IUseCase<RegisterCustomerRequest, CustomerResponse>`):
  1. Leer la regla del tipo desde `IDocumentTypeCatalog` (cachear 10 min). Si el tipo está inactivo → `400 Customers.DocumentTypeInactive`.
  2. Crear `IdentityDocument` → si falla `400 Customers.InvalidDocumentNumber`.
  3. `ICustomerRepository.ExistsByDocumentAsync(type, number)` → si existe `409 Customers.DocumentAlreadyExists` (no revelar el id existente).
  4. Exigir `consents` con al menos `DATA_PROCESSING` aceptado (ver CUS-07) → si no, `422 Customers.ConsentRequired`.
  5. Crear el agregado, guardar consentimientos, `_audit.SetAction("CREATE", "customers")`, `SaveChanges`.
  6. Capturar `DbUpdateException` por la `UNIQUE (doc_type, doc_number)` (carrera entre dos altas) y traducirla a `409`.
  7. Encolar `CustomerRegisteredIntegrationEvent` en el outbox.
- [x] **CUS-01.6** `GetCustomerUseCase` → `404 Customers.NotFound`. Si el usuario es `ROLE_CLIENT`, solo puede leer su propio `CustomerId` (`ICurrentUser.CustomerId`); si no coincide devolver `404` (no `403`, para no confirmar existencia).
- [x] **CUS-01.7** `SearchCustomersUseCase` → `PagedResult<CustomerListItem>`; filtros: `q` (documento exacto o nombre con `unaccent ILIKE`), `kycStatus`, `personType`, `createdFrom/To`; `page` ≥1, `pageSize` 1–100 (default 20); orden `created_at DESC`. Índice: `CREATE INDEX ix_customers_name_trgm ...` solo si hace falta (medir primero).
- [x] **CUS-01.8** `UpdateCustomerUseCase` (PATCH parcial: `contact`, `address`, `profile`), con concurrencia optimista por `version` → `409 Customers.ConcurrencyConflict` si cambió.
- [x] **CUS-01.9** Enmascarado en respuestas (RNF-05): si el usuario no tiene `customers.pii.unmasked`, `docNumber` → `****5678`, `email` → `j***@dominio.com`, teléfono → `*****4321`.

#### Endpoints

| Método | Ruta | Política | Request | Respuestas |
|---|---|---|---|---|
| `POST` | `/api/customers` | Admin, Analyst, Operator | `RegisterCustomerRequest` | `201` + `Location` · `400` · `409` · `422` |
| `GET` | `/api/customers/{id}` | `customers.read` o dueño (Client) | — | `200` · `404` |
| `GET` | `/api/customers` | `customers.read` | query | `200 PagedResult` |
| `PATCH` | `/api/customers/{id}` | Admin, Analyst, Operator | `UpdateCustomerRequest` + `If-Match: <version>` | `200` · `400` · `404` · `409` |

```csharp
public sealed record RegisterCustomerRequest(
    string PersonType,              // NATURAL | JURIDICA
    string DocType,                 // DNI | CE | RUC | PASAPORTE
    string DocNumber,
    string FullName,
    DateOnly? BirthDate,
    string Email,
    string PhonePrimary,
    string? PhoneSecondary,
    string Address,
    string District,
    decimal MonthlyNetIncome,
    string? Occupation,
    string? EmployerName,
    int WorkSeniorityMonths,
    IReadOnlyList<ConsentAcceptance> Consents);

public sealed record CustomerResponse(
    Guid Id, string PersonType, string DocType, string DocNumber, string FullName,
    DateOnly? BirthDate, string? Email, bool EmailVerified, string? PhonePrimary, bool PhoneVerified,
    string? PhoneSecondary, string? Address, string? District,
    decimal MonthlyNetIncome, string? Occupation, string? EmployerName, int WorkSeniorityMonths,
    string KycStatus, DateTimeOffset? KycExpiresAt, bool IsPep, string AmlRiskLevel,
    decimal CreditBalance, int Version, DateTimeOffset CreatedAt);
```

#### Criterios de aceptación

- **CA-01.a** Dado un DNI `45879612` inexistente, cuando `POST /api/customers` con datos válidos, entonces `201`, `kycStatus = PENDING` y cabecera `Location: /api/customers/{id}`.
- **CA-01.b** Dado un cliente con DNI `12345678`, cuando se registra otro con el mismo DNI, entonces `409` con `code = Customers.DocumentAlreadyExists`.
- **CA-01.c** Dado un RUC con dígito verificador incorrecto, entonces `400 Customers.InvalidDocumentNumber`.
- **CA-01.d** Dada una persona natural de 17 años, entonces `400 Customers.Underage`.
- **CA-01.e** Dadas dos peticiones simultáneas con el mismo documento, entonces una obtiene `201` y la otra `409` (nunca `500`).
- **CA-01.f** Un usuario `ROLE_COLLECTOR` ve el documento enmascarado; un `ROLE_ANALYST` lo ve completo.

---

### BE-CUS-02 — Historial auditado de datos críticos

- [x] **CUS-02.1** En `UpdateCustomerUseCase`, si cambian campos críticos (`monthly_net_income`, `address`, `district`, `phone_primary`, `phone_secondary`, `email`) usar `_audit.SetAction("UPDATE_CRITICAL", "customers")`; si no, `"UPDATE"`.
- [x] **CUS-02.2** Verificar que el `AuditLogInterceptor` registra `old_values` / `new_values` solo con las columnas modificadas (si registra la fila entera, está bien, pero la consulta debe calcular el diff).
- [x] **CUS-02.3** Endpoint `GET /api/customers/{id}/history?page=&pageSize=` (Admin, Analyst, Auditor). Implementación: puerto `ICustomerHistoryReader` en Customers que consulta `audit_logs` por `entity = 'customers' AND entity_id = {id}` (SQL de solo lectura con Dapper/`NpgsqlConnectionFactory`, como `AuditLogReader` de Security).
- [x] **CUS-02.4** Respuesta:

```csharp
public sealed record CustomerChangeEntry(
    DateTimeOffset ChangedAt, Guid? ChangedBy, string? ChangedByEmail, string Action,
    IReadOnlyList<FieldChange> Changes, string? Ip, string? UserAgent);
public sealed record FieldChange(string Field, string? OldValue, string? NewValue);
```

- **CA-02.a** Dado un cliente con ingreso 3 000, cuando se actualiza a 4 500, entonces `/history` devuelve una entrada `UPDATE_CRITICAL` con `monthlyNetIncome: 3000 → 4500`, usuario, IP y user-agent.
- **CA-02.b** Un cambio solo de `occupation` aparece como `UPDATE`.

---

### BE-CUS-03 — Documentos KYC en Object Storage

#### Puertos y adaptadores

- [x] **CUS-03.1** `IDocumentStorage`:

```csharp
public interface IDocumentStorage
{
    Task<StoredObject> PutAsync(string key, Stream content, string contentType, CancellationToken ct);
    Task<Uri> GetSignedReadUrlAsync(string key, TimeSpan ttl, CancellationToken ct);
    Task<bool> ExistsAsync(string key, CancellationToken ct);
}
public sealed record StoredObject(string StorageUri, string Sha256, long SizeBytes);
```

- [x] **CUS-03.2** `LocalDocumentStorage` (solo `Development`): guarda en `Customers:Storage:LocalPath`, firma la URL con HMAC + expiración y expone `GET /api/customers/files/{token}` que valida firma y expiración.
- [x] **CUS-03.3** `S3DocumentStorage` (S3 o compatible, p. ej. MinIO/R2): bucket privado, cifrado SSE, URL pre-firmada. Credenciales desde el gestor de secretos (nunca en `appsettings`; lo vigila `ConfigurationSecretsTests`).
- [x] **CUS-03.4** Clave del objeto: `customers/{customerId}/{category}/{documentId}.{ext}` (sin nombre original ni datos personales).

#### Caso de uso

- [x] **CUS-03.5** `UploadCustomerDocumentUseCase` — `POST /api/customers/{id}/documents` (`multipart/form-data`: `category`, `file`):
  1. Validar tamaño ≤ `MaxFileSizeMb` (default 10) → `400 Customers.FileTooLarge`.
  2. Validar tipo por **firma mágica del archivo**, no solo por extensión: JPEG, PNG, WEBP, PDF → `400 Customers.UnsupportedFileType`.
  3. Calcular SHA-256 en streaming mientras se sube (no cargar el archivo entero en memoria).
  4. Subir a storage **antes** de abrir la transacción; luego en transacción: marcar el vigente de esa categoría `is_current = false`, insertar el nuevo, `_audit.SetAction("UPLOAD", "customer_documents")`.
  5. Si la transacción falla, el objeto queda huérfano: registrarlo en log `Warning` (limpieza de huérfanos = tarea F2).
  6. Si el cliente estaba `REJECTED` o `EXPIRED`, **no** cambia de estado aquí (lo hace `SubmitKyc`, CUS-04).
- [x] **CUS-03.6** `ListCustomerDocumentsUseCase` — `GET /api/customers/{id}/documents?includeHistory=false`.
- [x] **CUS-03.7** `GetDocumentUrlUseCase` — `GET /api/customers/{id}/documents/{docId}/url` → `{ url, expiresAt }` con TTL 5 min. Cada emisión de URL se audita (`VIEW_DOCUMENT`) — quién vio el DNI de quién.
- [x] **CUS-03.8** Límite de petición en Kestrel para esa ruta (`RequestSizeLimit`) = `MaxFileSizeMb` + margen.

**Permisos:** subir → Admin, Analyst, Operator y Client (solo los suyos). Ver/URL → `customers.documents.read` o dueño.

- **CA-03.a** Subir un JPG de 2 MB como `DNI_FRONTAL` devuelve `201` con `sha256` y sin URL pública.
- **CA-03.b** Un `.exe` renombrado a `.jpg` devuelve `400 Customers.UnsupportedFileType`.
- **CA-03.c** La URL firmada deja de funcionar a los 5 min (`403` del storage / del endpoint local).
- **CA-03.d** Subir un segundo `DNI_FRONTAL` deja el primero con `is_current = false` y ambos visibles con `includeHistory=true`.

---

### BE-CUS-04 — Máquina de estados KYC

#### Transiciones permitidas

```
            submit                    approve
PENDING ───────────► PENDING* ──────────────────► VERIFIED ──(job vencimiento)──► EXPIRED
   ▲                    │ reject                       │                              │
   │                    ▼                              │ reject (fraude detectado)    │
   └──── resubmit ── REJECTED ◄────────────────────────┘                              │
   └───────────────────────────────── resubmit ◄──────────────────────────────────────┘
* PENDING con kyc_submitted_at ≠ null = "en revisión"
```

| Desde | Acción | Hacia | Quién | Condiciones |
|---|---|---|---|---|
| `PENDING` | `Submit` | `PENDING` (+`kyc_submitted_at`) | Analyst, Operator, Client | Documentos obligatorios presentes (ver tabla) |
| `PENDING` (enviado) | `Approve` | `VERIFIED` | Analyst, Admin (`customers.kyc.review`) | Notas obligatorias (≥10 caracteres); ninguna verificación automática en `FAIL`; si `IsPep` o `AmlRiskLevel = HIGH` → solo Admin |
| `PENDING` (enviado) | `Reject` | `REJECTED` | Analyst, Admin | Motivo obligatorio |
| `VERIFIED` | `Reject` | `REJECTED` | Admin | Motivo obligatorio (fraude posterior) |
| `REJECTED` / `EXPIRED` | `Resubmit` | `PENDING` | Analyst, Operator, Client | Limpia `kyc_verified_*`, `kyc_rejected_reason` |
| `VERIFIED` | Job | `EXPIRED` | Sistema | `kyc_expires_at ≤ now` |

Documentos obligatorios para `Submit`:

| Tipo de persona | Categorías |
|---|---|
| `NATURAL` | `DNI_FRONTAL`, `DNI_REVERSO`, `SELFIE` (+ `BOLETA_PAGO` si `MonthlyNetIncome > Customers:Kyc:IncomeProofThreshold`) |
| `JURIDICA` | `FICHA_RUC` |

- [x] **CUS-04.1** Métodos en `Customer`: `SubmitKyc(IReadOnlySet<DocumentCategory> present, IClock)`, `ApproveKyc(Guid by, string notes, IClock, TimeSpan validity)`, `RejectKyc(Guid by, string reason, IClock)`, `ResubmitKyc(IClock)`, `ExpireKyc(IClock)`. Transición inválida → `Error.Conflict("Customers.InvalidKycTransition", "...")`.
- [x] **CUS-04.2** `ApproveKyc` fija `KycVerifiedBy`, `KycVerifiedAt`, `KycNotes`, `KycExpiresAt = now + Customers:Kyc:ValidityMonths` (default 12).
- [x] **CUS-04.3** Cada transición emite evento de dominio → outbox: `CustomerKycSubmitted`, `CustomerKycVerified`, `CustomerKycRejected`, `CustomerKycExpired` (ver §10).
- [x] **CUS-04.4** Endpoints:

| Método | Ruta | Body |
|---|---|---|
| `POST` | `/api/customers/{id}/kyc/submit` | — |
| `POST` | `/api/customers/{id}/kyc/approve` | `{ "notes": "..." }` |
| `POST` | `/api/customers/{id}/kyc/reject` | `{ "reason": "..." }` |
| `POST` | `/api/customers/{id}/kyc/resubmit` | — |
| `GET` | `/api/customers/{id}/kyc` | → estado, fechas, documentos vigentes, verificaciones automáticas, faltantes |

- [x] **CUS-04.5** `ExpireKycJob` (Quartz, `[DisallowConcurrentExecution]`, clustering BE-NF-09): diario 02:00 `America/Lima`; procesa en lotes de 500 con `UPDATE ... WHERE kyc_status = 'VERIFIED' AND kyc_expires_at <= now() RETURNING id` y encola un `CustomerKycExpired` por cliente. Registrar en `CustomersJobs.cs` igual que `SecurityJobs.cs`.
- [x] **CUS-04.6** Aviso previo: el mismo job encola `CustomerKycExpiringSoon` para los que vencen en 30 días (Notifications decide si envía correo).

- **CA-04.a** Aprobar sin notas → `422 Customers.KycNotesRequired`.
- **CA-04.b** `Submit` sin `SELFIE` → `422 Customers.MissingRequiredDocuments` con la lista de faltantes en `extensions.missing`.
- **CA-04.c** Aprobar un cliente `REJECTED` → `409 Customers.InvalidKycTransition`.
- **CA-04.d** Con `kyc_expires_at` en el pasado, tras correr el job el estado es `EXPIRED` y hay un evento en `outbox_messages`.
- **CA-04.e** Un Analyst no puede aprobar un PEP → `403 Customers.KycRequiresAdmin`.

---

### BE-CUS-05 — Verificación automática de identidad (`IIdentityVerifier`)

- [x] **CUS-05.1** Puerto:

```csharp
public interface IIdentityVerifier
{
    string ProviderName { get; }
    Task<KycCheckOutcome> VerifyRegistryAsync(IdentityRegistryQuery query, CancellationToken ct);      // DNI vs registro oficial
    Task<KycCheckOutcome> VerifyLivenessAsync(BiometricQuery query, CancellationToken ct);              // selfie + prueba de vida + face match
    Task<KycCheckOutcome> ScreenWatchlistsAsync(WatchlistQuery query, CancellationToken ct);            // PEP + sanciones/AML
}
public sealed record KycCheckOutcome(KycCheckType Type, KycCheckResult Result, decimal? Score,
    string? ExternalReference, string RawResponseJson, string? Error);
```

- [x] **CUS-05.2** `FakeIdentityVerifier` (desarrollo y pruebas), determinista por documento:
  - DNI que termina en `0` → registro `FAIL`; en `9` → watchlist `REVIEW` con `IsPep = true`; resto → `PASS` con score 95.
  - Seleccionado por `Customers:IdentityVerifier:Provider = "Fake"`. En `Production` el arranque **falla** si el proveedor es `Fake` (prueba en `StartupSecretGuardTests` o similar).
- [x] **CUS-05.3** Disparo: al ejecutarse `SubmitKyc`, se encola `RunKycChecksRequested(customerId)` en el mismo commit. `RunKycChecksHandler` (consumidor del outbox):
  1. Inserta 3 filas `kyc_verifications` en `PENDING`.
  2. Llama al proveedor **fuera** de la transacción, con timeout `Customers:IdentityVerifier:TimeoutSeconds` (default 15) y reintento exponencial (3 intentos) solo para errores transitorios.
  3. Guarda resultado, `score`, `raw_response` (sin imágenes en base64), `completed_at`.
  4. Si watchlist devuelve PEP → `customer.MarkAsPep()` y `AmlRiskLevel = HIGH`; sanciones → `AmlRiskLevel = HIGH`.
  5. Idempotente: si ya hay resultado no `PENDING` para ese check del mismo envío, no repite.
- [x] **CUS-05.4** Reintento manual: `POST /api/customers/{id}/kyc/checks/retry` (Analyst, Admin) solo para checks en `ERROR`.
- [x] **CUS-05.5** `GET /api/customers/{id}/kyc` incluye los resultados (último por tipo) para el visor FE-BO-08.
- [x] **CUS-05.6** Regla de aprobación (enlaza CUS-04): cualquier check en `FAIL` bloquea `Approve`; `REVIEW` o `ERROR` permiten aprobar pero exigen notas que lo mencionen (validación: notas ≥ 30 caracteres en ese caso).

- **CA-05.a** Con proveedor Fake y DNI `40000000`, tras `Submit` el check `IDENTITY_REGISTRY` queda `FAIL` y `Approve` devuelve `422 Customers.KycCheckFailed`.
- **CA-05.b** Si el proveedor no responde en 15 s, el check queda `ERROR` y el resto continúa.
- **CA-05.c** El handler procesado dos veces no crea filas duplicadas.

---

### BE-CUS-06 — Bloqueo de desembolso por KYC

- [x] **CUS-06.1** Contrato público en `Customers.Core/Contracts/ICustomerDisbursementEligibility.cs`:

```csharp
public interface ICustomerDisbursementEligibility
{
    Task<DisbursementEligibility> CheckAsync(Guid customerId, Guid bankAccountId, CancellationToken ct);
}
public sealed record DisbursementEligibility(bool IsEligible, IReadOnlyList<string> Reasons);
// Reasons: KYC_NOT_VERIFIED | KYC_EXPIRED | ACCOUNT_NOT_FOUND | ACCOUNT_NOT_OWNED |
//          ACCOUNT_NOT_VERIFIED | ACCOUNT_INACTIVE | ACCOUNT_IN_COOLING_PERIOD
```

- [x] **CUS-06.2** Implementación `internal` en Customers, registrada en DI. Calcula también "KYC vencido" aunque el job aún no haya corrido (`kyc_expires_at <= now`).
- [x] **CUS-06.3** Documentar en el README de Loans que el caso de uso de desembolso **debe** llamarlo dentro de su transacción y responder `422` con `code = Loans.CustomerNotEligible` y `extensions.reasons`. Mensaje para KYC: "Cliente con KYC pendiente" (CA-05.2).
- [x] **CUS-06.4** Defensa en BD (opcional pero recomendada): la FK compuesta DB-42 en `loans (customer_id, customer_bank_account_id)` → la crea quien implemente Loans, apoyándose en el `UNIQUE (customer_id, id)` de A5.

- **CA-06.a** Cliente `PENDING` → `IsEligible = false`, `Reasons = [KYC_NOT_VERIFIED]`.
- **CA-06.b** Cliente `VERIFIED` con cuenta en periodo de espera → `[ACCOUNT_IN_COOLING_PERIOD]`.
- **CA-06.c** Cuenta de otro cliente → `[ACCOUNT_NOT_OWNED]`.

---

### BE-CUS-07 — Consentimientos por finalidad y versión

- [x] **CUS-07.1** `GET /api/consents/current` (anónimo, lo usa el portal antes de registrarse) → textos vigentes `{ purpose, version, body, sha256 }`.
- [x] **CUS-07.2** `ConsentAcceptance(string Purpose, string Version)` en el alta; el caso de uso verifica que la versión enviada **es la vigente** → si no, `422 Customers.ConsentVersionOutdated`.
- [x] **CUS-07.3** `DATA_PROCESSING` obligatorio en el alta; `CREDIT_BUREAU_QUERY` obligatorio antes de que Origination consulte buró (Origination lo verificará vía contrato `ICustomerConsentQuery.HasActiveAsync(customerId, purpose)`); `MARKETING` opcional.
- [x] **CUS-07.4** Se guarda IP (`IClientInfoProvider` o equivalente en Common), user-agent y canal (`BACKOFFICE` si lo registra un empleado, `PORTAL` si es el cliente).
- [x] **CUS-07.5** `POST /api/customers/{id}/consents` (otorgar uno nuevo, p. ej. marketing) y `DELETE /api/customers/{id}/consents/MARKETING` (revocar). Revocar `DATA_PROCESSING` o `CREDIT_BUREAU_QUERY` → `422 Customers.ConsentNotRevocable` (Ley 29733 permite revocarlos, pero eso es un proceso de baja de cliente — fuera de MVP; documentarlo).
- [x] **CUS-07.6** `GET /api/customers/{id}/consents` → historial completo, incluidos revocados.
- [x] **CUS-07.7** Revocar marketing encola `CustomerMarketingConsentRevoked` (Notifications deja de enviar campañas).

- **CA-07.a** Alta sin `DATA_PROCESSING` → `422 Customers.ConsentRequired`.
- **CA-07.b** Alta con versión `v1` cuando la vigente es `v2` → `422 Customers.ConsentVersionOutdated`.
- **CA-07.c** Revocar marketing deja `revoked_at` y no borra la fila; volver a otorgarlo crea una fila nueva.

---

### BE-CUS-08 — Ficha 360

- [x] **CUS-08.1** Puerto `ICustomerPortfolioReader` en Customers.Core:

```csharp
Task<CustomerPortfolio> GetAsync(Guid customerId, CancellationToken ct);
// CustomerPortfolio(IReadOnlyList<LoanSummary> Loans, IReadOnlyList<ApplicationSummary> Applications)
```

- [x] **CUS-08.2** Adaptador que llama a los **contratos públicos** de Loans (`ILoanPortfolioQuery`) y Origination (`IApplicationHistoryQuery`). Si esos módulos aún no los exponen, crear las interfaces en sus `Contracts/` con una implementación que devuelva listas vacías y un `TODO(BE-LOA-xx)`; nunca leer sus tablas desde Customers.
- [x] **CUS-08.3** `GetCustomerOverviewUseCase` — `GET /api/customers/{id}/overview` (Admin, Analyst, Collector, Operator, Auditor):

```csharp
public sealed record CustomerOverviewResponse(
    CustomerResponse Customer,
    IReadOnlyList<LoanSummary> ActiveLoans,          // VIGENTE, EN_MORA
    IReadOnlyList<LoanSummary> ClosedLoans,
    ConsolidatedDebt Debt,                           // Principal, Interest, LateFees, Total (Money)
    int CurrentMaxDaysPastDue,
    int HistoricalMaxDaysPastDue,
    bool HasActiveDelinquency,                       // alerta visual (RF-06.1)
    IReadOnlyList<ApplicationSummary> Applications,  // orden cronológico DESC
    decimal CreditBalance);
```

- [x] **CUS-08.4** Cálculos en el caso de uso (puros, con pruebas): `Debt.Total = Σ(principal + interés + mora)` de préstamos activos, redondeo a 2 decimales; `HasActiveDelinquency = ActiveLoans.Any(l => l.DaysPastDue > 0)`.
- [x] **CUS-08.5** Rendimiento: p95 < 300 ms con 20 préstamos; llamadas a Loans y Origination en paralelo (`Task.WhenAll`).

- **CA-08.a** (CA-06.1) Cliente con un préstamo al día (saldo 1 000) y otro con 12 días de atraso (saldo 2 000 + 30 de mora) → dos préstamos en `ActiveLoans`, `Debt.Total = 3030.00`, `CurrentMaxDaysPastDue = 12`, `HasActiveDelinquency = true`.

---

### BE-CUS-09 — Cuentas del cliente

#### Dominio

- [x] **CUS-09.1** `CustomerBankAccount` (entidad hija del agregado `Customer` o agregado propio con `CustomerId`; **recomendado: agregado propio** para no cargar todas las cuentas al editar un cliente).
- [x] **CUS-09.2** Validaciones por tipo:

| `account_type` | Dato obligatorio | Validación |
|---|---|---|
| `CUENTA_AHORRO`, `CUENTA_CORRIENTE` | `cci` | `^\d{20}$`; `bank_code` (3 primeros) existe en el catálogo de bancos; dígitos de control (ver §14 R1) |
| `BILLETERA_DIGITAL` | `phoneWallet` + `bankName` (Yape, Plin…) | `^9\d{8}$` |
| `TARJETA_DEBITO` | `gatewayToken` | Nunca se recibe el PAN; solo el token de la pasarela. Si llega algo con forma de PAN (13–19 dígitos) → `400 Customers.RawCardNotAllowed` |

- [x] **CUS-09.3** Cifrado del número con puerto `IFieldProtector` (`Protect`, `Unprotect`, `Hash`): AES-GCM con clave del gestor de secretos y HMAC-SHA256 para `account_number_hash`. Reutilizar la lógica de `AesGcmSecretProtector` de Security **copiándola a Common** (`Common.Adapters/Crypto/`) en lugar de referenciar Security.
- [x] **CUS-09.4** Estado inicial `PENDING_VERIFICATION`, `is_primary = false`.

#### Casos de uso y endpoints

| Método | Ruta | Política | Descripción |
|---|---|---|---|
| `POST` | `/api/customers/{id}/bank-accounts` | Operator, Admin, Client (propio) | Registrar cuenta |
| `GET` | `/api/customers/{id}/bank-accounts` | `customers.read` o dueño | Lista con `last4`, banco, tipo, estado, primaria, `disbursementEnabledFrom` |
| `POST` | `/api/customers/{id}/bank-accounts/{accId}/verify` | Operator, Admin | Verificar titularidad (manual) |
| `POST` | `/api/customers/{id}/bank-accounts/{accId}/primary` | Ver CUS-10 | Marcar como primaria |
| `DELETE` | `/api/customers/{id}/bank-accounts/{accId}` | Operator, Admin, Client (propio) | Desactivar (soft) |

- [x] **CUS-09.5** `RegisterBankAccountUseCase`: valida, cifra, calcula hash y `last4`; duplicado (mismo hash activo) → `409 Customers.BankAccountAlreadyExists`. Máximo `Customers:BankAccounts:MaxActivePerCustomer` (default 5) → `422 Customers.BankAccountLimitReached`.
- [x] **CUS-09.6** `VerifyBankAccountOwnershipUseCase`: body `{ holderName, method: "MANUAL", notes }`. Regla: `holderName` normalizado (mayúsculas, sin tildes, sin dobles espacios) debe coincidir con `customer.FullName` → si no, `422 Customers.AccountHolderMismatch`. Fija `is_verified`, `verified_by`, `verified_at`, `status = ACTIVE`. El operador que verifica **no** puede ser el mismo usuario `ROLE_CLIENT` dueño.
- [x] **CUS-09.7** `DeactivateBankAccountUseCase`: `status = INACTIVE`, `is_primary = false`, `deactivated_at`. Si era primaria y hay préstamos en desembolso pendiente, Customers no lo sabe: lo controla Loans al desembolsar vía CUS-06 (`ACCOUNT_INACTIVE`).
- [x] **CUS-09.8** Respuesta nunca incluye `cci` ni número completo: `{ id, bankName, accountType, last4, phoneWalletMasked, isVerified, isPrimary, status, disbursementEnabledFrom }`.

- **CA-09.a** Registrar un CCI de 19 dígitos → `400 Customers.InvalidCci`.
- **CA-09.b** Registrar dos veces el mismo CCI para el mismo cliente → `409`.
- **CA-09.c** Verificar con titular "MARIA PEREZ" un cliente "JUAN PEREZ" → `422 Customers.AccountHolderMismatch`.
- **CA-09.d** El JSON de respuesta nunca contiene los 20 dígitos del CCI (prueba que serializa y busca el valor).

---

### BE-CUS-10 — Cambio de cuenta de desembolso con OTP y periodo de espera

- [x] **CUS-10.1** Flujo en dos pasos:
  1. `POST /api/customers/{id}/bank-accounts/{accId}/primary` → si el cliente **no tiene** primaria activa (primera vez), se marca directamente **sin** periodo de espera (requiere cuenta verificada). Si **ya tiene** una primaria, responde `202 Accepted` con `{ otpRequestId, channel, maskedTarget, expiresAt }` y envía OTP con propósito `ACCOUNT_CHANGE` y `reference_id = accId`.
  2. `POST /api/customers/{id}/bank-accounts/{accId}/primary/confirm` con `{ otpRequestId, code }` → valida OTP (CUS-11), en una transacción: quita `is_primary` a la anterior, marca la nueva, fija `disbursement_enabled_from = now + Customers:BankAccounts:CoolingPeriodHours`.
- [x] **CUS-10.2** `CoolingPeriodHours` configurable, validado en rango **24–48** al arrancar.
- [x] **CUS-10.3** El OTP se envía al **teléfono verificado** del cliente (o correo si no hay teléfono verificado); nunca a un dato recibido en la misma petición.
- [x] **CUS-10.4** Encolar `CustomerDisbursementAccountChanged { customerId, oldAccountLast4, newAccountLast4, enabledFrom }` → Notifications avisa al cliente por **todos** sus canales verificados (alerta antifraude).
- [x] **CUS-10.5** Si un empleado inicia el cambio en nombre del cliente, igual se exige el OTP del cliente; se audita quién lo inició (`initiated_by`).
- [x] **CUS-10.6** Solo cuentas `ACTIVE` y `is_verified = true` pueden ser primarias → `422 Customers.AccountNotVerified`.

- **CA-10.a** Cliente con primaria A; marca B → `202`; confirma con OTP correcto → B primaria, A no primaria, `disbursementEnabledFrom = now + 24 h`.
- **CA-10.b** Antes de que pase el periodo, `ICustomerDisbursementEligibility` devuelve `ACCOUNT_IN_COOLING_PERIOD`.
- **CA-10.c** OTP incorrecto 5 veces → `423 Customers.OtpLocked` y la primaria sigue siendo A.
- **CA-10.d** Primera cuenta primaria del cliente → `200` sin OTP y sin periodo de espera.

---

### BE-CUS-11 — OTP con propósito, expiración e intentos

- [x] **CUS-11.1** Entidad `OtpCode`: `Issue(customerId, purpose, channel, target, referenceId, codeHash, IClock, ttl, maxAttempts)`, `Verify(string code, IOtpHasher, IClock) → Result`, `Invalidate(IClock)`.
- [x] **CUS-11.2** Reglas:

| Regla | Valor por defecto (configurable) |
|---|---|
| Longitud | 6 dígitos, generado con `RandomNumberGenerator.GetInt32(0, 1_000_000)` |
| Expiración | 5 min |
| Intentos por código | 5 → al 5.º fallo `invalidated_at` y `423 Customers.OtpLocked` |
| Reenvío | Mínimo 60 s entre envíos del mismo propósito → `422 Customers.OtpResendTooSoon` (con `extensions.retryAfterSeconds`) |
| Máximo de envíos | 5 por cliente y propósito en 1 h → `423 Customers.OtpLocked` durante 1 h |
| Uno activo | Emitir uno nuevo invalida el anterior del mismo propósito |
| Comparación | Hash HMAC-SHA256 con pepper; comparación en tiempo constante (`CryptographicOperations.FixedTimeEquals`) |
| Propósito y referencia | Un OTP de `ACCOUNT_CHANGE` para la cuenta B **no** sirve para la cuenta C ni para `CONTRACT_SIGNATURE` |

- [x] **CUS-11.3** Puerto `IOtpSender.SendAsync(channel, target, code, purpose)`: la implementación MVP **encola** un evento en el outbox (`OtpDeliveryRequested`) que consume Notifications. **El código en claro solo viaja en ese mensaje**; el payload del outbox se debe tratar como sensible (no loguearlo).
- [x] **CUS-11.4** Contrato público reutilizable por otros módulos (Loans para firma de contrato, Security/portal para login):

```csharp
public interface ICustomerOtpService
{
    Task<Result<OtpIssued>> IssueAsync(Guid customerId, string purpose, Guid? referenceId, CancellationToken ct);
    Task<Result> VerifyAsync(Guid customerId, Guid otpRequestId, string code, string purpose, Guid? referenceId, CancellationToken ct);
}
```

- [x] **CUS-11.5** Verificación de contacto: `POST /api/customers/{id}/contact/verify/start` `{ channel: "EMAIL"|"SMS" }` y `/confirm` `{ otpRequestId, code }` → fija `EmailVerifiedAt` / `PhoneVerifiedAt` (cubre "correo electrónico verificado" de RF-04.1).
- [x] **CUS-11.6** Toda verificación fallida se audita con IP (`OTP_FAILED`); el bloqueo se audita (`OTP_LOCKED`).

- **CA-11.a** Código correcto tras 6 min → `422 Customers.OtpExpired`.
- **CA-11.b** Código correcto usado dos veces → la segunda `422 Customers.OtpAlreadyUsed`.
- **CA-11.c** OTP de `CONTACT_VERIFICATION` presentado para `ACCOUNT_CHANGE` → `422 Customers.OtpInvalid`.
- **CA-11.d** Pedir reenvío a los 20 s → `422 Customers.OtpResendTooSoon` con `retryAfterSeconds = 40`.

---

## 8. Catálogo de errores

Todos con prefijo `Customers.`; se devuelven en `ProblemDetails` con `extensions.code`.

| Código | `ErrorType` | HTTP | Requerimiento |
|---|---|---|---|
| `Customers.NotFound` | NotFound | 404 | 01 |
| `Customers.InvalidDocumentNumber` | Validation | 400 | 01 |
| `Customers.DocumentTypeInactive` | Validation | 400 | 01 |
| `Customers.Underage` | Validation | 400 | 01 |
| `Customers.InvalidPersonDocumentCombination` | Validation | 400 | 01 |
| `Customers.DocumentAlreadyExists` | Conflict | 409 | 01 |
| `Customers.ConcurrencyConflict` | Conflict | 409 | 01 |
| `Customers.FileTooLarge` | Validation | 400 | 03 |
| `Customers.UnsupportedFileType` | Validation | 400 | 03 |
| `Customers.DocumentNotFound` | NotFound | 404 | 03 |
| `Customers.InvalidKycTransition` | Conflict | 409 | 04 |
| `Customers.KycNotesRequired` | Unprocessable | 422 | 04 |
| `Customers.MissingRequiredDocuments` | Unprocessable | 422 | 04 |
| `Customers.KycRequiresAdmin` | Forbidden | 403 | 04 |
| `Customers.KycCheckFailed` | Unprocessable | 422 | 05 |
| `Customers.ConsentRequired` | Unprocessable | 422 | 07 |
| `Customers.ConsentVersionOutdated` | Unprocessable | 422 | 07 |
| `Customers.ConsentNotRevocable` | Unprocessable | 422 | 07 |
| `Customers.InvalidCci` | Validation | 400 | 09 |
| `Customers.InvalidWalletPhone` | Validation | 400 | 09 |
| `Customers.RawCardNotAllowed` | Validation | 400 | 09 |
| `Customers.BankAccountAlreadyExists` | Conflict | 409 | 09 |
| `Customers.BankAccountLimitReached` | Unprocessable | 422 | 09 |
| `Customers.AccountHolderMismatch` | Unprocessable | 422 | 09 |
| `Customers.AccountNotVerified` | Unprocessable | 422 | 10 |
| `Customers.OtpInvalid` | Unprocessable | 422 | 11 |
| `Customers.OtpExpired` | Unprocessable | 422 | 11 |
| `Customers.OtpAlreadyUsed` | Unprocessable | 422 | 11 |
| `Customers.OtpResendTooSoon` | Unprocessable | 422 | 11 |
| `Customers.OtpLocked` | Locked | 423 | 11 |

---

## 9. Resumen de endpoints y permisos

| Método | Ruta | Admin | Analyst | Operator | Collector | Auditor | Client (propio) | Req. |
|---|---|:-:|:-:|:-:|:-:|:-:|:-:|---|
| POST | `/api/customers` | ✅ | ✅ | ✅ | | | | 01 |
| GET | `/api/customers` | ✅ | ✅ | ✅ | ✅ | ✅ | | 01 |
| GET | `/api/customers/{id}` | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | 01 |
| PATCH | `/api/customers/{id}` | ✅ | ✅ | ✅ | | | | 01 |
| GET | `/api/customers/{id}/history` | ✅ | ✅ | | | ✅ | | 02 |
| POST | `/api/customers/{id}/documents` | ✅ | ✅ | ✅ | | | ✅ | 03 |
| GET | `/api/customers/{id}/documents` | ✅ | ✅ | ✅ | | ✅ | ✅ | 03 |
| GET | `/api/customers/{id}/documents/{docId}/url` | ✅ | ✅ | ✅ | | ✅ | ✅ | 03 |
| GET | `/api/customers/{id}/kyc` | ✅ | ✅ | ✅ | | ✅ | ✅ | 04 |
| POST | `/api/customers/{id}/kyc/submit` | ✅ | ✅ | ✅ | | | ✅ | 04 |
| POST | `/api/customers/{id}/kyc/approve` | ✅ | ✅ | | | | | 04 |
| POST | `/api/customers/{id}/kyc/reject` | ✅ | ✅ | | | | | 04 |
| POST | `/api/customers/{id}/kyc/resubmit` | ✅ | ✅ | ✅ | | | ✅ | 04 |
| POST | `/api/customers/{id}/kyc/checks/retry` | ✅ | ✅ | | | | | 05 |
| GET | `/api/consents/current` | anónimo | | | | | | 07 |
| GET | `/api/customers/{id}/consents` | ✅ | ✅ | ✅ | | ✅ | ✅ | 07 |
| POST | `/api/customers/{id}/consents` | ✅ | ✅ | ✅ | | | ✅ | 07 |
| DELETE | `/api/customers/{id}/consents/MARKETING` | ✅ | ✅ | ✅ | | | ✅ | 07 |
| GET | `/api/customers/{id}/overview` | ✅ | ✅ | ✅ | ✅ | ✅ | | 08 |
| POST | `/api/customers/{id}/bank-accounts` | ✅ | | ✅ | | | ✅ | 09 |
| GET | `/api/customers/{id}/bank-accounts` | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | 09 |
| POST | `/api/customers/{id}/bank-accounts/{accId}/verify` | ✅ | | ✅ | | | | 09 |
| DELETE | `/api/customers/{id}/bank-accounts/{accId}` | ✅ | | ✅ | | | ✅ | 09 |
| POST | `/api/customers/{id}/bank-accounts/{accId}/primary` | ✅ | | ✅ | | | ✅ | 10 |
| POST | `/api/customers/{id}/bank-accounts/{accId}/primary/confirm` | ✅ | | ✅ | | | ✅ | 10 |
| POST | `/api/customers/{id}/contact/verify/start` | ✅ | ✅ | ✅ | | | ✅ | 11 |
| POST | `/api/customers/{id}/contact/verify/confirm` | ✅ | ✅ | ✅ | | | ✅ | 11 |

Reglas comunes de los endpoints:

- [x] Cada endpoint con `.WithName(...)`, `.WithSummary("... (BE-CUS-XX)")` y `.Produces<T>()` / `.ProducesProblem(...)` para que el OpenAPI (BE-NF-05) quede completo.
- [x] "Client (propio)": filtro de endpoint reutilizable `RequireCustomerOwnershipOrRoles(...)` que compara `{id}` con `ICurrentUser.CustomerId`; si no coincide → `404`.
- [x] `Idempotency-Key` obligatorio en `POST /api/customers`, `POST .../bank-accounts` y `POST .../primary/confirm` (middleware existente).

---

## 10. Eventos de integración

En `Customers.Core/Contracts/IntegrationEvents/`, heredando de `IntegrationEvent`, registrados en `IntegrationEventTypeRegistry`. Nunca incluyen documento, CCI ni teléfono completos.

| Evento | Payload | Consumidores previstos |
|---|---|---|
| `CustomerRegisteredIntegrationEvent` | `CustomerId, PersonType` | Notifications (bienvenida) |
| `CustomerKycSubmittedIntegrationEvent` | `CustomerId` | Customers (`RunKycChecksHandler`) |
| `CustomerKycVerifiedIntegrationEvent` | `CustomerId, ExpiresAt` | Origination (habilita flujo), Notifications |
| `CustomerKycRejectedIntegrationEvent` | `CustomerId, Reason` | Notifications |
| `CustomerKycExpiredIntegrationEvent` / `...ExpiringSoon` | `CustomerId, ExpiresAt` | Notifications, Origination |
| `CustomerDisbursementAccountChangedIntegrationEvent` | `CustomerId, OldLast4, NewLast4, EnabledFrom` | Notifications (alerta antifraude) |
| `CustomerMarketingConsentRevokedIntegrationEvent` | `CustomerId` | Notifications |
| `OtpDeliveryRequestedIntegrationEvent` | `CustomerId, Channel, Target, Code, Purpose` | Notifications (**sensible**: no loguear) |

---

## 11. Configuración (`appsettings`)

```jsonc
"Customers": {
  "Kyc": {
    "ValidityMonths": 12,
    "ExpiringSoonDays": 30,
    "IncomeProofThreshold": 3000.00
  },
  "Documents": {
    "MaxFileSizeMb": 10,
    "SignedUrlTtlSeconds": 300,
    "AllowedTypes": [ "image/jpeg", "image/png", "image/webp", "application/pdf" ]
  },
  "Storage": {
    "Provider": "Local",            // Local | S3
    "LocalPath": "./.storage",
    "Bucket": "",                   // S3: nombre; credenciales desde el gestor de secretos
    "Region": ""
  },
  "IdentityVerifier": {
    "Provider": "Fake",             // Fake | <proveedor real en F2>; Fake prohibido en Production
    "TimeoutSeconds": 15
  },
  "BankAccounts": {
    "CoolingPeriodHours": 24,       // validado 24–48
    "MaxActivePerCustomer": 5
  },
  "Otp": {
    "Length": 6,
    "TtlSeconds": 300,
    "MaxAttempts": 5,
    "ResendCooldownSeconds": 60,
    "MaxSendsPerHour": 5
    // "Pepper": desde el gestor de secretos, nunca aquí
  }
}
```

Secretos nuevos (añadir al [`RUNBOOK_SECRETOS_Y_ROTACION.md`](../backend/docs/RUNBOOK_SECRETOS_Y_ROTACION.md)): `Customers:Otp:Pepper`, `Customers:FieldProtection:Key`, `Customers:FieldProtection:HmacKey`, credenciales de storage.

---

## 12. Plan de pruebas

| Proyecto | Qué probar | Ítems |
|---|---|---|
| `LoanSystem.Domain.UnitTests` | `IdentityDocumentTests` (DNI, CE, RUC con dígito verificador — casos válidos e inválidos, PASAPORTE), `CustomerTests` (invariantes, edad con `IClock` falso), `KycStateMachineTests` (todas las transiciones de la tabla CUS-04, válidas e inválidas), `CustomerBankAccountTests` (CCI, billetera, PAN rechazado), `OtpCodeTests` (expiración, intentos, un solo uso, propósito/referencia) | 01, 04, 09, 11 |
| `LoanSystem.Application.UnitTests` | Un archivo por caso de uso con dobles de los puertos: `RegisterCustomerUseCaseTests` (409, 422 consentimiento), `ApproveKycUseCaseTests` (PEP solo Admin, check FAIL), `ConfirmPrimaryAccountUseCaseTests` (periodo de espera), `CustomerDisbursementEligibilityTests` (cada `Reason`), `GetCustomerOverviewUseCaseTests` (CA-08.a con montos exactos) | 01–11 |
| `LoanSystem.Adapters.UnitTests` | `CustomersEndpointsAuthorizationTests` (matriz §9: cada rol × endpoint → 200/403/404), `LocalDocumentStorageTests` (firma y expiración), `FileSignatureSnifferTests`, `AesGcmFieldProtectorTests`, serialización que no filtra CCI (CA-09.d) | 03, 09, §9 |
| `LoanSystem.Architecture.Tests` | Aislamiento de Customers; `Fake` prohibido en Production; sin `float`/`double` | B4, 05 |
| Integración (PostgreSQL real, Testcontainers — BE-NF-07) | Migraciones 19–23 sobre BD limpia; carrera de alta duplicada (CA-01.e); índice único de primaria (dos confirmaciones concurrentes → una falla); `ExpireKycJob` (CA-04.d); historial desde `audit_logs` (CA-02.a) | A, 01, 02, 04, 10 |
| Postman / contrato | Colección `Customers` con un request por endpoint y los ejemplos de error del §8; se ejecuta en CI | §9 |

Datos de prueba sugeridos: DNI `45879612` (válido), RUC `20100070970` (válido), RUC `20100070971` (dígito incorrecto), CCI `00219300012345678901` (formato válido).

---

## 13. Definition of Done

Un requerimiento BE-CUS-XX está terminado cuando:

- [x] Todos sus ítems `CUS-XX.Y` están marcados.
- [x] Cada criterio de aceptación (`CA-XX.*`) tiene al menos una prueba automatizada que pasa.
- [ ] `dotnet build` sin warnings nuevos y `dotnet test` en verde (incluidas pruebas de arquitectura).
- [ ] Las migraciones nuevas corren con `LoanSystem.DbMigrator` en BD limpia y en BD con seeds.
- [x] Endpoints visibles en OpenAPI con `summary`, respuestas y ejemplos de error.
- [x] Las escrituras dejan registro en `audit_logs` con acción, usuario, IP y user-agent.
- [x] Ningún log, respuesta ni evento contiene documento, CCI, teléfono o código OTP en claro (salvo `OtpDeliveryRequested`, marcado como sensible).
- [x] README de `Customers.Core` y `Customers.Adapters` actualizados con los archivos nuevos (formato actual: "¿Para qué sirve? / ¿Qué hace? / ¿Por qué?").
- [x] En [`REQUERIMIENTOS_BACKEND.md`](REQUERIMIENTOS_BACKEND.md) la columna **Estado** del requerimiento pasa a **Implementado**.
- [ ] PR revisado por otra persona; si toca `IFieldProtector`, OTP o storage, revisión de seguridad.

> **Estado de verificación:** `dotnet build backend\LoanSystem.slnx --no-restore --no-incremental` compila con `0` errores y `0` warnings del compilador (`3` warnings `NU1903` preexistentes de `Microsoft.OpenApi 2.0.0`). Las pruebas de Customers y arquitectura pasan (`Domain.UnitTests` Customers `27/27`, `Application.UnitTests` Customers `18/18`, `Adapters.UnitTests` Customers/Security/StartupSecretGuard/JWT `85/85`, `ArchitectureTests` `66/66`), mientras que la ejecución completa de `dotnet test` conserva `7` fallos preexistentes ajenos a Customers (`ConfigurationSecretsTests.SourceFiles_MustNotContainPrivateKeyPemBlocks`, `2` en `Application.UnitTests` de Security/Approvals y `4` en `Adapters.UnitTests` de Security JWT/JWKS). En `IntegrationTests` (`4/4` de `CustomersIntegrationTests`, `18/18` total), las pruebas con Testcontainers/PostgreSQL retornan temprano porque Docker Desktop no está activo en este equipo (`A5.3` y punto 4 pendientes de ejecutar con PostgreSQL/Docker activo).

---

## 14. Riesgos y preguntas abiertas

| # | Tema | Acción propuesta | Dueño |
|---|---|---|---|
| R1 | Algoritmo exacto de los 2 dígitos de control del CCI | Confirmar con la documentación de la CCE o con el banco pagador. **Mientras tanto** el MVP valida longitud (20), solo dígitos y código de banco contra catálogo; dejar `ICciValidator` aislado para añadir el control después. | Negocio / Treasury |
| R2 | Proveedor real de verificación de identidad (RENIEC, biometría, listas) | Elegir en F2; el puerto y el *fake* ya permiten avanzar. | Producto |
| R3 | ¿Vigencia del KYC 12 meses para todos o según `AmlRiskLevel` (p. ej. HIGH = 6 meses)? | Default 12 meses; si se decide por riesgo, `ValidityMonths` pasa a ser un diccionario por nivel. | Cumplimiento |
| R4 | Catálogo de bancos (código CCI → nombre) | Crear tabla `banks` en Administration o seed estático en Customers. Decidir antes de CUS-09. | Equipo |
| R5 | Revocación de `DATA_PROCESSING` (derecho ARCO, Ley 29733) | Fuera de MVP; requiere flujo de baja/anonimización. Abrir requerimiento aparte. | Legal |
| R6 | Archivos huérfanos en storage si falla la transacción (CUS-03.5) | Job de limpieza en F2 que compara objetos vs `customer_documents`. | Equipo |
| R7 | `ROLE_CLIENT` → `ICurrentUser.CustomerId` | Verificar que el JWT del portal incluye el claim del `customer_id`; si no, añadirlo en Security antes de los endpoints "Client (propio)". | Security |
