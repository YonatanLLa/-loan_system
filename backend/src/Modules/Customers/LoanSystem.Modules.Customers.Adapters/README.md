# LoanSystem.Modules.Customers.Adapters

## 1. ¿Para qué sirve?
`LoanSystem.Modules.Customers.Adapters` es la **Capa de Adaptadores (Entrada y Salida)** del módulo de Clientes y KYC (`Customers`). Conecta el núcleo `LoanSystem.Modules.Customers.Core` con los endpoints HTTP REST bajo `/api/v1/customers` y con la persistencia en PostgreSQL 17 dentro del esquema `customers`.

---

## 2. ¿Qué hace?

### Responsabilidades actuales y archivos `.cs`

- **`CustomersModule.cs`**
  - Clase pública de composición del módulo que expone `AddCustomersModule` (registrando `CustomersDbContext`, `ICustomersUnitOfWork` e `ICustomersOutbox`) y `MapCustomersEndpoints`.

- **`Inbound/Http/CustomersEndpoints.cs`**
  - Adaptador primario HTTP (Minimal APIs) que define el grupo `/api/v1/customers` con la etiqueta OpenAPI `"Customers"`, preparando las rutas para el registro de clientes, carga/verificación de documentos KYC, registro de cuentas bancarias para desembolso y consulta de perfil crediticio.

- **`Outbound/Persistence/CustomersDbContext.cs`**
  - Adaptador secundario de persistencia que hereda de `ModuleDbContext`.
  - Configura por defecto el esquema **`customers`** (`modelBuilder.HasDefaultSchema("customers")`), mapeando las tablas creadas en `database/migrations/03_customers_kyc.sql` (`customers`, `customer_addresses`, `customer_bank_accounts`, `kyc_documents`, `credit_assessments`).

### ¿Qué se agrega aquí conforme crece el módulo?
- Endpoints HTTP protegidos por políticas RBAC (`ANALYST`, `ADMIN`, `CASHIER` y autogestión para `CUSTOMER`) y mapeados a `ProblemDetails` mediante `ProblemDetailsResultMapper`.
- Configuraciones EF Core (`IEntityTypeConfiguration<Customer>`, etc.) y `CustomerRepository` en `Outbound/Persistence/`.
- Consumidores de eventos de integración (`Inbound/Events/`) cuando `Integrations` confirme la validación de identidad RENIEC o el reporte de central de riesgos (SBS/Equifax).

---

## 3. ¿Por qué está diseñado así?

1. **Aislamiento de Datos Sensibles (PII y KYC) en el Esquema `customers`:**
   Los datos personales identificables (PII), documentos de identidad y cuentas bancarias de los clientes están encapsulados dentro del esquema `customers` y solo son accesibles a través de `CustomersDbContext`. Ningún otro módulo puede leer o modificar directamente estas tablas sin pasar por los contratos del módulo.
2. **Encapsulamiento `internal`:**
   Todos los adaptadores (`CustomersEndpoints`, `CustomersDbContext`) son `internal sealed`, garantizando a nivel de compilador y `NetArchTest` que no existan referencias cruzadas ilegales desde otros módulos.
