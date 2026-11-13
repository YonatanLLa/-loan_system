# Arquitectura y Configuración del Backend

> Documento técnico que resume la arquitectura aplicada, el stack tecnológico y las
> configuraciones vigentes del backend del **Sistema de Préstamos** (Fintech digital).
>
> Fecha: 2026-11-07

---

## 1. Resumen ejecutivo

| Aspecto | Decisión aplicada |
|---|---|
| **Plataforma** | .NET 10 / C# 14 |
| **Estilo arquitectónico** | Monolito Modular + Arquitectura Hexagonal (Puertos y Adaptadores) por módulo |
| **ORM / acceso a datos** | Entity Framework Core 10 (escritura/mapeo) + Dapper (lecturas/consultas) |
| **Base de datos** | PostgreSQL 17 (vía Npgsql), esquema administrado SQL-First con DbUp |
| **Caché / idempotencia** | Redis 7 (StackExchange.Redis), con fallback en memoria |
| **API** | ASP.NET Core Minimal APIs + OpenAPI + Scalar |
| **Autenticación** | JWT Bearer (**RS256** + `kid` RFC 7638 + `/.well-known/jwks.json`) + autorización por roles |
| **Jobs / background** | Quartz.NET (zona horaria `America/Lima`) + patrón Outbox |
| **Logging** | Serilog |
| **Testing** | xUnit, Shouldly, NetArchTest, Testcontainers |
| **Gestión de paquetes** | Central Package Management (CPM) |

---

## 2. Plataforma y versiones (.NET)

- **SDK fijado** en `global.json`: `10.0.100` con `rollForward: latestFeature` y `allowPrerelease: false`.
- **Target Framework** global (`Directory.Build.props`): `net10.0`.
- **Lenguaje:** C# 14 (`LangVersion: latest`).
- **Flags de compilación obligatorios** para todos los proyectos:
  - `Nullable: enable` — referencias nulas habilitadas.
  - `ImplicitUsings: enable`.
  - `TreatWarningsAsErrors: true` — las advertencias rompen el build (excepto avisos
    transitorios de auditoría NuGet `NU1901`–`NU1904`).
- **Central Package Management (CPM):** todas las versiones de paquetes se definen una sola
  vez en `Directory.Packages.props` (`ManagePackageVersionsCentrally: true`); los `.csproj`
  solo declaran `<PackageReference>` sin versión.

---

## 3. Arquitectura aplicada

### 3.1 Monolito Modular Hexagonal

El sistema es un **monolito modular**: una sola solución desplegable dividida en **12 módulos
de negocio** independientes. Cada módulo sigue **Arquitectura Hexagonal (Puertos y
Adaptadores)** y se materializa en **dos proyectos**:

```
LoanSystem.Modules.<Módulo>.Core       → Dominio + Casos de uso + Puertos  (agnóstico de frameworks)
LoanSystem.Modules.<Módulo>.Adapters   → Adaptadores de entrada y salida   (EF Core, Dapper, HTTP, Jobs)
```

- **`.Core`** no referencia EF Core, ASP.NET Core, Npgsql ni Redis. Solo contiene:
  - `Domain/` — entidades, value objects, reglas de negocio.
  - `Application/` — implementación de los casos de uso.
  - `Contracts/` — DTOs e interfaces públicas del módulo.
  - `Ports/Inbound` y `Ports/Outbound` — puertos de entrada (casos de uso) y de salida
    (repositorios, proveedores externos).
- **`.Adapters`** implementa los puertos:
  - `Inbound/Http` — Minimal APIs y webhooks.
  - `Inbound/Jobs` — jobs de Quartz.
  - `Outbound/Persistence` — `DbContext` de EF Core y consultas Dapper.
  - `Outbound/Providers` — integraciones con proveedores externos.

### 3.2 Encapsulamiento de módulos

