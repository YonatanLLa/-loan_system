# Backend — Sistema de Préstamos (.NET 10, Monolito Modular Hexagonal)

Backend transaccional para la plataforma de créditos y préstamos (Fintech 100% digital), implementado sobre **.NET 10 / C# 14** con **Arquitectura Hexagonal (Puertos y Adaptadores)** por cada módulo, conforme a los principios y lineamientos establecidos en [`ARQUITECTURA_GENERAL.md`](../ARQUITECTURA_GENERAL.md) y [`docs/REQUERIMIENTOS_BACKEND.md`](../docs/REQUERIMIENTOS_BACKEND.md).

---

## 1. Principios de Arquitectura

1. **Hexagonal por Módulo:**
   - Cada módulo se divide en dos proyectos explícitos:
     - `LoanSystem.Modules.<Módulo>.Core`: Dominio, casos de uso, contratos y puertos de entrada/salida. Totalmente agnóstico de frameworks (sin referencias a Entity Framework, ASP.NET Core, Npgsql ni Redis).
     - `LoanSystem.Modules.<Módulo>.Adapters`: Implementaciones de entrada (Minimal APIs, Webhooks, Jobs) y salida (EF Core `DbContext`, Dapper, proveedores externos).
2. **Puertos de Entrada y Casos de Uso:**
   - Convención `I<Verbo><Objeto>UseCase` con método `ExecuteAsync(...)`, retornando `Result<T>`.
   - Reemplaza frameworks pesados con inyección de dependencias directa y tipos de resultado funcionales.
3. **Precisión Financiera (Regla Inviolable):**
   - Todos los importes se manejan en `decimal` de 128 bits con el Value Object `Money`.
   - Redondeo estricto **Half-Up** (`MidpointRounding.AwayFromZero`) a 2 decimales.
   - Prohibido el uso de `float` o `double` en entidades y modelos de dominio (validado automáticamente por pruebas de arquitectura).
4. **SQL-First:**
   - La base de datos relacional PostgreSQL 15+ se administra mediante scripts SQL en `database/migrations/`, aplicados por `LoanSystem.DbMigrator` (DbUp). EF Core solo mapea entidades sin generar migraciones duplicadas.
5. **Idempotencia y Outbox:**
   - Middleware de idempotencia global con cabecera `Idempotency-Key` respaldado en Redis.
   - Patrón Outbox transaccional para la publicación confiable de eventos e integraciones asíncronas.

---

## 2. Estructura de la Solución

```text
backend/
├── LoanSystem.slnx                 # Solución en formato XML de .NET 10
├── Directory.Build.props           # .NET 10, C# 14, Nullable, TreatWarningsAsErrors
├── Directory.Packages.props        # Central Package Management (CPM)
├── global.json                     # Bloqueo de SDK 10.0.x
├── docker-compose.yml              # PostgreSQL 17 + Redis 7
├── src/
│   ├── Common/
│   │   ├── LoanSystem.Common.Core/       # Entity, ValueObject, Money, Result, Error, puertos base
│   │   └── LoanSystem.Common.Adapters/   # Npgsql, Redis, IdempotencyMiddleware, AuditLogInterceptor, ProblemDetails
│   ├── Modules/
│   │   ├── Security/                     # Autenticación, RBAC, bitácora (Core & Adapters)
│   │   ├── Customers/                    # Clientes, KYC, cuentas bancarias, OTP (Core & Adapters)
│   │   ├── Products/                     # Catálogo de productos y simulación de cronogramas (Core & Adapters)
│   │   ├── Origination/                  # Solicitudes y evaluación DTI (Core & Adapters)
│   │   ├── Loans/                        # Préstamos, cronogramas y prepagos (Core & Adapters)
│   │   ├── Payments/                     # Recaudación, cascada de prelación y libro mayor (Core & Adapters)
│   │   ├── Treasury/                     # Cuentas bancarias, desembolsos y conciliación (Core & Adapters)
│   │   ├── Collections/                  # Mora diaria, aging y promesas de pago (Core & Adapters)
│   │   ├── Integrations/                 # Webhooks y adaptadores de pasarela (Core & Adapters)
│   │   ├── Notifications/                # SMS, WhatsApp y Email desacoplados (Core & Adapters)
│   │   ├── Administration/               # Parámetros, empresa, catálogos y feriados (Core & Adapters)
│   │   └── Reports/                      # Vistas de consulta, dashboards y estados de cuenta (Core & Adapters)
│   ├── LoanSystem.WebApi/                # Host HTTP: Minimal APIs, OpenAPI + Scalar, JWT, HealthChecks
│   ├── LoanSystem.Workers/               # Host Worker: Quartz.NET (Zona horaria America/Lima) y Outbox
│   └── LoanSystem.DbMigrator/            # Consola de migraciones de base de datos con DbUp
└── tests/
    ├── LoanSystem.Domain.UnitTests/      # Pruebas matemáticas de Money, redondeo Half-Up y Value Objects
    ├── LoanSystem.Application.UnitTests/ # Pruebas funcionales de Result y caso de uso de simulación
    ├── LoanSystem.Adapters.UnitTests/    # Middleware de idempotencia y registro de eventos del Outbox
    ├── LoanSystem.Architecture.Tests/    # Validación estricta de reglas hexagonales con NetArchTest
    └── LoanSystem.IntegrationTests/      # Pruebas end-to-end con Testcontainers (PostgreSQL 17)
```

