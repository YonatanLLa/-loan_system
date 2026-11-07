# LoanSystem.WebApi

## 1. ¿Para qué sirve?
`LoanSystem.WebApi` es el **Host HTTP Principal y Raíz de Composición (*Composition Root*) Síncrona** del sistema. En nuestra Arquitectura Hexagonal de Monolito Modular, este proyecto es un contenedor ejecutable ultradelgado cuya única misión es:
1. Configurar el pipeline HTTP de ASP.NET Core (.NET 10 Minimal APIs).
2. Configurar la seguridad transversal (Autenticación JWT Bearer + las 6 políticas RBAC del negocio).
3. Ensamblar los 12 módulos de negocio llamando a sus métodos de registro (`Add<Module>Module` y `Map<Module>Endpoints`).
4. Exponer la documentación interactiva OpenAPI + Scalar y los endpoints de salud (`/health`).

**No contiene lógica de negocio, entidades de dominio ni consultas a la base de datos.**

---

## 2. ¿Qué hace?

### Responsabilidades actuales y archivos

- **`Program.cs`**
  - **Observabilidad y Logging:** Inicializa Serilog leyendo configuración estructurada para trazabilidad de peticiones HTTP.
  - **Infraestructura Común:** Registra `IClock` (`SystemClock`), `ICurrentUser` (`CurrentUserProvider`), `NpgsqlConnectionFactory`, `AuditLogInterceptor`, el servicio de **Idempotencia HTTP** (`AddIdempotency`) y la infraestructura de **Outbox** (`AddOutboxInfrastructure`).
  - **Seguridad (JWT + 6 Roles RBAC):** Configura `AddAuthentication(JwtBearerDefaults.AuthenticationScheme)` y define las políticas de autorización basadas en los 6 roles oficiales del sistema:
    - `ADMIN` (Administrador del sistema / configuración global)
    - `ANALYST` (Analista de créditos / evaluación y originación)
    - `APPROVER` (Comité / aprobador de solicitudes y excepciones)
    - `CASHIER` (Cajero / ventanilla de pagos y desembolsos)
    - `COLLECTOR` (Gestor de cobranza y seguimiento de mora)
    - `CUSTOMER` (Cliente final para autogestión y consulta)
  - **Composición de los 12 Módulos:** Registra en el contenedor de inyección de dependencias (`IServiceCollection`) y mapea en el router (`IEndpointRouteBuilder` bajo el grupo `/api/v1`) cada uno de los 12 módulos:
    1. `Administration`
    2. `Security`
    3. `Customers`
    4. `Products`
    5. `Origination`
    6. `Loans`
    7. `Payments`
    8. `Treasury`
    9. `Collections`
    10. `Notifications`
    11. `Integrations`
    12. `Reports`
  - **Documentación API:** Activa `MapOpenApi()` y la interfaz moderna **Scalar** (`MapScalarApiReference()`) en entornos de desarrollo.
  - **Middleware de Idempotencia:** Inserta `UseMiddleware<IdempotencyMiddleware>()` después de autenticación/autorización y antes de ejecutar los endpoints de negocio.

- **`appsettings.json` / `appsettings.Development.json`**
  - Almacena las cadenas de conexión (`Postgres`, `Redis`), configuración de `Jwt` (`Issuer`, `Audience`, `SigningKey`) y niveles de log de Serilog.

### ¿Qué se agrega aquí conforme crece el sistema?
- Configuración de *Rate Limiting*, CORS, compresión de respuestas o telemetría OpenTelemetry a nivel de host.
- **Nunca** se agregan aquí controladores o endpoints de negocio directos; todo endpoint nuevo debe crearse dentro de `Inbound/Http/<Module>Endpoints.cs` del módulo correspondiente.

---

## 3. ¿Por qué está diseñado así?

1. **Principio de Raíz de Composición Única (*Single Composition Root*):**
   Los módulos de negocio están completamente aislados entre sí y sus implementaciones internas son `internal`. `LoanSystem.WebApi` actúa como el ensamblador que conecta los adaptadores de todos los módulos en tiempo de ejecución sin que los módulos se acoplen directamente entre ellos.
2. **Minimal APIs Descentralizadas por Módulo:**
   En lugar de tener una carpeta `Controllers/` gigante en `WebApi` que conozca los 12 dominios, cada módulo expone un único método público `Map<Module>Endpoints(this IEndpointRouteBuilder)`. Así, cada módulo es dueño de sus rutas `/api/v1/<modulo>/...`, sus contratos DTO y sus requisitos de autorización RBAC.
3. **Visibilidad Pública para `WebApplicationFactory<Program>`:**
   Al final de `Program.cs` se declara `public partial class Program;`, lo que permite que `LoanSystem.IntegrationTests` levante el host completo en memoria conectado a contenedores reales (Testcontainers PostgreSQL) para pruebas de extremo a extremo.