Definido en `Directory.Build.props`: los `.Core` exponen sus tipos `internal` únicamente a su
propio `.Adapters` (para el registro de DI) y a los proyectos de pruebas unitarias, mediante
`InternalsVisibleTo`. Esto impide que un módulo acceda a los internos de otro.

### 3.3 Puertos de entrada y casos de uso

- Convención `I<Verbo><Objeto>UseCase` con método `ExecuteAsync(...)`.
- Retornan un tipo de resultado funcional `Result<T>` (en lugar de excepciones para el flujo
  de negocio).
- Inyección de dependencias directa, sin frameworks de mediación pesados.

### 3.4 Los 12 módulos de negocio

| Módulo | Responsabilidad |
|---|---|
| **Security** | Autenticación, RBAC, bitácora de auditoría |
| **Customers** | Clientes, KYC, cuentas bancarias, OTP |
| **Products** | Catálogo de productos y simulación de cronogramas |
| **Origination** | Solicitudes y evaluación DTI |
| **Loans** | Préstamos, cronogramas y prepagos |
| **Payments** | Recaudación, cascada de prelación y libro mayor |
| **Treasury** | Cuentas bancarias, desembolsos y conciliación |
| **Collections** | Mora diaria, aging y promesas de pago |
| **Integrations** | Webhooks y adaptadores de pasarela de pago |
| **Notifications** | SMS, WhatsApp y Email desacoplados |
| **Administration** | Parámetros, empresa, catálogos y feriados |
| **Reports** | Vistas de consulta, dashboards y estados de cuenta |

### 3.5 Reglas transversales

- **Precisión financiera (regla inviolable):** todos los importes usan `decimal` (128 bits)
  encapsulado en el Value Object `Money`, con redondeo **Half-Up**
  (`MidpointRounding.AwayFromZero`) a 2 decimales. El uso de `float`/`double` en el dominio
  está **prohibido** y validado automáticamente por pruebas de arquitectura.
- **SQL-First:** el esquema relacional se administra con scripts SQL aplicados por
  `LoanSystem.DbMigrator` (DbUp). EF Core **solo mapea** entidades, no genera migraciones.
- **Idempotencia:** middleware global con cabecera `Idempotency-Key` respaldado en Redis.
- **Outbox transaccional:** publicación confiable de eventos e integraciones asíncronas.

---

## 4. ORM y acceso a datos

Se aplica una estrategia de **persistencia poliglota por operación**:

- **Entity Framework Core 10** (`Microsoft.EntityFrameworkCore` 10.0.4) — mapeo de entidades y
  escrituras transaccionales. Cada módulo tiene su propio `DbContext` en
  `Outbound/Persistence`.
- **Npgsql.EntityFrameworkCore.PostgreSQL** 10.0.3 — proveedor de PostgreSQL para EF Core.
- **Dapper** 2.1.89 — consultas de lectura de alto rendimiento (reportes, vistas, pruebas de
  integración).
- **DbUp (`dbup-postgresql` 7.0.1)** — motor de migraciones SQL-First ejecutado por el
  proyecto de consola `LoanSystem.DbMigrator`. EF Core **no** genera migraciones; el esquema
  es la fuente de verdad en los scripts SQL.

> Nota de diseño: EF Core se usa para mapeo/escritura y Dapper para lectura; DbUp es el único
> responsable de crear y versionar el esquema.

---

## 5. Hosts y proyectos de la solución

```
backend/
├── LoanSystem.slnx                 # Solución (formato XML .slnx de .NET 10)
├── Directory.Build.props           # net10.0, C# 14, Nullable, TreatWarningsAsErrors
├── Directory.Packages.props        # Central Package Management
├── global.json                     # Bloqueo de SDK 10.0.x
├── docker-compose.yml              # PostgreSQL 17 + Redis 7
├── src/
│   ├── Common/
│   │   ├── LoanSystem.Common.Core/        # Entity, ValueObject, Money, Result, Error, puertos base
│   │   └── LoanSystem.Common.Adapters/    # Npgsql, Redis, IdempotencyMiddleware, Outbox, ProblemDetails
│   ├── Modules/                           # 12 módulos (cada uno Core + Adapters)
│   ├── LoanSystem.WebApi/                 # Host HTTP: Minimal APIs, OpenAPI + Scalar, JWT, HealthChecks
│   ├── LoanSystem.Workers/                # Host Worker: Quartz.NET + Outbox
│   └── LoanSystem.DbMigrator/             # Consola de migraciones (DbUp)
└── tests/                                 # 5 suites de pruebas
```

