# Plan de implementación — Módulo Products (productos de crédito y reglas financieras)

> Documento de ejecución para el desarrollador backend. Desglosa la sección **3. Products** de
> [`REQUERIMIENTOS_BACKEND.md`](REQUERIMIENTOS_BACKEND.md) (BE-PRO-01 … BE-PRO-09) en tareas concretas:
> migraciones, clases por capa, fórmulas exactas, endpoints, reglas de validación, códigos de error,
> valores de prueba calculados y criterio de "terminado".
>
> Historias de origen: [`03-productos-de-credito.md`](03-productos-de-credito.md) (US-07, US-08, US-09, RNF-06, RNF-07) ·
> Decisión previa obligatoria: [`DECISION_01_TASA_EFECTIVA_TEA.md`](../backend/docs/DECISION_01_TASA_EFECTIVA_TEA.md) ·
> BD: DB-28 … DB-31, DB-37 de [`REQUERIMIENTOS_BDD.md`](REQUERIMIENTOS_BDD.md) ·
> Frontend que lo consume: FE-BO-11, FE-BO-12, FE-BO-14, FE-CP-06 de [`REQUERIMIENTOS_FRONTEND.md`](REQUERIMIENTOS_FRONTEND.md).

---

## Índice

0. [Cómo usar este documento](#0-cómo-usar-este-documento)
1. [Alcance](#1-alcance)
2. [Estado actual del código](#2-estado-actual-del-código)
3. [Decisiones de diseño (cerradas)](#3-decisiones-de-diseño-cerradas)
4. [Orden de ejecución y dependencias](#4-orden-de-ejecución-y-dependencias)
5. [Fase A — Base de datos](#5-fase-a--base-de-datos)
6. [Fase B — Estructura del módulo](#6-fase-b--estructura-del-módulo)
7. [Ítems por requerimiento (BE-PRO-01 … BE-PRO-09 + TCEA)](#7-ítems-por-requerimiento)
8. [Catálogo de errores](#8-catálogo-de-errores)
9. [Resumen de endpoints y permisos](#9-resumen-de-endpoints-y-permisos)
10. [Contratos públicos y eventos](#10-contratos-públicos-y-eventos)
11. [Configuración (`appsettings`)](#11-configuración-appsettings)
12. [Valores de referencia para pruebas](#12-valores-de-referencia-para-pruebas)
13. [Plan de pruebas](#13-plan-de-pruebas)
14. [Definition of Done](#14-definition-of-done)
15. [Riesgos y preguntas abiertas](#15-riesgos-y-preguntas-abiertas)

---

## 0. Cómo usar este documento

- Cada ítem tiene un ID `PRO-XX.Y`. **Una rama / PR por requerimiento** (`feat/BE-PRO-02`, …).
- Las casillas `- [ ]` son la lista de trabajo; se marcan en el PR.
- Cada requerimiento termina con **Criterios de aceptación** (Dado / Cuando / Entonces). Ninguno se aprueba sin prueba automatizada.
- Los valores numéricos esperados de §12 se calcularon con aritmética decimal de 40 dígitos y redondeo half-up; son la referencia para las pruebas (sustituyen al "Excel patrón" de BE-NF-07 mientras no exista).
- Si algo de este plan choca con el código existente, gana el patrón del código y se corrige este documento en el mismo PR.

**Reglas transversales del módulo** (no se repiten en cada ítem):

| Regla | Detalle |
|---|---|
| Aritmética | Solo `decimal` / `Money`. Prohibido `double`, `float`, `Math.Pow` (lo verifica `FloatingPointIlScanner`). Potencias enteras con `InterestRate.Pow`; raíces con Newton en `decimal` (ya implementado). |
| Redondeo | La **tasa del periodo no se redondea** (DECISION_01). Se redondea cada componente (interés, capital, comisión) al convertirlo a `Money`: 2 decimales, `MidpointRounding.AwayFromZero`. |
| Cuadre | `Σ capital de las cuotas = capital financiado`, exacto al céntimo. El residuo lo absorbe **la última cuota** (RNF-07). |
| Unidades en la API | Las tasas viajan como **porcentaje** (`30` = 30 %), igual que el `/simulate` actual. En BD se guardan como **fracción** (`0.300000`). La conversión ocurre solo en el mapeo de contratos. |
| Fechas | `DateOnly` para fechas de negocio, calculadas con `IClock.LimaToday`. Prohibido `DateTime.Now`. |
| Errores | `Result`/`Result<T>` + `Error.*` → `.ToHttpResult()` → `ProblemDetails`. |
| Auditoría | `ProductsDbContext` ya usa `.UseAuditLog(sp)`; cada escritura llama `_audit.SetAction("<ACCION>", "<tabla>")`. |
| Visibilidad | Dominio, aplicación y adaptadores `internal sealed`; solo `Contracts/` y `Ports/Inbound/` son `public`. |
| Migraciones | SQL-First con DbUp. La última usada es `23_customer_bank_accounts_v2.sql`; este plan usa **24 a 27**. |

---

## 1. Alcance

| ID | Requerimiento | Fase | En este plan |
|---|---|---|---|
| BE-PRO-01 | `POST/GET /api/loan-products` con validación de rangos | MVP | ✅ CRUD completo, activar/desactivar |
| BE-PRO-02 | Motor Francés, Flat, Alemán y Americano con conversión TEA → tasa del periodo por frecuencia | MVP | ✅ |
| BE-PRO-03 | Comisión de apertura fija o %, deductiva o financiada; cargo periódico por cuota | MVP | ✅ |
| BE-PRO-04 | Separar comisión de administración y seguro (% del saldo) | F2 | ⚠️ Solo el diseño (§7, PRO-04); se implementa en F2 |
| BE-PRO-05 | Gracia, tasa de mora diaria, base de mora, anatocismo desactivado | MVP | ✅ Parámetros + calculadora pura de mora |
| BE-PRO-06 | Día de pago fijo para calcular vencimientos | MVP | ✅ Generador de fechas con feriados |
| BE-PRO-07 | Elegibilidad: edad, ingreso mínimo y antigüedad laboral | MVP | ✅ Evaluador público para Origination |
| BE-PRO-08 | TEA y tasa de mora ≤ tope legal vigente del BCRP | MVP | ✅ Tabla de topes + validación + job de cumplimiento |
| BE-PRO-09 | Cambiar un producto no afecta préstamos desembolsados (snapshot) | MVP | ✅ Versionado inmutable + cuatro ojos para cambios de tasa |
| BE-ORI-04 (parte de motor) | Cálculo de la **TCEA** | MVP | ✅ El cálculo vive aquí; Origination solo lo muestra/guarda |
| BE-ORI-06 (parámetro) | Umbrales de DTI por producto | MVP | ✅ Solo el parámetro en el producto; la regla la aplica Origination |
| BE-PRO-10, BE-PRO-11 | Desgravamen integrado en la anualidad, gracia inicial, ITF | V2 | ❌ Otro plan |
| BE-PRO-12 | Tope BCRP también en contrato | V2 | ⚠️ La validación queda expuesta como contrato; Loans la llamará en V2 |

---

## 2. Estado actual del código

**Existe y se conserva:**

| Archivo | Qué hace | Qué cambia en este plan |
|---|---|---|
| `Products.Core/Domain/InterestRate.cs` | TEA → tasa mensual con Newton en `decimal`; `Pow` por cuadrados | Se generaliza a cualquier número de días (PRO-02.1). `ToMonthlyRate` se mantiene como atajo. |
| `Products.Core/Domain/FrenchAmortizationSchedule.cs` | Francés mensual, residuo en la última cuota | Pasa a ser `FrenchStrategy` dentro del motor (PRO-02.4). Sus pruebas actuales **deben seguir pasando** sin cambiar valores. |
| `Products.Core/Domain/AmortizationMethod.cs` | Enum con solo `French`; parser acepta `"FRENCH"` | Se alinea con el enum PG (`FRANCES`, `FLAT`, `ALEMAN`, `AMERICANO`); `"FRENCH"` queda como alias. |
| `Products.Core/Application/SimulateScheduleUseCase.cs` + `POST /api/products/simulate` (anónimo) | Simulación rápida sin producto | Se mantiene por compatibilidad y gana métodos y frecuencias (PRO-02.8). |
| `Products.Adapters/Outbound/Persistence/ProductsDbContext.cs` | Sin entidades | Mapea productos, versiones, topes y `ApprovalRequest`. |
| BD `04_loan_products.sql` | Tabla `loan_products` con montos, plazos, frecuencias, método, TEA, comisiones, gracia, mora, anatocismo, penalidad, elegibilidad, `holiday_shift_direction` | Se extiende (migración 24) y se versiona (25). |
| BD `06_loans_schedules.sql` | `loans` ya guarda un snapshot de tasa, método, frecuencia, gracia, mora y comisiones | Se le añade `product_version_id` (27). |
| `Common.Core/Domain/Approvals/ApprovalOperationTypes.ProductRateChange` y permiso `approvals.products.rates` | Cuatro ojos ya preparado | Products implementa su `IApprovalHandler` (PRO-09). |
| Tests: `InterestRateTests`, `FrenchAmortizationScheduleTests`, `SimulateScheduleUseCaseTests` | Cubren el francés mensual | Se conservan; se añaden los de §13. |

**Falta:** entidad de producto, CRUD, versionado, métodos Flat/Alemán/Americano, frecuencias distintas de mensual, fechas de vencimiento (día de pago y feriados), comisiones en el cronograma, TCEA, calculadora de mora, elegibilidad, topes BCRP, contratos públicos para Origination y Loans.

**Pendientes de DECISION_01 que este plan cierra:** otras periodicidades (PRO-02.1), TCEA (PRO-TCEA), topes BCRP (PRO-08), precisión de la tasa del periodo guardada (A4), alineación `FRENCH`/`FRANCES` (PRO-02.3).

---

## 3. Decisiones de diseño (cerradas)

| # | Decisión | Motivo |
|---|---|---|
| D1 | CRUD bajo **`/api/loan-products`** (como dicen RF-07.2 y BE-PRO-01). `POST /api/products/simulate` se mantiene como alias por compatibilidad. | No romper el frontend actual del simulador. |
| D2 | **Conversión de tasas con año de 360 días:** `i_d = (1 + TEA)^(d/360) − 1`, donde `d` son los días del periodo. Para `NOMINAL`: `i_d = TNA · d/360`. Con `d = 30` el resultado es idéntico al actual `(1+TEA)^(1/12) − 1`. | Pendiente de DECISION_01; convención SBS. |
| D3 | **Dos modos de días** en el motor: **`NOMINAL_PERIODS`** (sin fecha de desembolso: cada periodo vale 1/7/15/30 días según la frecuencia) y **`ACTUAL_360`** (con fecha de desembolso: días reales entre vencimientos teóricos). El simulador rápido usa el primero; el cronograma de un producto y el préstamo real usan el segundo. | El día de pago (BE-PRO-06) hace irregular el primer periodo; solo con días reales la TEA cobrada es exactamente la anunciada. |
| D4 | En `ACTUAL_360` el **interés se calcula hasta la fecha teórica** de vencimiento; si cae en feriado o domingo, solo se mueve la **fecha exigible** (NEXT/PREV) y no se cobra interés adicional. La mora se cuenta desde la fecha exigible. | Favorable al cliente y evita que un feriado cambie la cuota. |
| D5 | Día no hábil = **domingo** + feriados de la tabla `holidays` (incluidos los `is_recurring` por día y mes). El sábado es hábil (canales digitales). Configurable. | Fintech 100 % digital. |
| D6 | **Versionado inmutable**: `loan_product_versions` guarda cada conjunto de condiciones financieras; `loan_products` guarda la cabecera y una copia de la versión vigente (para lecturas simples). Solicitudes y préstamos referencian `product_version_id`. | RNF-06 con trazabilidad completa, sin romper el esquema actual. |
| D7 | Cambios en **tasa, mora, gracia, comisiones, penalidad, método o segmento regulatorio** de un producto existente pasan por **cuatro ojos** (`PRODUCT_RATE_CHANGE`). Cambios en límites, frecuencias, elegibilidad, día de pago, feriados o DTI crean versión nueva directamente. Nombre y descripción no crean versión. | BE-SEC-09 sin frenar la operación diaria. |
| D8 | El alta de un producto **no** requiere cuatro ojos, pero **sí** valida topes BCRP. El producto nace `ACTIVO` (CA-07.1). | Coherente con la historia; el riesgo queda en §15. |
| D9 | Solo tasas **`EFECTIVA`** para productos (`NOMINAL` solo en el simulador rápido). | Transparencia: al cliente se le informa TEA. |
| D10 | **Anatocismo bloqueado** en MVP: `allowsCompounding = true` → `422`. **Penalidad por prepago** forzada a `0` en MVP. Ambas restricciones salen de configuración (§11) para poder relajarlas si legal lo aprueba. | RF-09.4 y brecha de cumplimiento (prepago sin penalidad) de REQUERIMIENTOS_BACKEND §12. |
| D11 | Los **límites de monto** se aplican al **monto solicitado** (el que pide el cliente), no al capital financiado con comisión. | Es lo que el cliente ve en el simulador. |
| D12 | En **FLAT** la TEA que se informa y que se valida contra el tope BCRP es la **TEA efectiva real** (TIR del flujo), calculada con el **plazo máximo** del producto (el peor caso). | Con interés flat, la tasa real supera la tasa de entrada (ver §12: 30 % de entrada → 56,80 % real a 12 meses). |
| D13 | La mora es **interés simple** sobre la base, calculado de cero en cada liquidación (`base × tasa × días`), nunca como suma de devengos diarios redondeados. | Evita descuadres de céntimos y garantiza que no capitaliza. |
| D14 | El motor es **puro** (sin BD, sin reloj, sin I/O): recibe todo por parámetros, incluido el calendario de días hábiles. Un servicio de aplicación carga producto y feriados y llama al motor. | Rendimiento (< 50 ms) y pruebas deterministas. |

---

## 4. Orden de ejecución y dependencias

```
Fase A  BD (migraciones 24–27) ─────────┐
Fase B  Estructura + permisos ───────────┤
                                         ▼
Sprint 1:  PRO-02 Motor (tasas, 4 métodos, modo NOMINAL_PERIODS) ──► PRO-03 Comisiones ──► TCEA
Sprint 2:  PRO-06 Fechas y feriados (modo ACTUAL_360) ──► PRO-05 Mora ──► PRO-01 CRUD de productos
Sprint 3:  PRO-08 Topes BCRP ──► PRO-09 Versionado + cuatro ojos ──► PRO-07 Elegibilidad
F2:        PRO-04 Comisión de administración y seguro separados
```

| Requerimiento | Depende de | Estimación |
|---|---|---|
| Fase A + B | — | 2 d |
| PRO-02 Motor | B | 4 d |
| PRO-03 Comisiones | PRO-02 | 1 d |
| TCEA | PRO-03 | 1,5 d |
| PRO-06 Fechas | PRO-02, contrato de feriados de Administration | 2,5 d |
| PRO-05 Mora | B | 1 d |
| PRO-01 CRUD | A, PRO-02 | 3 d |
| PRO-08 Topes BCRP | PRO-01 | 2,5 d |
| PRO-09 Versionado + cuatro ojos | PRO-01, PRO-08 | 3 d |
| PRO-07 Elegibilidad | PRO-01 | 1,5 d |
| **Total MVP** | | **≈ 22 d/dev** |
| PRO-04 (F2) | PRO-03 | 2 d |

---

## 5. Fase A — Base de datos

Scripts en `database/migrations/`, con cabecera de dominio e IDs como `04_loan_products.sql`.

### A1. `24_loan_products_v2.sql` (DB-29, DB-30, DB-31, BE-PRO-06/07/08)

- [ ] **A1.1** Tipos y columnas nuevas:

```sql
CREATE TYPE regulatory_segment        AS ENUM ('CONSUMO', 'MICROEMPRESA', 'PEQUENA_EMPRESA');
CREATE TYPE product_compliance_status AS ENUM ('COMPLIANT', 'NON_COMPLIANT', 'UNKNOWN');

ALTER TABLE loan_products
    ADD COLUMN code                      VARCHAR(30),
    ADD COLUMN payment_day               SMALLINT CHECK (payment_day BETWEEN 1 AND 28),          -- DB-29
    ADD COLUMN min_work_seniority_months SMALLINT NOT NULL DEFAULT 0
                                         CHECK (min_work_seniority_months BETWEEN 0 AND 600),  -- DB-30
    ADD COLUMN max_dti_auto              NUMERIC(5,4) NOT NULL DEFAULT 0.3500,                 -- DB-31
    ADD COLUMN max_dti_manual            NUMERIC(5,4) NOT NULL DEFAULT 0.5000,
    ADD COLUMN regulatory_segment        regulatory_segment NOT NULL DEFAULT 'CONSUMO',
    ADD COLUMN compliance_status         product_compliance_status NOT NULL DEFAULT 'UNKNOWN',
    ADD COLUMN compliance_checked_at     TIMESTAMPTZ,
    ADD COLUMN current_version_id        UUID,                 -- FK se añade en la migración 25
    ADD COLUMN version                   INTEGER NOT NULL DEFAULT 0;   -- concurrencia optimista
```

- [ ] **A1.2** Código único (rellenar filas existentes antes de `NOT NULL`):

```sql
UPDATE loan_products SET code = 'PRD_' || upper(substr(replace(id::text, '-', ''), 1, 8)) WHERE code IS NULL;
ALTER TABLE loan_products ALTER COLUMN code SET NOT NULL;
ALTER TABLE loan_products ADD CONSTRAINT uq_loan_products_code UNIQUE (code);
```

- [ ] **A1.3** Restricciones de coherencia (defensa en BD de lo que valida el dominio):

```sql
ALTER TABLE loan_products
    ADD CONSTRAINT chk_product_dti         CHECK (max_dti_auto > 0 AND max_dti_auto <= max_dti_manual AND max_dti_manual <= 1),
    ADD CONSTRAINT chk_product_ages        CHECK (min_age >= 18 AND max_age >= min_age AND max_age <= 85),
    ADD CONSTRAINT chk_product_grace       CHECK (grace_days BETWEEN 0 AND 30),
    ADD CONSTRAINT chk_product_frequencies CHECK (cardinality(allowed_frequencies) > 0),
    ADD CONSTRAINT chk_product_mora_rate   CHECK (mora_rate_per_day >= 0),
    ADD CONSTRAINT chk_product_fees        CHECK (opening_fee_rate >= 0 AND opening_fee_fixed >= 0 AND periodic_fee >= 0);
```

### A2. `25_loan_product_versions.sql` (BE-PRO-09)

- [ ] **A2.1** Tabla inmutable de versiones (mismas columnas financieras que `loan_products`):

```sql
CREATE TABLE loan_product_versions (
    id                        UUID                PRIMARY KEY DEFAULT gen_random_uuid(),
    product_id                UUID                NOT NULL REFERENCES loan_products(id),
    version_number            INTEGER             NOT NULL,
    currency_code             CHAR(3)             NOT NULL,
    min_amount                NUMERIC(18,4)       NOT NULL,
    max_amount                NUMERIC(18,4)       NOT NULL,
    min_term                  SMALLINT            NOT NULL,
    max_term                  SMALLINT            NOT NULL,
    allowed_frequencies       payment_frequency[] NOT NULL,
    amortization_method       amortization_method NOT NULL,
    rate_type                 rate_type           NOT NULL,
    annual_rate               NUMERIC(12,6)       NOT NULL,
    opening_fee_rate          NUMERIC(12,6)       NOT NULL,
    opening_fee_fixed         NUMERIC(18,4)       NOT NULL,
    opening_fee_mode          commission_mode     NOT NULL,
    periodic_fee              NUMERIC(18,4)       NOT NULL,
    grace_days                SMALLINT            NOT NULL,
    mora_rate_per_day         NUMERIC(12,6)       NOT NULL,
    mora_base                 mora_base_type      NOT NULL,
    allows_anatocismo         BOOLEAN             NOT NULL,
    prepayment_penalty_rate   NUMERIC(12,6)       NOT NULL,
    min_age                   SMALLINT            NOT NULL,
    max_age                   SMALLINT            NOT NULL,
    min_monthly_income        NUMERIC(18,4)       NOT NULL,
    min_work_seniority_months SMALLINT            NOT NULL,
    payment_day               SMALLINT,
    holiday_shift_direction   VARCHAR(4)          NOT NULL,
    max_dti_auto              NUMERIC(5,4)        NOT NULL,
    max_dti_manual            NUMERIC(5,4)        NOT NULL,
    regulatory_segment        regulatory_segment  NOT NULL,
    effective_from            TIMESTAMPTZ         NOT NULL DEFAULT NOW(),
    change_reason             VARCHAR(500),
    approval_request_id       UUID,                -- NULL si no pasó por cuatro ojos
    created_by                UUID                REFERENCES users(id),
    created_at                TIMESTAMPTZ         NOT NULL DEFAULT NOW(),
    UNIQUE (product_id, version_number)
);
CREATE INDEX ix_loan_product_versions_product ON loan_product_versions (product_id, version_number DESC);
```

- [ ] **A2.2** Append-only, con el mismo patrón que `15_audit_logs_append_only.sql`:

```sql
CREATE OR REPLACE FUNCTION fn_loan_product_versions_immutable()
RETURNS TRIGGER LANGUAGE plpgsql AS $$
BEGIN
    RAISE EXCEPTION 'loan_product_versions es inmutable (RNF-06): % no permitido', TG_OP;
END $$;
CREATE TRIGGER tg_loan_product_versions_immutable
    BEFORE UPDATE OR DELETE ON loan_product_versions
    FOR EACH ROW EXECUTE FUNCTION fn_loan_product_versions_immutable();
```

- [ ] **A2.3** Backfill: insertar la versión `1` de cada producto existente copiando sus columnas (`currency_code` desde `currencies.code`), luego `UPDATE loan_products SET current_version_id = v.id` y crear la FK `fk_loan_products_current_version`.

### A3. `26_regulatory_rate_caps.sql` (BE-PRO-08)

- [ ] **A3.1**

```sql
CREATE EXTENSION IF NOT EXISTS btree_gist;

CREATE TABLE regulatory_rate_caps (
    id                    UUID               PRIMARY KEY DEFAULT gen_random_uuid(),
    currency_code         CHAR(3)            NOT NULL,
    segment               regulatory_segment NOT NULL,
    max_compensatory_tea  NUMERIC(12,6)      NOT NULL CHECK (max_compensatory_tea > 0),
    max_moratory_tea      NUMERIC(12,6)      NOT NULL CHECK (max_moratory_tea > 0),
    valid_from            DATE               NOT NULL,
    valid_to              DATE,                          -- NULL = vigente sin fecha de fin
    source_reference      VARCHAR(150)       NOT NULL,   -- N.º de circular del BCRP
    created_by            UUID               REFERENCES users(id),
    created_at            TIMESTAMPTZ        NOT NULL DEFAULT NOW(),
    CHECK (valid_to IS NULL OR valid_to >= valid_from),
    EXCLUDE USING gist (
        currency_code WITH =,
        segment       WITH =,
        daterange(valid_from, COALESCE(valid_to, 'infinity'::date), '[]') WITH &&)
);
COMMENT ON TABLE regulatory_rate_caps IS
    'Topes de TEA compensatoria y moratoria publicados por el BCRP (Ley 31143). Vigencias sin solaparse por moneda y segmento (BE-PRO-08).';
```

### A4. `27_product_version_refs.sql` (BE-PRO-09; coordinar con Origination y Loans)

- [ ] **A4.1**

```sql
ALTER TABLE loan_applications ADD COLUMN product_version_id UUID REFERENCES loan_product_versions(id);
ALTER TABLE loans             ADD COLUMN product_version_id UUID REFERENCES loan_product_versions(id);

-- Snapshot adicional que el préstamo necesita para recalcular sin leer el producto
ALTER TABLE loans
    ADD COLUMN rate_type               rate_type,
    ADD COLUMN period_rate             NUMERIC(24,16),   -- tasa del periodo usada (DECISION_01: 12,6 es corto para tasas diarias)
    ADD COLUMN day_count_mode          VARCHAR(20),      -- 'ACTUAL_360' | 'NOMINAL_PERIODS'
    ADD COLUMN payment_day             SMALLINT,
    ADD COLUMN holiday_shift_direction VARCHAR(4),
    ADD COLUMN opening_fee_mode        commission_mode,
    ADD COLUMN tcea                    NUMERIC(12,6);    -- DB-37
```

- [ ] **A4.2** Backfill de `product_version_id` con la versión 1 del producto de cada fila existente.
- [ ] **A4.3** Ejecutar `LoanSystem.DbMigrator` sobre BD limpia y sobre BD con `seeds/`: ambos deben pasar.

---

## 6. Fase B — Estructura del módulo

```
LoanSystem.Modules.Products.Core/
├── Contracts/                 (public)  LoanProductContracts.cs, ScheduleContracts.cs (incluye SimulateScheduleContracts actual),
│                                        RateCapContracts.cs, IScheduleCalculator.cs, IProductCatalog.cs,
│                                        IProductEligibilityEvaluator.cs, ILateFeeCalculator.cs, IntegrationEvents/*.cs
├── Domain/                    (internal)
│   ├── LoanProduct.cs, ProductTerms.cs (VO con todas las condiciones financieras), ProductVersion.cs,
│   ├── RegulatoryRateCap.cs, ProductsEnums.cs, ProductValidator.cs
│   └── Engine/                InterestRate.cs (existente, generalizado), DayCountMode.cs, ScheduleInput.cs, ScheduleResult.cs,
│                              IAmortizationStrategy.cs, FrenchStrategy.cs, FlatStrategy.cs, GermanStrategy.cs,
│                              AmericanStrategy.cs, ScheduleEngine.cs, DueDateGenerator.cs, IBusinessCalendar.cs,
│                              OpeningFeeCalculator.cs, TceaCalculator.cs, LateFeeCalculator.cs
├── Application/               (internal) *UseCase.cs, ScheduleCalculator.cs, ProductEligibilityEvaluator.cs,
│                                         ProductRateChangeApprovalHandler.cs, ProductsOptions.cs
└── Ports/
    ├── Inbound/               (public)  I*UseCase.cs
    └── Outbound/              ILoanProductRepository, IRegulatoryRateCapRepository, IHolidayProvider,
                               ICurrencyCatalog, IProductsUnitOfWork (existe), IProductsOutbox (existe)

LoanSystem.Modules.Products.Adapters/
├── Inbound/Http/              ProductsEndpoints.cs (simulate existente), LoanProductsEndpoints.cs, RateCapsEndpoints.cs
├── Inbound/Jobs/              RateCapComplianceJob.cs, ProductsJobs.cs
└── Outbound/Persistence/      ProductsDbContext.cs (editar), Configurations/*.cs, LoanProductRepository.cs,
                               RegulatoryRateCapRepository.cs, AdministrationHolidayProvider.cs, CurrencyCatalog.cs
```

- [ ] **B1** `ProductsEnums.cs`: enums C# con miembros **idénticos** a las etiquetas PG (`FRANCES`, `FLAT`, `ALEMAN`, `AMERICANO`, `DIARIA`, `SEMANAL`, `QUINCENAL`, `MENSUAL`, `DEDUCTIVA`, `FINANCIADA`, `SALDO_CAPITAL_VENCIDO`, `CUOTA_TOTAL_VENCIDA`, `ACTIVO`, `INACTIVO`, `CONSUMO`, …), mapeados con `NpgsqlNullNameTranslator` como en Security. El enum `AmortizationMethod` actual (`French = 1`) se reemplaza; el parser acepta `"FRENCH"` como alias de `FRANCES`.
- [ ] **B2** `ProductsDbContext.OnModelCreating`: `ApplyConfigurationsFromAssembly`, `MapEnum<...>()` de cada enum, y `ApprovalRequestConfiguration` de Common (necesario para PRO-09).
- [ ] **B3** (parcial: secciones `Engine` y `Cache` con `ValidateOnStart`; `Rules` y `RateCaps` llegan con PRO-01/PRO-08) `ProductsOptions` (sección `Products`, §11) con `ValidateOnStart()`.
- [x] **B4** Feriados: crear en **Administration** el contrato público `IHolidayCalendarQuery.GetHolidaysAsync(DateOnly from, DateOnly to, ct)` (implementado con ese nombre: devuelve solo feriados; los días de semana no hábiles los decide Products por configuración, D5) (con implementación que lee `holidays` y expande `is_recurring`). Products lo consume a través de su puerto `IHolidayProvider` (adaptador `AdministrationHolidayProvider`, caché en memoria 1 h). Nunca leer la tabla `holidays` desde Products.
- [ ] **B5** Prueba de arquitectura: ningún módulo referencia `Products.Core.Domain` (incluido `Engine`) ni `Products.Adapters`; solo `Contracts` y `Ports.Inbound`.
- [ ] **B6** Permisos en `Modules/Security/.../Domain/RolePermissions.cs`:

| Permiso | Admin | Analyst | Operator | Collector | Auditor | Client |
|---|:-:|:-:|:-:|:-:|:-:|:-:|
| `products.read` (nuevo) | ✅ | ✅ | ✅ | ✅ | ✅ | — |
| `products.manage` (existe) | ✅ | — | — | — | — | — |
| `products.rate_caps.manage` (nuevo) | ✅ | — | — | — | — | — |
| `approvals.products.rates` (existe) | ✅ | — | — | — | — | — |

> El cliente ve el catálogo público con `GET /api/loan-products/catalog` (solo productos activos y conformes, sin datos internos como DTI).

---

## 7. Ítems por requerimiento

### BE-PRO-02 — Motor de amortización (se implementa primero)

**Objetivo:** un motor puro que, dados capital, tasa, frecuencia, número de cuotas, método y (opcionalmente) fecha de desembolso y calendario, devuelva un cronograma que cuadre al céntimo.

#### Tasa del periodo

- [x] **PRO-02.1** Generalizar `InterestRate`:

```csharp
internal static decimal ToPeriodRate(decimal annualRateFraction, RateType rateType, int days);
// EFECTIVA: (1 + TEA)^(days/360) − 1
// NOMINAL:  TNA · days / 360
```

  Implementación sin punto flotante: reducir la fracción `days/360` con `g = mcd(days, 360)`; `n = 360/g`, `m = days/g`; resultado `= Pow(NthRoot(1 + TEA, n), m) − 1`. Para muchas fechas con días distintos, calcular **una vez** `q = NthRoot(1 + TEA, 360)` y luego `Pow(q, days) − 1` (clase `DailyGrowthFactor`).
  - `ToMonthlyRate(pct, type)` queda como `ToPeriodRate(pct/100, type, 30)`; las pruebas existentes no cambian.
- [x] **PRO-02.2** Días nominales por frecuencia (modo `NOMINAL_PERIODS`): `DIARIA = 1`, `SEMANAL = 7`, `QUINCENAL = 15`, `MENSUAL = 30`.
- [x] **PRO-02.3** `AmortizationMethodParser` y `PaymentFrequencyParser` aceptan las etiquetas PG; `FRENCH` → `FRANCES` (alias).

#### Entrada y salida del motor

- [x] **PRO-02.4** Tipos internos:

```csharp
internal sealed record ScheduleInput(
    Money Principal,                    // capital financiado (ya incluye comisión si es FINANCIADA)
    decimal AnnualRateFraction,         // 0.30 = 30 %
    RateType RateType,
    PaymentFrequency Frequency,
    int Installments,
    AmortizationMethod Method,
    DateOnly? DisbursementDate,         // null → NOMINAL_PERIODS ; valor → ACTUAL_360
    int? PaymentDay,                    // solo MENSUAL
    HolidayShift HolidayShift,          // NEXT | PREV
    IBusinessCalendar? Calendar,        // requerido si DisbursementDate tiene valor
    Money PeriodicFee);                 // cargo fijo por cuota (PRO-03)
```

> **Implementado (PRO-02):** para mantener el motor puro, `ScheduleInput` recibe `DueDates: IReadOnlyList<DueDatePair(Theoretical, Due)>`
> ya calculadas en lugar de `PaymentDay`/`HolidayShift`/`Calendar`. En PRO-06 el `DueDateGenerator` produce esa lista
> y el servicio de aplicación se la pasa al motor. `ScheduleResult` añade `Method`, `Frequency` y `EffectiveAnnualRate` (solo FLAT, D12).

```csharp
internal sealed record ScheduleLine(
    int Number, DateOnly? TheoreticalDueDate, DateOnly? DueDate, int Days,
    Money Principal, Money Interest, Money Fees, Money Total, Money RemainingBalance);

internal sealed record ScheduleResult(
    DayCountMode Mode, decimal ReferencePeriodRate, Money Principal, Money InstallmentAmount,
    Money TotalInterest, Money TotalFees, Money TotalPayable, IReadOnlyList<ScheduleLine> Lines);
```

- [x] **PRO-02.5** `ScheduleEngine.Build(ScheduleInput)` → `Result<ScheduleResult>`:
  1. Valida: `Principal > 0`, `Installments ≥ 1`, tasa ≥ 0, `Calendar` presente si hay fecha.
  2. Obtiene las fechas con `DueDateGenerator` (PRO-06) o, sin fecha, días fijos `d`.
  3. Delega en la estrategia del método.
  4. Suma cargos periódicos, totales y verifica el **invariante de cuadre**: `Σ Principal = input.Principal` y `RemainingBalance` final `= 0`. Si no se cumple → `Error.Failure("Products.ScheduleNotBalanced")` (nunca debería ocurrir; es una red de seguridad que debe tener prueba).

#### Las cuatro estrategias

En todas: `S₀ = P`; `i_k` = tasa del periodo `k` (con `d_k` días; en `NOMINAL_PERIODS` es la misma para todo `k`); `Interés_k = round(S_{k−1} · i_k)`; la **última cuota** toma `Capital_n = S_{n−1}` (absorbe el residuo).

| Método | Capital de la cuota `k < n` | Interés | Cuota |
|---|---|---|---|
| **FRANCES** (`FrenchStrategy`) | `R − Interés_k` | sobre saldo | `R` constante (la última puede diferir en céntimos) |
| **ALEMAN** (`GermanStrategy`) | `round(P / n)` | sobre saldo | decreciente |
| **AMERICANO** (`AmericanStrategy`) | `0` (en la última, `P`) | `round(P · i_k)` | solo interés; la última, `P + interés` |
| **FLAT** (`FlatStrategy`) | `C − Interés_k` (por diferencia) | `round(I / n)`, con `I = round(P · i · n)` e `i` en días nominales | `C = round((P + I) / n)` fija; la última toma `Capital_n = S_{n−1}` e `Interés_n = I − Σ Interés_{k<n}` |

- [x] **PRO-02.6** Cálculo de `R` en FRANCES:
  - `NOMINAL_PERIODS`: `R = round(P · i · (1+i)^n / ((1+i)^n − 1))`; si `i = 0`, `R = round(P / n)` (código actual).
  - `ACTUAL_360`: `R = round( P / Σ_{k=1..n} q^(−D_k) )`, donde `q = (1+TEA)^(1/360)` y `D_k` = días desde el desembolso hasta el vencimiento **teórico** `k`. Esto garantiza cuota nivelada aun con primer periodo irregular.
  - Si en algún `k < n` resulta `R − Interés_k < 0` → `Error.Validation("Products.ScheduleNotAmortizing")` (puede pasar con un primer periodo muy largo; los límites de PRO-06 lo evitan).
- [x] **PRO-02.7** FLAT: siempre usa días nominales para `i` (las fechas solo se usan para mostrar vencimientos). Exponer además `EffectiveAnnualRate` (TIR del flujo, ver TCEA) — se usa en D12.
- [x] **PRO-02.8** `SimulateScheduleUseCase` (endpoint anónimo actual): aceptar `method` = `FRANCES|FRENCH|FLAT|ALEMAN|AMERICANO`, `frequency` (default `MENSUAL`) e `installments` (alias de `termMonths`, que se mantiene). Siempre en `NOMINAL_PERIODS`. Límite duro: `installments ≤ 720`, `amount ≤ 10 000 000` → `400`.
- [x] **PRO-02.9** Rendimiento: 360 cuotas en `ACTUAL_360` en **< 5 ms** dentro del motor (BenchmarkDotNet opcional; como mínimo una prueba con `Stopwatch` y umbral holgado de 50 ms para CI).

- **CA-02.a** (CA-08.1) Francés, P = 10 000, TEA 30 %, 12 cuotas mensuales, sin fecha: cuotas 1–11 = **957,86**, última = **957,84**, `Σ capital = 10 000,00` (§12, V1).
- **CA-02.b** Alemán con los mismos datos: capital 833,33 en las cuotas 1–11 y **833,37** en la 12 (§12, V2).
- **CA-02.c** Americano: 11 cuotas de **221,04** y la última **10 221,04** (§12, V3).
- **CA-02.d** Flat: interés total **2 652,53**, cuotas 1–11 de **1 054,38** y última de **1 054,35** (§12, V4).
- **CA-02.e** Las pruebas actuales de `FrenchAmortizationScheduleTests` (885,62 con TEA 12 %) pasan sin cambios.
- **CA-02.f** Para todo método, frecuencia y `n ∈ {1, 2, 12, 36, 360}`, con montos aleatorios entre 100 y 1 000 000 (prueba por propiedades): `Σ capital = P` y saldo final `= 0`.

---

### BE-PRO-03 — Comisiones

- [x] **PRO-03.1** `OpeningFeeCalculator.Calculate(Money requested, decimal feeRateFraction, Money feeFixed)` → `round(requested · rate) + fixed`.
- [x] **PRO-03.2** Efecto según modo:

| Modo | Capital financiado (entra al motor) | Desembolso neto al cliente |
|---|---|---|
| `DEDUCTIVA` | `requested` | `requested − fee` (debe ser `> 0`, si no `422 Products.OpeningFeeExceedsAmount`) |
| `FINANCIADA` | `requested + fee` | `requested` |

- [x] **PRO-03.3** Cargo periódico: `periodic_fee` fijo por cuota → columna `Fees` de cada línea; **no** forma parte de la fórmula de amortización ni genera interés.
- [x] **PRO-03.4** (regla lista en `OpeningFeeCalculator.ValidateProductTerms`; la invoca `ProductValidator` en PRO-01) Validaciones en el producto: `opening_fee_rate` 0–20 %; `opening_fee_fixed ≥ 0`; `periodic_fee ≥ 0`; en `DEDUCTIVA`, la comisión calculada sobre `min_amount` debe ser menor que `min_amount`.

- **CA-03.a** P = 10 000, comisión 2 % `DEDUCTIVA` → `openingFee = 200,00`, `netDisbursement = 9 800,00`, cronograma sobre 10 000.
- **CA-03.b** Mismo caso `FINANCIADA` → cronograma sobre **10 200,00**, desembolso neto 10 000,00.
- **CA-03.c** `periodic_fee = 5` → cada cuota suma 5,00 en `Fees` y `TotalFees = 5 · n`.

---

### TCEA — Tasa de Costo Efectivo Anual (motor para BE-ORI-04)

- [x] **PRO-T.1** `TceaCalculator.Calculate(Money netDisbursement, IReadOnlyList<(int DaysFromDisbursement, Money Total)> flows)` → `decimal tceaFraction`.
  - Incógnita `q = (1 + TCEA)^(1/360)`. Resolver `f(q) = net − Σ Total_k · q^(−D_k) = 0` con **Newton** (`f'(q) = Σ Total_k · D_k · q^(−D_k−1)`), arrancando en `q₀ = (1 + TEA)^(1/360)`; si Newton no converge en 50 iteraciones, **bisección** en `q ∈ [1, 1.01]`. Tolerancia `|Δq| < 1e-18`. Solo potencias enteras (sin `Math.Pow`).
  - `TCEA = q^360 − 1`. Se informa en % con 2 decimales.
  - En `NOMINAL_PERIODS`, `D_k = k · d`.
- [x] **PRO-T.2** Flujos que entran: desembolso **neto** (t₀) y el **total** de cada cuota (capital + interés + cargos + seguro en F2). La mora y la penalidad no entran (son contingentes).
- [ ] **PRO-T.3** (motor listo: `LoanQuoteCalculator` devuelve `TeaFraction` y `TceaFraction`; falta exponerlo en `ScheduleQuote` con PRO-Q) Toda cotización del motor (`ScheduleQuote`, §10) incluye `teaPercentage` y `tceaPercentage`.

- **CA-T.a** Francés P = 10 000, TEA 30 %, 12 mensuales, comisión 2 % deductiva + cargo 5 por cuota → **TCEA = 36,54 %** (§12, V5).
- **CA-T.b** Sin comisiones ni cargos → `|TCEA − TEA| < 0,01` puntos porcentuales (el redondeo de céntimos da 29,9997 %).

---

### BE-PRO-06 — Día de pago y fechas de vencimiento

- [x] **PRO-06.1** `IBusinessCalendar` (puro): `bool IsBusinessDay(DateOnly)`. Implementación `BusinessCalendar(IReadOnlySet<DateOnly> holidays, IReadOnlySet<DayOfWeek> nonBusinessWeekdays)`. El servicio de aplicación la construye con `IHolidayProvider` para el rango `[desembolso, desembolso + plazo + 31 días]`.
- [x] **PRO-06.2** `DueDateGenerator.Generate(...)` → lista de `(TheoreticalDueDate, DueDate)`:

| Frecuencia | Vencimiento teórico `k` |
|---|---|
| `MENSUAL` con `payment_day` | `k = 1`: primera fecha con día = `payment_day` estrictamente posterior al desembolso y con al menos `MinDaysToFirstInstallment` (15) días; `k > 1`: mismo día de los meses siguientes. |
| `MENSUAL` sin `payment_day` | `desembolso.AddMonths(k)` (siempre desde la fecha base, no encadenado, para no arrastrar el 31 → 28). |
| `QUINCENAL` | `desembolso + 15·k` días |
| `SEMANAL` | `desembolso + 7·k` días |
| `DIARIA` | `k`-ésimo día **hábil** posterior al desembolso (ya es hábil; no se desplaza) |

- [x] **PRO-06.3** Desplazamiento (todas menos `DIARIA`): si el teórico no es hábil, `DueDate` = siguiente hábil (`NEXT`) o anterior hábil (`PREV`). Con `PREV`, si el resultado queda ≤ al vencimiento exigible anterior o ≤ al desembolso, se usa `NEXT`.
- [x] **PRO-06.4** Interés por días **teóricos** (D4): `d_k = teórico_k − teórico_{k−1}` (con `teórico_0 = desembolso`).
- [x] **PRO-06.5** Validaciones del producto: `payment_day` en 1–28 y solo si `MENSUAL ∈ allowed_frequencies` (si no, `400 Products.PaymentDayRequiresMonthly`); `holiday_shift_direction ∈ {NEXT, PREV}`.
- [x] **PRO-06.6** `MinDaysToFirstInstallment` configurable (default 15). Con `payment_day` el primer periodo queda entre 15 y 45 días.

- **CA-06.a** Desembolso **2026-11-25**, `payment_day = 5` → el 2026-12-05 está a 10 días (< 15) → primer vencimiento **2027-01-05** (41 días).
- **CA-06.b** Desembolso **2026-11-14**, `payment_day = 8`, feriado 2026-12-08 (martes) → teórico 2026-12-08; exigible **2026-12-09** con `NEXT` y **2026-12-07** con `PREV`; el interés de la cuota 1 se calcula por **24** días en ambos casos.
- **CA-06.c** Mismo desembolso, sin feriados, Francés TEA 30 %, 12 cuotas en `ACTUAL_360` → cuota **955,06**; cuota 1: interés **176,45** (24 días); cuota 2: interés **210,71** (31 días); última **955,02** (§12, V6).
- **CA-06.d** `DIARIA`: un desembolso en sábado genera la cuota 1 el lunes siguiente si el domingo no es hábil.
- **CA-06.e** Desembolso el 31-ene sin `payment_day`: vencimientos 28/29-feb, 31-mar, 30-abr (sin arrastre).

---

### BE-PRO-05 — Gracia y mora

- [ ] **PRO-05.1** `LateFeeCalculator.Calculate(LateFeeInput)`:

```csharp
internal sealed record LateFeeInput(
    Money Base,               // según mora_base, ver abajo
    decimal DailyRateFraction,// 0.001 = 0,1 % diario
    DateOnly DueDate,         // fecha EXIGIBLE (ya desplazada por feriado)
    DateOnly AsOf,            // fecha de pago o de liquidación
    int GraceDays);

// díasAtraso = max(0, AsOf − DueDate)
// si díasAtraso ≤ GraceDays → 0
// si no                    → round(Base × DailyRate × díasAtraso)   ← cuenta TODOS los días, no solo los posteriores a la gracia
```

- [ ] **PRO-05.2** Base de cálculo (la arma quien llama — Loans/Collections — con estas reglas, documentadas en el XML-doc del contrato):
  - `SALDO_CAPITAL_VENCIDO`: capital **pendiente** de la cuota vencida.
  - `CUOTA_TOTAL_VENCIDA`: capital + interés + cargos **pendientes** de la cuota vencida. **Nunca** incluye mora ya devengada (sin anatocismo).
- [ ] **PRO-05.3** Contrato público `ILateFeeCalculator` (§10) para que Loans y Collections no dupliquen la fórmula.
- [ ] **PRO-05.4** Validaciones del producto: `grace_days` 0–30; `mora_rate_per_day ≥ 0`; `allows_anatocismo = true` → `422 Products.CompoundingNotAllowed` (salvo que `Products:Rules:AllowCompounding = true`).
- [ ] **PRO-05.5** Equivalente anual de la mora para comparar con el tope (PRO-08): `TEA_mora = (1 + tasaDiaria)^360 − 1` (criterio conservador; ver §15 R2).

- **CA-05.a** (CA-09.1) Vencimiento 2026-12-10, gracia 3, base 736,82, tasa 0,1 % diaria: pago el 12 → **0,00**; el 13 → **0,00** (3 días = gracia); el 14 → **2,95** (4 días).
- **CA-05.b** Liquidar el día 20 da lo mismo que `base × tasa × 10` redondeado una sola vez (no la suma de 10 devengos redondeados).

---

### BE-PRO-01 — Catálogo de productos (CRUD)

#### Dominio

- [ ] **PRO-01.1** `ProductTerms` (Value Object inmutable con todas las condiciones financieras) + `ProductValidator.Validate(ProductTerms, ProductsOptions)` → `Result` que acumula **todos** los errores (no solo el primero) en `extensions.errors`:

| Campo (API) | Regla | Código de error |
|---|---|---|
| `code` | `^[A-Z0-9_]{3,30}$`, único | `Products.InvalidCode` / `Products.CodeAlreadyExists` (409) |
| `name` | 3–150 caracteres | `Products.InvalidName` |
| `currency` | existe y activa en `currencies` | `Products.CurrencyNotSupported` |
| `minAmount`, `maxAmount` | `minAmount > 0`, `maxAmount ≥ minAmount`, máx. 2 decimales | `Products.InvalidAmountRange` (CA-07.2) |
| `minTerm`, `maxTerm` | `1 ≤ minTerm ≤ maxTerm ≤ 720` | `Products.InvalidTermRange` |
| `allowedFrequencies` | no vacío, sin repetidos, valores del enum | `Products.InvalidFrequencies` |
| `amortizationMethod` | `FRANCES`, `FLAT`, `ALEMAN`, `AMERICANO` | `Products.InvalidMethod` |
| `rateType` | solo `EFECTIVA` (D9) | `Products.NominalRateNotAllowed` |
| `annualRatePercentage` | `> 0` y `≤ 1000`; además ≤ tope BCRP (PRO-08) | `Products.InvalidRate` / `Products.RateCapExceeded` |
| `openingFee.*` | PRO-03.4 | `Products.InvalidOpeningFee` / `Products.OpeningFeeExceedsAmount` |
| `graceDays`, `moraRatePerDayPercentage`, `moraBase` | PRO-05.4 | `Products.InvalidLateFeeSettings` |
| `allowsCompounding` | `false` (D10) | `Products.CompoundingNotAllowed` (422) |
| `prepaymentPenaltyRatePercentage` | `0` (D10) | `Products.PrepaymentPenaltyNotAllowed` (422) |
| `eligibility.minAge/maxAge` | `18 ≤ minAge ≤ maxAge ≤ 85` | `Products.InvalidAgeRange` |
| `eligibility.minMonthlyIncome` | `≥ 0` | `Products.InvalidIncome` |
| `eligibility.minWorkSeniorityMonths` | 0–600 | `Products.InvalidSeniority` |
| `paymentDay`, `holidayShift` | PRO-06.5 | `Products.PaymentDayRequiresMonthly` / `Products.InvalidPaymentDay` |
| `dti.maxAutoPercentage/maxManualPercentage` | `0 < auto ≤ manual ≤ 100` | `Products.InvalidDtiThresholds` |
| `regulatorySegment` | `CONSUMO`, `MICROEMPRESA`, `PEQUENA_EMPRESA` | `Products.InvalidSegment` |

- [ ] **PRO-01.2** `LoanProduct` (aggregate root): `Create(code, name, description, ProductTerms, createdBy, clock)` crea también `ProductVersion` 1; `Rename(name, description)`; `Activate()`, `Deactivate()`; `ApplyNewVersion(ProductTerms, reason, approvalRequestId?, by, clock)` (PRO-09).

#### Casos de uso y endpoints

| Método | Ruta | Política | Descripción | Respuestas |
|---|---|---|---|---|
| `POST` | `/api/loan-products` | Admin | Crear producto (activo) | `201` + `Location` · `400` · `409` · `422` |
| `GET` | `/api/loan-products?status=&currency=&q=&page=&pageSize=` | `products.read` | Listado paginado | `200 PagedResult` |
| `GET` | `/api/loan-products/{id}` | `products.read` | Producto + versión vigente + estado de cumplimiento | `200` · `404` |
| `PATCH` | `/api/loan-products/{id}` | Admin | Editar (ver PRO-09: puede devolver `200` o `202`) | `200` · `202` · `400` · `404` · `409` · `422` |
| `POST` | `/api/loan-products/{id}/activate` | Admin | Activar (re-valida tope BCRP) | `200` · `422` |
| `POST` | `/api/loan-products/{id}/deactivate` | Admin | Desactivar (no afecta solicitudes ni préstamos existentes) | `200` |
| `GET` | `/api/loan-products/catalog` | Client y backoffice | Productos `ACTIVO` y `COMPLIANT`, datos públicos | `200` |

- [ ] **PRO-01.3** `CreateLoanProductUseCase`: validar (PRO-01.1) → validar tope BCRP (PRO-08) → crear producto + versión 1 → `compliance_status = COMPLIANT` → `_audit.SetAction("CREATE", "loan_products")` → outbox `LoanProductCreated`. Traducir la violación de `uq_loan_products_code` a `409`.
- [ ] **PRO-01.4** Los productos **no se borran** (no hay `DELETE`).
- [ ] **PRO-01.5** Concurrencia optimista con `version` + cabecera `If-Match` en `PATCH` → `409 Products.ConcurrencyConflict`.

```csharp
public sealed record CreateLoanProductRequest(
    string Code, string Name, string? Description, string Currency,
    decimal MinAmount, decimal MaxAmount, int MinTerm, int MaxTerm,
    IReadOnlyList<string> AllowedFrequencies, string AmortizationMethod,
    string RateType, decimal AnnualRatePercentage,
    OpeningFeeDto OpeningFee, decimal PeriodicFee,
    int GraceDays, decimal MoraRatePerDayPercentage, string MoraBase, bool AllowsCompounding,
    decimal PrepaymentPenaltyRatePercentage,
    EligibilityDto Eligibility, int? PaymentDay, string HolidayShift,
    DtiThresholdsDto Dti, string RegulatorySegment);

public sealed record OpeningFeeDto(decimal RatePercentage, decimal FixedAmount, string Mode);
public sealed record EligibilityDto(int MinAge, int MaxAge, decimal MinMonthlyIncome, int MinWorkSeniorityMonths);
public sealed record DtiThresholdsDto(decimal MaxAutoPercentage, decimal MaxManualPercentage);

public sealed record LoanProductResponse(
    Guid Id, string Code, string Name, string? Description, string Status, string ComplianceStatus,
    int Version, Guid CurrentVersionId, int CurrentVersionNumber, ProductTermsDto Terms,
    decimal? EffectiveTeaPercentage,   // solo FLAT (D12)
    DateTimeOffset CreatedAt);
```

- **CA-01.a** (CA-07.1) Crear "Microcrédito Diario" válido → `201`, `status = ACTIVO`, versión 1 creada.
- **CA-01.b** (CA-07.2) `minAmount = 5000`, `maxAmount = 1000` → `400 Products.InvalidAmountRange`.
- **CA-01.c** Un request con tres errores devuelve los tres en `extensions.errors`.
- **CA-01.d** Código repetido → `409`.
- **CA-01.e** Desactivar un producto no modifica `loans` ni `loan_applications` existentes y lo saca de `/catalog`.

---

### BE-PRO-08 — Topes de tasa del BCRP

- [ ] **PRO-08.1** `RegulatoryRateCap` + `IRegulatoryRateCapRepository.FindApplicableAsync(currency, segment, DateOnly date)`.
- [ ] **PRO-08.2** `RateCapPolicy.Check(ProductTerms terms, RegulatoryRateCap? cap)` → `Result`:
  - TEA a comparar: la `annual_rate` del producto; en **FLAT**, la TEA efectiva real con `max_term` (D12).
  - Mora a comparar: `(1 + mora_diaria)^360 − 1` (PRO-05.5).
  - Si `TEA > max_compensatory_tea` → `422 Products.RateCapExceeded` (`extensions`: `cap`, `actual`, `sourceReference`). Equivale al `ERR_USURY_CAP_EXCEEDED` de BE-PRO-12.
  - Si la mora supera `max_moratory_tea` → `422 Products.MoratoryRateCapExceeded`.
  - Sin tope registrado: si `Products:RateCaps:RequireCap = true` (obligatorio en Production) → `422 Products.RateCapNotConfigured`; si `false` → pasa con `compliance_status = UNKNOWN` y log `Warning`.
- [ ] **PRO-08.3** Puntos de validación: crear producto, solicitar cambio de tasa, **aprobar** cambio de tasa (re-valida), activar producto, y cada cotización con producto (`IScheduleCalculator`).
- [ ] **PRO-08.4** Endpoints:

| Método | Ruta | Política |
|---|---|---|
| `GET` | `/api/regulatory-rate-caps?currency=&segment=` | Admin, Analyst, Auditor |
| `GET` | `/api/regulatory-rate-caps/current?currency=PEN&segment=CONSUMO` | `products.read` |
| `POST` | `/api/regulatory-rate-caps` | Admin (`products.rate_caps.manage`) |

  Los topes son **append-only**: no hay `PUT`/`DELETE`. Para corregir, se registra uno nuevo; el anterior se cierra fijando `valid_to = nuevo.valid_from − 1` en la misma transacción (única actualización permitida, y solo si `valid_to` era `NULL`). Solape → la `EXCLUDE` de BD falla → traducir a `409 Products.RateCapOverlap`.
- [ ] **PRO-08.5** `RateCapComplianceJob` (Quartz, `[DisallowConcurrentExecution]`, clustering BE-NF-09): diario 00:30 `America/Lima` **y** disparado al registrar un tope. Recorre productos `ACTIVO`, recalcula cumplimiento con el tope vigente a hoy y actualiza `compliance_status` + `compliance_checked_at`. Cada producto que pasa a `NON_COMPLIANT` encola `LoanProductBecameNonCompliant` (Notifications avisa a Admin). Un producto `NON_COMPLIANT` **no** admite solicitudes nuevas (PRO-07) pero sus préstamos siguen intactos.

- **CA-08.a** Tope PEN/CONSUMO = 100 % TEA; crear producto con TEA 120 % → `422 Products.RateCapExceeded`.
- **CA-08.b** Producto FLAT con tasa de entrada 30 %, 12 cuotas máximas, tope 50 % → `422` (su TEA real es 56,80 %).
- **CA-08.c** Registrar un tope de 60 % vigente desde mañana con un producto activo al 70 % → tras el job, `compliance_status = NON_COMPLIANT` y evento en el outbox.
- **CA-08.d** Dos topes PEN/CONSUMO con vigencias que se cruzan → `409 Products.RateCapOverlap`.

---

### BE-PRO-09 — Versionado y snapshot (con cuatro ojos para tasas)

- [ ] **PRO-09.1** Clasificación de campos al editar (`PATCH /api/loan-products/{id}`):

| Tipo de cambio | Campos | Efecto | Respuesta |
|---|---|---|---|
| Descriptivo | `name`, `description` | Actualiza cabecera, sin versión nueva | `200` |
| Operativo | límites de monto/plazo, frecuencias, elegibilidad, `paymentDay`, `holidayShift`, DTI | Versión nueva inmediata | `200` |
| Sensible (D7) | `annualRatePercentage`, `moraRatePerDayPercentage`, `moraBase`, `graceDays`, `openingFee`, `periodicFee`, `prepaymentPenaltyRatePercentage`, `amortizationMethod`, `regulatorySegment` | Crea `ApprovalRequest` `PRODUCT_RATE_CHANGE`; **no** cambia nada todavía | `202` + `{ approvalRequestId }` |

  Si el `PATCH` mezcla campos sensibles con otros, **todo** el cambio va a aprobación (una sola versión resultante). `changeReason` (≥ 10 caracteres) obligatorio en cambios operativos y sensibles.
- [ ] **PRO-09.2** Solicitud de aprobación: `ApprovalRequest.Submit(new ApprovalSubmission(OperationType: PRODUCT_RATE_CHANGE, Module: "Products", TargetEntity: "loan_products", TargetId: productId, PayloadJson: <ProductTerms propuestos>, TargetSnapshotJson: <versión vigente + current_version_id>, Reason: changeReason, ...), requestedBy, clock, ttl)`; guardar con `SaveApprovalChangesAsync` (traduce el pendiente duplicado a `409 Approvals.DuplicatePending`). Seguir como referencia `RequestUserRoleChangeUseCase` y [`ANALISIS_09_CUATRO_OJOS.md`](../backend/docs/ANALISIS_09_CUATRO_OJOS.md).
- [ ] **PRO-09.3** `ProductRateChangeApprovalHandler : IApprovalHandler` (`OperationType = PRODUCT_RATE_CHANGE`), en **una** transacción del `ProductsDbContext`:
  1. Si `current_version_id` ya no es el del snapshot → `409 Products.VersionChangedSinceRequest` (alguien editó entre medio).
  2. Re-validar `ProductValidator` y tope BCRP vigente **hoy**.
  3. Crear la versión `n+1` con `approval_request_id`, actualizar la copia en `loan_products`, marcar la aprobación como `APPROVED`.
  4. Outbox `LoanProductVersionCreated`.
- [ ] **PRO-09.4** `GET /api/loan-products/{id}/versions` (Admin, Analyst, Auditor) → lista con número, fecha, autor, motivo, aprobación y **diff** contra la anterior (`[{ field, old, new }]`).
- [ ] **PRO-09.5** Contrato `IProductCatalog.GetVersionAsync(versionId)` → `ProductTermsSnapshot` (todas las condiciones). Origination guarda `product_version_id` en la solicitud al crearla; Loans copia el snapshot a `loans` al desembolsar y **nunca** vuelve a leer el producto para recalcular.
- [ ] **PRO-09.6** Solicitudes en curso: siguen con la versión con la que se crearon (decisión explícita; ver §15 R4).

- **CA-09.a** (RNF-06) Préstamo desembolsado con versión 1 (TEA 30 %); se aprueba TEA 25 % → el préstamo conserva 30 % y su cronograma no cambia; una solicitud nueva usa versión 2.
- **CA-09.b** `PATCH` con nueva TEA → `202`; el producto sigue en 30 % hasta que otro usuario aprueba; el mismo usuario no puede aprobar (`403`, regla de Security).
- **CA-09.c** Dos cambios de tasa pendientes sobre el mismo producto → el segundo `409 Approvals.DuplicatePending`.
- **CA-09.d** Un `UPDATE` directo en `loan_product_versions` falla en BD.

---

### BE-PRO-07 — Elegibilidad

- [ ] **PRO-07.1** Contrato público:

```csharp
public interface IProductEligibilityEvaluator
{
    Task<Result<EligibilityResult>> EvaluateAsync(EligibilityRequest request, CancellationToken ct);
}
public sealed record EligibilityRequest(
    Guid ProductId, string PersonType, DateOnly? BirthDate, decimal MonthlyNetIncome,
    int WorkSeniorityMonths, decimal RequestedAmount, int Installments, string Frequency, DateOnly ApplicationDate);
public sealed record EligibilityResult(bool IsEligible, Guid ProductVersionId, IReadOnlyList<EligibilityFailure> Failures);
public sealed record EligibilityFailure(string Code, string Message, string? Expected, string? Actual);
```

- [ ] **PRO-07.2** Reglas (se evalúan **todas** y se devuelven juntas):

| Código | Regla |
|---|---|
| `PRODUCT_INACTIVE` | `status = INACTIVO` |
| `PRODUCT_NOT_COMPLIANT` | `compliance_status = NON_COMPLIANT` |
| `AMOUNT_OUT_OF_RANGE` | monto fuera de `[min_amount, max_amount]` |
| `TERM_OUT_OF_RANGE` | cuotas fuera de `[min_term, max_term]` |
| `FREQUENCY_NOT_ALLOWED` | frecuencia no incluida en `allowed_frequencies` |
| `AGE_BELOW_MIN` | `NATURAL`: edad a `ApplicationDate` < `min_age` |
| `AGE_ABOVE_MAX_AT_MATURITY` | `NATURAL`: edad en la fecha del **último vencimiento** > `max_age` (§15 R5) |
| `INCOME_BELOW_MIN` | `MonthlyNetIncome < min_monthly_income` |
| `SENIORITY_BELOW_MIN` | `WorkSeniorityMonths < min_work_seniority_months` |

  Para `JURIDICA` no aplican las reglas de edad. La edad se calcula con años cumplidos (`DateOnly`), no con días/365.
- [ ] **PRO-07.3** Origination (BE-ORI-01/02) traduce `IsEligible = false` a `400` con la lista de fallos. Products no conoce la solicitud.
- [ ] **PRO-07.4** Los umbrales de DTI (`max_dti_auto`, `max_dti_manual`) se exponen en `ProductTermsSnapshot` para BE-ORI-06; Products **no** calcula DTI.

- **CA-07.a** Cliente de 68 años, producto `max_age = 70`, 36 cuotas mensuales → `AGE_ABOVE_MAX_AT_MATURITY` (tendría 71).
- **CA-07.b** Ingreso 900 con mínimo 1 000 y antigüedad 2 meses con mínimo 6 → dos fallos en la misma respuesta.

---

### Cotización con producto (une PRO-02, 03, 06, 08 y TCEA)

- [ ] **PRO-Q.1** `ScheduleCalculator : IScheduleCalculator` (servicio de aplicación): carga la versión del producto (caché 5 min), valida límites (`AMOUNT_OUT_OF_RANGE`, etc. → `400`), valida tope BCRP, calcula comisión, arma el calendario, llama al motor en `ACTUAL_360` con `DisbursementDate` (default `IClock.LimaToday`), calcula TCEA y devuelve `ScheduleQuote` (§10).
- [ ] **PRO-Q.2** Endpoint `POST /api/loan-products/{id}/quote` (backoffice y Client): `{ amount, installments, frequency, disbursementDate? }` → `ScheduleQuote`. Es lo que usa el simulador del portal (FE-CP-06) y lo que Origination llama en `POST /api/loan-applications/simulate` (BE-ORI-03).
- [ ] **PRO-Q.3** Presupuesto de tiempo: < 50 ms p95 (BE-NF-01) con caché caliente.

---

### BE-PRO-04 — Comisión de administración y seguro separados (F2, solo diseño)

- [ ] **PRO-04.1** Migración futura: reemplazar `periodic_fee` por `admin_fee_fixed` (por cuota) + `insurance_type` (`NONE | FIXED | RATE_ON_BALANCE`) + `insurance_value`.
- [ ] **PRO-04.2** `RATE_ON_BALANCE`: `Seguro_k = round(S_{k−1} · tasa_seguro_periodo)`, columna `Insurance` separada en `ScheduleLine`; entra en `Total` y en la TCEA. No altera el capital ni el interés.
- [ ] **PRO-04.3** El cronograma del préstamo guarda las columnas por separado (coordinar con Loans).
- [ ] **PRO-04.4** La integración del desgravamen en la anualidad (cuota total constante) es **BE-PRO-10 (V2)**, no este ítem.

---

## 8. Catálogo de errores

Prefijo `Products.`; en `ProblemDetails` con `extensions.code`. Los validadores que acumulan errores los devuelven en `extensions.errors: [{ code, field, message }]`.

| Código | `ErrorType` | HTTP | Req. |
|---|---|---|---|
| `Products.NotFound` / `Products.VersionNotFound` | NotFound | 404 | 01, 09 |
| `Products.InvalidCode` | Validation | 400 | 01 |
| `Products.CodeAlreadyExists` | Conflict | 409 | 01 |
| `Products.InvalidName` | Validation | 400 | 01 |
| `Products.CurrencyNotSupported` | Validation | 400 | 01 |
| `Products.InvalidAmountRange` | Validation | 400 | 01 |
| `Products.InvalidTermRange` | Validation | 400 | 01 |
| `Products.InvalidFrequencies` / `Products.InvalidFrequency` | Validation | 400 | 01, 02 |
| `Products.InvalidMethod` | Validation | 400 | 02 (existe) |
| `Products.InvalidRateType` / `Products.NominalRateNotAllowed` | Validation | 400 | 02 (existe), 01 |
| `Products.InvalidRate` / `Products.InvalidAmount` / `Products.InvalidTerm` | Validation | 400 | 02 (existen) |
| `Products.InvalidOpeningFee` | Validation | 400 | 03 |
| `Products.OpeningFeeExceedsAmount` | Unprocessable | 422 | 03 |
| `Products.InvalidLateFeeSettings` | Validation | 400 | 05 |
| `Products.CompoundingNotAllowed` | Unprocessable | 422 | 05 |
| `Products.PrepaymentPenaltyNotAllowed` | Unprocessable | 422 | 01 |
| `Products.InvalidAgeRange` / `Products.InvalidIncome` / `Products.InvalidSeniority` | Validation | 400 | 07 |
| `Products.InvalidPaymentDay` / `Products.PaymentDayRequiresMonthly` | Validation | 400 | 06 |
| `Products.InvalidDtiThresholds` / `Products.InvalidSegment` | Validation | 400 | 01 |
| `Products.ScheduleNotAmortizing` | Validation | 400 | 02 |
| `Products.ScheduleNotBalanced` | Failure | 500 | 02 (no debería ocurrir) |
| `Products.InvalidPeriodicFee` | Validation | 400 | 02, 03 |
| `Products.InvalidDueDates` | Validation | 400 | 02, 06 (fechas mal armadas por quien llama al motor) |
| `Products.QuoteOutOfRange` | Validation | 400 | Q |
| `Products.RateCapExceeded` (= `ERR_USURY_CAP_EXCEEDED`) | Unprocessable | 422 | 08 |
| `Products.MoratoryRateCapExceeded` | Unprocessable | 422 | 08 |
| `Products.RateCapNotConfigured` | Unprocessable | 422 | 08 |
| `Products.RateCapOverlap` | Conflict | 409 | 08 |
| `Products.ConcurrencyConflict` | Conflict | 409 | 01 |
| `Products.VersionChangedSinceRequest` | Conflict | 409 | 09 |
| `Products.ChangeReasonRequired` | Validation | 400 | 09 |

---

## 9. Resumen de endpoints y permisos

| Método | Ruta | Admin | Analyst | Operator | Collector | Auditor | Client | Anónimo | Req. |
|---|---|:-:|:-:|:-:|:-:|:-:|:-:|:-:|---|
| POST | `/api/products/simulate` (existente) | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | 02 |
| POST | `/api/loan-products` | ✅ | | | | | | | 01 |
| GET | `/api/loan-products` | ✅ | ✅ | ✅ | ✅ | ✅ | | | 01 |
| GET | `/api/loan-products/catalog` | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | | 01 |
| GET | `/api/loan-products/{id}` | ✅ | ✅ | ✅ | ✅ | ✅ | | | 01 |
| PATCH | `/api/loan-products/{id}` | ✅ | | | | | | | 01, 09 |
| POST | `/api/loan-products/{id}/activate` · `/deactivate` | ✅ | | | | | | | 01 |
| GET | `/api/loan-products/{id}/versions` | ✅ | ✅ | | | ✅ | | | 09 |
| POST | `/api/loan-products/{id}/quote` | ✅ | ✅ | ✅ | | | ✅ | | Q |
| GET | `/api/regulatory-rate-caps` | ✅ | ✅ | | | ✅ | | | 08 |
| GET | `/api/regulatory-rate-caps/current` | ✅ | ✅ | ✅ | ✅ | ✅ | | | 08 |
| POST | `/api/regulatory-rate-caps` | ✅ | | | | | | | 08 |
| POST | `/api/approvals/{id}/approve` (Security, existente) | ✅ (otro usuario) | | | | | | | 09 |

- [ ] Cada endpoint con `.WithName`, `.WithSummary("… (BE-PRO-XX)")`, `.Produces<T>()` y `.ProducesProblem(...)` (BE-NF-05).
- [ ] `Idempotency-Key` obligatorio en `POST /api/loan-products` y `POST /api/regulatory-rate-caps`.
- [ ] Rate limiting en `/api/products/simulate` (anónimo): 30 req/min por IP (`Microsoft.AspNetCore.RateLimiting`).

---

## 10. Contratos públicos y eventos

### Contratos (en `Products.Core/Contracts/`, registrados en DI por `AddProductsModule`)

| Interfaz | Método | Lo usa |
|---|---|---|
| `IScheduleCalculator` | `Task<Result<ScheduleQuote>> QuoteAsync(QuoteRequest, ct)` — con producto; `Result<ScheduleQuote> Calculate(ScheduleTermsInput)` — con un snapshot ya congelado (para Loans, sin leer el producto) | Origination (simulación, solicitud), Loans (cronograma al desembolsar, reestructuración) |
| `IProductCatalog` | `GetVersionAsync(versionId)`, `GetCurrentVersionAsync(productId)` → `ProductTermsSnapshot` | Origination, Loans |
| `IProductEligibilityEvaluator` | `EvaluateAsync(EligibilityRequest)` | Origination |
| `ILateFeeCalculator` | `Money Calculate(LateFeeRequest)` (puro) | Loans, Collections, Payments |
| `IRateCapChecker` | `CheckAsync(currency, segment, teaPct, moraDailyPct, date)` | Loans en V2 (BE-PRO-12, contrato) |

```csharp
public sealed record ScheduleQuote(
    Guid? ProductVersionId, string Currency, string Method, string Frequency, string DayCountMode,
    decimal RequestedAmount, decimal OpeningFee, string OpeningFeeMode, decimal FinancedPrincipal, decimal NetDisbursement,
    decimal InstallmentAmount, decimal TotalInterest, decimal TotalFees, decimal TotalPayable,
    decimal TeaPercentage, decimal TceaPercentage, decimal PeriodRate,
    DateOnly? DisbursementDate, IReadOnlyList<QuoteInstallment> Installments);

public sealed record QuoteInstallment(
    int Number, DateOnly? TheoreticalDueDate, DateOnly? DueDate, int Days,
    decimal Principal, decimal Interest, decimal Fees, decimal Total, decimal RemainingBalance);
```

### Eventos de integración (`Contracts/IntegrationEvents/`, registrados en `IntegrationEventTypeRegistry`)

| Evento | Payload | Consumidores |
|---|---|---|
| `LoanProductCreatedIntegrationEvent` | `ProductId, Code, VersionId` | Reports |
| `LoanProductVersionCreatedIntegrationEvent` | `ProductId, VersionId, VersionNumber, ChangedFields[]` | Origination (invalida cachés), Reports |
| `LoanProductStatusChangedIntegrationEvent` | `ProductId, Status` | Origination, portal |
| `LoanProductBecameNonCompliantIntegrationEvent` | `ProductId, CapId, ActualTea, CapTea` | Notifications (alerta a Admin) |

---

## 11. Configuración (`appsettings`)

```jsonc
"Products": {
  "Engine": {
    "MaxInstallments": 720,
    "MaxSimulationAmount": 10000000,
    "MinDaysToFirstInstallment": 15,
    "NonBusinessWeekdays": [ "Sunday" ]
  },
  "Rules": {
    "AllowCompounding": false,          // D10 — anatocismo
    "AllowPrepaymentPenalty": false,    // D10
    "AllowNominalRateProducts": false,  // D9
    "MaxOpeningFeeRatePercentage": 20,
    "MaxGraceDays": 30
  },
  "RateCaps": {
    "RequireCap": true,                 // false solo en Development
    "ComplianceJobCron": "0 30 0 * * ?" // 00:30 America/Lima
  },
  "Cache": {
    "ProductVersionMinutes": 5,
    "HolidaysMinutes": 60
  }
}
```

---

## 12. Valores de referencia para pruebas

Calculados con aritmética decimal de 40 dígitos y redondeo half-up por componente, replicando las reglas de este plan. **Usarlos tal cual en las pruebas.**

**Tasa del periodo para TEA 30 %** (`(1,30)^(d/360) − 1`):

| Frecuencia | d | Tasa del periodo |
|---|---|---|
| DIARIA | 1 | 0,07290553 % |
| SEMANAL | 7 | 0,51145623 % |
| QUINCENAL | 15 | 1,09918153 % |
| MENSUAL | 30 | 2,21044506 % |

**Caso base:** P = 10 000,00 · TEA 30 % · 12 cuotas mensuales.

| # | Escenario | Valores esperados |
|---|---|---|
| V1 | FRANCES, `NOMINAL_PERIODS` | Cuota 1–11 = **957,86**; cuota 1: capital 736,82 + interés 221,04; cuota 12: capital 937,13 + interés 20,71 = **957,84**; interés total **1 494,30** |
| V2 | ALEMAN, `NOMINAL_PERIODS` | Cuota 1: 833,33 + 221,04 = **1 054,37**; cuota 12: 833,37 + 18,42 = **851,79**; interés total **1 436,76** |
| V3 | AMERICANO, `NOMINAL_PERIODS` | Cuotas 1–11: **221,04** (solo interés); cuota 12: **10 221,04**; interés total **2 652,48** |
| V4 | FLAT, `NOMINAL_PERIODS` | Interés total **2 652,53**; cuotas 1–11 = **1 054,38** (capital 833,34 + interés 221,04); cuota 12: capital 833,26 + interés 221,09 = **1 054,35**; **TEA efectiva real 56,80 %** |
| V5 | V1 + comisión 2 % `DEDUCTIVA` + cargo 5,00 por cuota | Comisión 200,00; desembolso neto 9 800,00; **TCEA 36,54 %** |
| V6 | FRANCES, `ACTUAL_360`, desembolso 2026-11-14, `payment_day = 8`, sin feriados | Cuota **955,06**; cuota 1 (2026-12-08, 24 días): capital 778,61 + interés **176,45**; cuota 2 (2027-01-08, 31 días): 744,35 + **210,71**; cuota 12 (2027-11-08): 933,69 + 21,33 = **955,02**; interés total **1 460,68** |
| V7 | Mora (CA-09.1): base 736,82, 0,1 % diario, gracia 3, vence 2026-12-10 | Día 12 → 0,00; día 13 → 0,00; día 14 → **2,95** |
| V8 | Equivalente anual de mora 0,1 % diario | `(1,001)^360 − 1` = **43,31 %** |
| V9 | FRANCES semanal, P = 10 000, TEA 30 %, 52 cuotas, `NOMINAL_PERIODS` | Cuota **219,50** |
| V10 | Existente (DECISION_01): TEA 12 %, 12 mensuales | Cuota **885,62** (TNA 12 % → 888,49) |

---

## 13. Plan de pruebas

| Proyecto | Qué probar | Ítems |
|---|---|---|
| `LoanSystem.Domain.UnitTests` | `InterestRateTests` (ampliar: `ToPeriodRate` para 1/7/15/30 días, tabla §12; ida y vuelta `(1+i_d)^(360/d) − 1 = TEA` con error < 1e-18); `FrenchStrategyTests`, `GermanStrategyTests`, `AmericanStrategyTests`, `FlatStrategyTests` (V1–V4, V9); `ScheduleEnginePropertyTests` (CA-02.f, con FsCheck o datos generados con semilla fija); `DueDateGeneratorTests` (CA-06.a–e, años bisiestos, fin de mes, `PREV` que choca con el vencimiento anterior); `OpeningFeeCalculatorTests` (CA-03.*); `TceaCalculatorTests` (V5, CA-T.b, convergencia con tasas altas 300 %); `LateFeeCalculatorTests` (V7, CA-05.b); `ProductValidatorTests` (una prueba por fila de la tabla PRO-01.1, y acumulación de errores); `RateCapPolicyTests` (CA-08.a/b, V8); `EligibilityRulesTests` (CA-07.*, cumpleaños el día del vencimiento) | 02, 03, 05, 06, 07, 08, T |
| `LoanSystem.Application.UnitTests` | `CreateLoanProductUseCaseTests` (201, 409, 422 por tope), `UpdateLoanProductUseCaseTests` (clasificación 200/202 de PRO-09.1, `If-Match`), `ProductRateChangeApprovalHandlerTests` (CA-09.a, `VersionChangedSinceRequest`, re-validación de tope), `ScheduleCalculatorTests` (límites del producto, V6 con calendario falso), `SimulateScheduleUseCaseTests` (ampliar: métodos y frecuencias nuevas, compatibilidad con `termMonths`) | 01, 08, 09, Q |
| `LoanSystem.Adapters.UnitTests` | `LoanProductsEndpointsAuthorizationTests` (matriz §9), `AdministrationHolidayProviderTests` (feriados recurrentes), serialización de porcentajes ↔ fracciones | §9, B4 |
| `LoanSystem.Architecture.Tests` | Aislamiento de Products; `Engine` sin dependencias de EF, `IClock` ni I/O; sin `double`/`float` | B5, D14 |
| Integración (PostgreSQL real, Testcontainers) | Migraciones 24–27 en BD limpia y con seeds; backfill de versión 1; trigger inmutable (CA-09.d); `EXCLUDE` de topes (CA-08.d); `RateCapComplianceJob` (CA-08.c); flujo completo de cuatro ojos con dos usuarios (CA-09.b/c) | A, 08, 09 |
| Rendimiento | Motor 360 cuotas `ACTUAL_360` < 5 ms; `POST /quote` p95 < 50 ms con caché caliente | PRO-02.9, Q.3 |
| Postman / contrato | Colección `Products` con un request por endpoint, los ejemplos V1–V6 como pruebas de respuesta y los errores del §8 | §9 |

---

## 14. Definition of Done

Un requerimiento BE-PRO-XX está terminado cuando:

- [ ] Todos sus ítems `PRO-XX.Y` están marcados.
- [ ] Cada criterio de aceptación tiene al menos una prueba automatizada en verde, usando los valores de §12 cuando aplique.
- [ ] Las pruebas existentes (`InterestRateTests`, `FrenchAmortizationScheduleTests`, `SimulateScheduleUseCaseTests`) siguen pasando **sin modificar sus valores esperados**.
- [ ] `dotnet build` sin warnings nuevos; `dotnet test` en verde, incluidas las de arquitectura.
- [ ] Migraciones nuevas corren con `LoanSystem.DbMigrator` en BD limpia y con seeds.
- [ ] Endpoints documentados en OpenAPI con `summary`, respuestas y ejemplos de error.
- [ ] Toda escritura deja registro en `audit_logs`.
- [ ] READMEs de `Products.Core` y `Products.Adapters` actualizados (formato: ¿Para qué sirve? / ¿Qué hace? / ¿Por qué?).
- [ ] [`DECISION_01_TASA_EFECTIVA_TEA.md`](../backend/docs/DECISION_01_TASA_EFECTIVA_TEA.md): marcar como resueltos los pendientes cerrados por este plan.
- [ ] En [`REQUERIMIENTOS_BACKEND.md`](REQUERIMIENTOS_BACKEND.md), el **Estado** del requerimiento pasa a **Implementado**.
- [ ] PR revisado por otra persona; los cambios en el motor (`Engine/`) los revisa además alguien de negocio/finanzas con los valores de §12.

---

## 15. Riesgos y preguntas abiertas

| # | Tema | Acción propuesta | Dueño |
|---|---|---|---|
| R1 | Convención de 360 días y días reales (D2, D3) | Validar con asesoría/SBS que el cálculo `ACTUAL_360` con interés hasta la fecha teórica es el que se informará en la hoja resumen. | Finanzas / Legal |
| R2 | Cómo se compara la mora diaria con el tope moratorio del BCRP (efectiva `(1+d)^360 − 1` vs. nominal `d · 360`) | Default conservador (efectiva). Confirmar con legal y, si cambia, es una sola función (`RateCapPolicy`). | Legal |
| R3 | ¿El alta de un producto también debe pasar por cuatro ojos? (D8) | Hoy no. Si cumplimiento lo exige, crear `PRODUCT_CREATION` en `ApprovalOperationTypes` y reutilizar el handler. | Cumplimiento |
| R4 | Solicitudes en curso cuando cambia la versión (PRO-09.6) | Hoy conservan su versión. Alternativa: re-cotizar al aprobar. Decidir con Origination antes de BE-ORI-07. | Producto |
| R5 | Edad máxima: ¿a la fecha de solicitud o al vencimiento? | Se eligió al vencimiento (práctica habitual, protege al seguro de desgravamen). Confirmar. | Riesgos |
| R6 | Topes: ¿se aplican a préstamos vigentes cuando baja el tope? | Hoy solo bloquean productos/solicitudes nuevas. Confirmar alcance de la Ley 31143. | Legal |
| R7 | Feriados regionales / días no laborables decretados a última hora | La tabla `holidays` es nacional. Los cronogramas ya generados no se recalculan; documentarlo. | Operaciones |
| R8 | Precisión `NUMERIC(12,6)` para `mora_rate_per_day` | Suficiente para tasas ≥ 0,0001 % diario. Si negocio necesita más, ampliar a `(14,10)` en una migración. | Equipo |
| R10 | `ACTUAL_360` + Francés con plazos mensuales largos: con TEA 30 % y 360 cuotas la cuota nivelada (223,32) no cubre el interés de un mes de 31 días (227,43) → `ScheduleNotAmortizing` (PRO-02.6). Con 12 % sí amortiza. | Limitar `max_term` mensual por producto (p. ej. ≤ 120) o aceptar amortización negativa puntual. Decidir antes de PRO-01. | Producto / Finanzas |
| R9 | IGV u otros tributos sobre comisiones | No contemplado. Confirmar con contabilidad antes de producción. | Contabilidad |
