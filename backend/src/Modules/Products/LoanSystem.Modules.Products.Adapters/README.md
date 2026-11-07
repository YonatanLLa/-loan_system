# LoanSystem.Modules.Products.Adapters

## 1. ¿Para qué sirve?
`LoanSystem.Modules.Products.Adapters` es la **Capa de Adaptadores (Entrada y Salida)** del módulo de Productos Crediticios (`Products`). Conecta el núcleo puro (`LoanSystem.Modules.Products.Core`) con el mundo exterior: expone los endpoints HTTP REST bajo `/api/v1/products`, configura la persistencia en PostgreSQL sobre el esquema `products` y registra las dependencias del módulo en el contenedor DI.

---

## 2. ¿Qué hace?

### Responsabilidades actuales y archivos `.cs`

- **`ProductsModule.cs`**
  - Punto único de entrada pública del módulo para el host (`LoanSystem.WebApi` y `LoanSystem.Workers`).
  - Expone dos métodos de extensión:
    - `AddProductsModule(this IServiceCollection services, IConfiguration configuration)`: Registra `ProductsDbContext` conectado a PostgreSQL con el `AuditLogInterceptor`, enlaza los puertos de salida (`IProductsUnitOfWork`, `IProductsOutbox`) y registra el caso de uso de entrada `ISimulateScheduleUseCase` → `SimulateScheduleUseCase`.
    - `MapProductsEndpoints(this IEndpointRouteBuilder endpoints)`: Delega el mapeo de rutas HTTP a `ProductsEndpoints`.

- **`Inbound/Http/ProductsEndpoints.cs`**
  - Adaptador primario HTTP construido con Minimal APIs de ASP.NET Core.
  - Define el grupo de rutas `/api/v1/products` con la etiqueta OpenAPI `"Products"`:
    - `GET /api/v1/products/ping`: Endpoint de verificación de disponibilidad del módulo.
    - `POST /api/v1/products/simulate-schedule`: Recibe un `SimulateScheduleRequest`, lo delega al puerto `ISimulateScheduleUseCase` y transforma el `Result<SimulateScheduleResponse>` en `200 OK` o en un `ProblemDetails` estándar (`400 Bad Request`) usando `ProblemDetailsResultMapper.ToProblemResult`.

- **`Outbound/Persistence/ProductsDbContext.cs`**
  - Adaptador secundario de persistencia que hereda de `ModuleDbContext` (`LoanSystem.Common.Adapters`).
  - Configura por defecto el esquema de base de datos **`products`** (`modelBuilder.HasDefaultSchema("products")`), aislando las tablas de catálogo crediticio (`loan_products`, `product_fees`) del resto de módulos en PostgreSQL.

### ¿Qué se agrega aquí conforme crece el módulo?
- En `Inbound/Http/`: Nuevos endpoints para listar productos activos (`GET /api/v1/products`), crear o configurar tasas/comisiones (`POST /api/v1/products`, protegido con política RBAC `ADMIN`).
- En `Outbound/Persistence/`: Configuraciones `IEntityTypeConfiguration<T>` de EF Core y repositorios concretos (`LoanProductRepository : ILoanProductRepository`) que consulten las tablas definidas en `database/migrations/04_loan_products.sql`.

---

## 3. ¿Por qué está diseñado así?

1. **Aislamiento de Esquema por Módulo (`HasDefaultSchema("products")`)**:
   En nuestro Monolito Modular, aunque todos los módulos comparten el mismo clúster de PostgreSQL 17 para aprovechar transacciones locales y simplicidad operativa, ningún módulo puede hacer `JOIN` ni escribir directamente en las tablas de otro módulo. `ProductsDbContext` opera exclusivamente sobre su propio esquema `products` (y la tabla transversal `public.outbox_messages` heredada de `ModuleDbContext`).
2. **Adaptadores HTTP Ultradelgados:**
   `ProductsEndpoints.cs` no contiene validaciones financieras ni fórmulas matemáticas; actúa únicamente como traductor entre el protocolo HTTP (JSON, códigos de estado, OpenAPI) y el puerto `ISimulateScheduleUseCase`.
3. **Encapsulamiento Estricto (`internal`):**
   Tanto `ProductsEndpoints` como `ProductsDbContext` son `internal sealed`. El único tipo `public` de todo el ensamblado es `ProductsModule`, garantizando mediante `NetArchTest` que ningún otro módulo pueda acoplarse a los detalles técnicos de `Products`.