- **`LoanSystem.WebApi`** (`Microsoft.NET.Sdk.Web`): host de la API. Registra los 12 módulos vía
  métodos de extensión `Add<Módulo>Module(...)`, configura OpenAPI + Scalar, JWT Bearer,
  autorización por roles e idempotencia.
- **`LoanSystem.Workers`**: host de procesos en segundo plano con Quartz.NET.
- **`LoanSystem.DbMigrator`** (`OutputType: Exe`): aplica los scripts SQL con DbUp.
- **`LoanSystem.Common.Adapters`**: infraestructura compartida (EF Core, Dapper, Redis, Quartz,
  middleware de idempotencia, interceptores de auditoría, ProblemDetails). Referencia
  `Microsoft.AspNetCore.App` vía `FrameworkReference`.

---

## 6. Configuración del host API (`Program.cs`)

Servicios y pipeline configurados en `LoanSystem.WebApi`:

- `AddProblemDetails()` — respuestas de error estandarizadas (RFC 7807).
- `AddOpenApi()` + `MapScalarApiReference()` — documentación interactiva (solo en Development).
- `AddHttpContextAccessor()` + `ICurrentUser` → `CurrentUserProvider`.
- `IClock` → `SystemClock` (singleton) — reloj inyectable para testabilidad.
- **Idempotencia:** `AddIdempotency(...)` usa Redis si `ConnectionStrings:Redis` está presente;
  de lo contrario cae a almacenamiento **en memoria** (con advertencia en logs, no apto para
  múltiples instancias).
- **Autenticación JWT Bearer (RS256, `AddSecurityAuthentication`):**
  - Centralizada en el módulo `Security` (`JwtKeyRing` + `JwtTokenGenerator`), sin lógica JWT inline en `Program.cs`.
  - Solo acepta algoritmo `RS256` (`ValidAlgorithms = ["RS256"]`), `RequireSignedTokens = true`, `TryAllIssuerSigningKeys = false`, y valida por `kid` exacto (thumbprint SHA-256 RFC 7638).
  - Expone `GET /.well-known/jwks.json` (`AllowAnonymous`, `Cache-Control: public, max-age=300`) solo con parámetros públicos (`kty`, `use`, `alg`, `kid`, `n`, `e`).
  - Reporta su estado en `GET /health` (`JwtKeyRingHealthCheck`: aviso si la clave activa vence en `< 14` días).
- **Autorización por roles** (políticas):
  `ROLE_ADMIN`, `ROLE_ANALYST`, `ROLE_OPERATOR`, `ROLE_COLLECTOR`, `ROLE_AUDITOR`, `ROLE_CLIENT`
  (ADMIN tiene acceso a las políticas de los demás roles operativos).
- **Pipeline:** `UseExceptionHandler` → `UseAuthentication` → `UseAuthorization` →
  `IdempotencyMiddleware` (tras autenticación, para alcanzar la clave al usuario).

### Configuración sin secretos en el código (`AddSecretManager` + `ValidateStartupSecrets`)