---

## 3. Puesta en Marcha

### Prerrequisitos
- .NET SDK 10.0+ instalado (`dotnet --version`)
- Docker Desktop o Docker Engine activo

### 1. Iniciar Servicios de Infraestructura (PostgreSQL y Redis)
```powershell
cd backend
docker compose up -d
```

### Configuración (sin secretos en el código)
- El código **no tiene valores por defecto** para secretos ni credenciales: fuera de `Development`, la aplicación falla al arrancar (`ValidateStartupSecrets`) si falta algún secreto obligatorio, si queda `Jwt:Key` legado o si detecta placeholders locales (`postgrespassword`, `changeme`, etc.).
- En `Development`:
  - `ConnectionStrings:Database` y `ConnectionStrings:Redis` coinciden con `docker-compose.yml` vía `appsettings.Development.json` (excluido de `dotnet publish`).
  - `Jwt:SigningKeys` no necesita configurarse localmente: si falta en `Development`, `JwtKeyRing` genera una clave RSA-3072 efímera en memoria con aviso en logs.
  - Para probar flujos MFA TOTP persistentes en local, configura User Secrets:
    ```powershell
    dotnet user-secrets --project src/LoanSystem.WebApi set "Security:Mfa:EncryptionKeys" '{"active":"v1","keys":{"v1":"<BASE64_32_BYTES>"}}'
    ```
- En Staging / Producción (`Secrets:Provider = KeyPerFile` o variables de entorno):
  - `ConnectionStrings__Database`
  - `ConnectionStrings__Redis` (opcional; sin Redis la idempotencia usa memoria local)
  - `Jwt__SigningKeys` (JSON con `{ "keys": [{ "privateKeyPem": "...PKCS#8...", "notBefore": "...", "notAfter": "..." }] }`; consultable públicamente vía `GET /.well-known/jwks.json`)
  - `Security__Mfa__EncryptionKeys` (JSON con `{ "active": "<keyId>", "keys": { "<keyId>": "<BASE64_32_BYTES>" } }`)
  - Nota: `LoanSystem.Workers` solo requiere `ConnectionStrings__Database` (no carga `Jwt__SigningKeys` ni `Security__Mfa__EncryptionKeys`).
  - Scripts de rotación sin downtime y runbook: [`scripts/rotate-jwt-keys.ps1`](scripts/rotate-jwt-keys.ps1), [`scripts/rotate-jwt-keys.sh`](scripts/rotate-jwt-keys.sh) y [`docs/RUNBOOK_SECRETOS_Y_ROTACION.md`](docs/RUNBOOK_SECRETOS_Y_ROTACION.md).

### 2. Ejecutar Migraciones de Base de Datos
Aplica los 17 scripts modulares de `database/`. La cadena de conexión se pasa como argumento, con `ConnectionStrings__Database` (o `DATABASE_URL`), o desde `Secrets:Provider=KeyPerFile`:
```powershell
dotnet run --project src/LoanSystem.DbMigrator -- "Host=localhost;Port=5432;Database=loan_system;Username=postgres;Password=postgrespassword"
```

### 3. Iniciar la Web API
```powershell
dotnet run --project src/LoanSystem.WebApi
```
- **Documentación Interactiva (Scalar):** `http://localhost:5000/scalar`
- **Especificación OpenAPI (JSON):** `http://localhost:5000/openapi/v1.json`
- **Health Check:** `http://localhost:5000/health`
- **Endpoints de Módulos:** `http://localhost:5000/api/<modulo>/ping`
- **Simulador de Préstamos (Ejemplo):**
  ```http
  POST /api/products/simulate
  Content-Type: application/json

  {
    "amount": 10000,
    "annualRatePercentage": 12,
    "termMonths": 12,
    "method": "FRENCH"
  }
  ```

### 4. Iniciar Procesos en Segundo Plano (Workers)
```powershell
dotnet run --project src/LoanSystem.Workers
```
Jobs programados con Quartz (hora de Lima `America/Lima`):
- `DailyMoraCalculationJob`: Diario a las **00:05**
- `DailyBalanceJob`: Diario a las **06:00**
- `DailyRemindersJob`: Diario a las **09:00**
- `DisbursementsInTransitJob`: Cada **15 minutos**
- `OutboxDispatcherJob`: Cada **10 segundos**

---

## 4. Ejecución de Pruebas Automatizadas

Para ejecutar todas las pruebas unitarias, de arquitectura e integración de la solución:
```powershell
dotnet test LoanSystem.slnx
```

| Suite | Propósito | Tecnología |
|---|---|---|
| **Domain.UnitTests** | Precisión de redondeo *Half-Up*, operaciones monetarias | xUnit + Shouldly |
| **Application.UnitTests** | Respuestas `Result<T>`, simulación de cronogramas y centavo residual | xUnit + Shouldly |
| **Adapters.UnitTests** | Idempotencia (replay, 409, 422, 5xx) y registro de handlers del Outbox | xUnit + Shouldly |
| **Architecture.Tests** | Aislamiento hexagonal, dependencias permitidas y tipo de datos de dominio | NetArchTest.Rules |
| **IntegrationTests** | Migración real sobre contenedor efímero PostgreSQL 17 | Testcontainers + Dapper |
