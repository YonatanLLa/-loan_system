# LoanSystem.DbMigrator

## 1. ¿Para qué sirve?
`LoanSystem.DbMigrator` es la herramienta CLI ejecutable encargada de **aplicar y versionar de forma determinista el esquema de base de datos PostgreSQL 17** utilizando un enfoque **SQL-First** con **DbUp**.

Dentro de la arquitectura del sistema, garantiza que los 12 esquemas modulares (`admin`, `security`, `customers`, `products`, `origination`, `loans`, `payments`, `treasury`, `collections`, `notifications`, `integrations`, `reports`), las funciones/triggers, los índices de rendimiento, los datos semilla (*seeds*) y la tabla transversal de Outbox se ejecuten exactamente en el orden requerido tanto en entornos locales, pipelines CI/CD, despliegues productivos como en las pruebas de integración (`Testcontainers`).

---

## 2. ¿Qué hace?

### Responsabilidades actuales y archivos

- **`Program.cs`**
  - Expone la clase pública `DatabaseMigrator` (invocable tanto desde la línea de comandos `dotnet run --project src/LoanSystem.DbMigrator` como programáticamente desde `LoanSystem.IntegrationTests`).
  - Verifica y asegura la existencia de la base de datos destino con `EnsureDatabase.For.PostgresqlDatabase(connectionString)`.
  - Localiza automáticamente el directorio raíz `database/` del repositorio y construye una secuencia ordenada y determinista de scripts SQL:
    1. `migrations/00_extensions_and_enums.sql` (Extensiones `uuid-ossp`, `pgcrypto`, y todos los tipos `ENUM` del dominio financiero).
    2. `functions/fn_updated_at.sql` (Función PL/pgSQL para actualizar automáticamente la columna `updated_at` mediante triggers).
    3. `migrations/01_admin_config.sql` (Parámetros del sistema, sucursales, feriados, secuencias y `audit_log` inmutable).
    4. `migrations/02_security_users.sql` (Usuarios, los 6 roles RBAC, permisos y sesiones/tokens).
    5. `migrations/03_customers_kyc.sql` (Clientes, direcciones, cuentas bancarias, documentos KYC y evaluaciones crediticias).
    6. `migrations/04_loan_products.sql` (Catálogo de productos crediticios, tasas y cargos/comisiones).
    7. `migrations/05_loan_applications.sql` (Solicitudes de préstamo, workflow de evaluación y comité de aprobación).
    8. `migrations/06_loans_schedules.sql` (Préstamos activos, cronogramas de amortización Francés, cuotas, desembolsos y refinanciamientos).
    9. `migrations/07_payments_ledger.sql` (Pagos, prelación de imputación `Mora → Interés → Capital`, recibos y libro mayor contable `accounting_entries`).
    10. `migrations/08_treasury_reconciliation.sql` (Saldos diarios de tesorería y conciliación bancaria).
    11. `migrations/09_collections_notifications.sql` (Casos de cobranza, gestiones, promesas de pago y plantillas/envíos de notificaciones).
    12. `migrations/10_integrations_reconciliation.sql` (Logs de pasarelas externas, webhooks y discrepancias de conciliación).
    13. `indexes/11_indexes.sql` (Índices compuestos y parciales para consultas críticas de mora, pagos y préstamos).
    14. `seeds/12_seed_data.sql` (Catálogos iniciales, roles base, configuración por defecto).
    15. `migrations/13_outbox_messages.sql` (Tabla `public.outbox_messages` e índice parcial para `FOR UPDATE SKIP LOCKED`).
  - Registra cada script ejecutado en la tabla de control `public.schemaversions` dentro de una transacción por script (`WithTransactionPerScript()`).

### ¿Qué se agrega aquí conforme crece el sistema?
- Cuando se añade una nueva migración SQL numerada en `database/migrations/` (por ejemplo, `14_new_feature.sql`), se incorpora en la lista ordenada `OrderedScripts` de `DatabaseMigrator` para garantizar su ejecución idempotente en todos los ambientes.

---

## 3. ¿Por qué está diseñado así?

1. **Filosofía SQL-First en vez de EF Core Migrations:**
   En un sistema financiero sobre PostgreSQL 17, la base de datos no es un simple almacén pasivo: utiliza tipos nativos `NUMERIC(18,2)` / `NUMERIC(12,6)`, restricciones `CHECK` estrictas, tipos `ENUM`, triggers de inmutabilidad (que prohíben `UPDATE`/`DELETE` en `audit_log` y `accounting_entries`) e índices parciales avanzados. Las migraciones autogeneradas por un ORM suelen abstraer o degradar estas capacidades. Con **DbUp + SQL puro**, los scripts en `database/` son la única fuente de verdad (*Single Source of Truth*).
2. **Paridad Absoluta entre Producción y Tests (`DatabaseMigrator`):**
   Al encapsular la lógica de migración en `DatabaseMigrator.Migrate(...)`, nuestros tests de integración en `LoanSystem.IntegrationTests` levantan un contenedor PostgreSQL 17 real con `Testcontainers` y ejecutan exactamente el mismo motor de migración que corre en producción, detectando cualquier error de sintaxis SQL o restricción rota antes de fusionar código.