- `AddSecretManager` soporta `Secrets:Provider = None` (default) o `KeyPerFile` (`Secrets:Path` + polling periódico por `Secrets:ReloadInterval` para rotación sin reinicio en Kubernetes).
- Fuera de `Development`, `ValidateStartupSecrets` falla al arrancar si falta `ConnectionStrings:Database` (y en WebApi `Jwt:SigningKeys` + `Security:Mfa:EncryptionKeys`), si está presente `Jwt:Key` legado o si detecta placeholders locales (`postgrespassword`, `changeme`, etc.).
- `appsettings.Development.json` está excluido de `dotnet publish` (`CopyToPublishDirectory="Never"`). Variables/archivos requeridos en WebApi:
  - `ConnectionStrings__Database`
  - `ConnectionStrings__Redis` (opcional; sin Redis la idempotencia usa memoria)
  - `Jwt__SigningKeys` (JSON con claves PKCS#8 RSA ≥ 2048 bits y ventanas `notBefore`/`notAfter`)
  - `Security__Mfa__EncryptionKeys` (JSON con keyring versionado AES-256-GCM para TOTP `v2`)
- En `Development`, `Jwt:SigningKeys` ausente genera una clave efímera en memoria; los secretos TOTP usan `<UserSecretsId>` de `LoanSystem.WebApi.csproj`.

---

## 7. Jobs programados (Workers / Quartz.NET)

Zona horaria `America/Lima`:

| Job | Frecuencia |
|---|---|
| `DailyMoraCalculationJob` | Diario 00:05 |
| `DailyBalanceJob` | Diario 06:00 |
| `DailyRemindersJob` | Diario 09:00 |
| `DisbursementsInTransitJob` | Cada 15 minutos |
| `OutboxDispatcherJob` | Cada 10 segundos |

---

## 8. Infraestructura local (`docker-compose.yml`)

| Servicio | Imagen | Puerto | Notas |
|---|---|---|---|
| **PostgreSQL** | `postgres:17-alpine` | 5432 | DB `loan_system`, usuario `postgres`, healthcheck `pg_isready` |
| **Redis** | `redis:7-alpine` | 6379 | Healthcheck `redis-cli ping` |

Volúmenes persistentes: `postgres_data`, `redis_data`.

---

## 9. Inventario de paquetes (CPM)

Versiones centralizadas en `Directory.Packages.props`:

**Microsoft .NET & EF Core (10.0.4)**
`Microsoft.AspNetCore.OpenApi`, `Microsoft.AspNetCore.Authentication.JwtBearer`,
`Microsoft.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.Design`,
`Microsoft.Extensions.Hosting`, `...Configuration.Abstractions`,
`...DependencyInjection.Abstractions`, `...Logging.Abstractions`.

**Base de datos y caché**
`Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.3, `Dapper` 2.1.89,
`StackExchange.Redis` 3.4.31, `dbup-postgresql` 7.0.1.

**API, Jobs y Logging**
`Scalar.AspNetCore` 2.19.0, `Quartz` 4.7.2, `Quartz.Extensions.Hosting` 4.7.2,
`Serilog.AspNetCore` 9.0.0.

**Testing**
`Microsoft.NET.Test.Sdk` 17.13.0, `xunit` 2.9.3, `xunit.runner.visualstudio` 3.0.2,
`Shouldly` 4.3.0, `NetArchTest.Rules` 1.3.2, `Testcontainers.PostgreSql` 4.16.0,
`coverlet.collector` 6.0.4.

---

## 10. Estrategia de pruebas

| Suite | Propósito | Tecnología |
|---|---|---|
| **Domain.UnitTests** | Precisión Half-Up, operaciones monetarias | xUnit + Shouldly |
| **Application.UnitTests** | `Result<T>`, simulación de cronogramas, centavo residual | xUnit + Shouldly |
| **Adapters.UnitTests** | Idempotencia (replay, 409, 422, 5xx), handlers del Outbox | xUnit + Shouldly |
| **Architecture.Tests** | Aislamiento hexagonal, dependencias permitidas, tipos de dominio | NetArchTest.Rules |
| **IntegrationTests** | Migración real sobre contenedor efímero PostgreSQL 17 | Testcontainers + Dapper |

> Las **pruebas de arquitectura** (NetArchTest) son clave: garantizan que los `.Core` no
> dependan de frameworks de infraestructura y que no se usen `float`/`double` en el dominio.
