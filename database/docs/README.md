# Documentación Técnica del Esquema de Base de Datos

Bienvenido a la documentación oficial del diseño, arquitectura física y matriz de relaciones del **Sistema de Préstamos y Créditos**. Este modelo está diseñado para **PostgreSQL 15+** y responde rigurosamente a las especificaciones funcionales y de negocio descritas en la documentación del proyecto (`/docs`).

---

## 1. Estándares y Convenciones de Arquitectura

El esquema implementa un **Monolito Modular** en base de datos con las siguientes premisas inviolables:

1. **Precisión Financiera:**
   - Todos los campos monetarios utilizan `NUMERIC(18,4)` con redondeo *Half-Up* a 2 decimales en el cálculo de cuotas finales. Nunca se emplean tipos de coma flotante (`FLOAT`, `DOUBLE PRECISION`).
   - Las tasas de interés y porcentajes utilizan `NUMERIC(12,6)` para permitir precisión de hasta 6 decimales en tasas equivalentes diarias y mensuales calculadas a partir de la TEA.

2. **Identificadores y Llaves Primarias:**
   - Llaves primarias universales tipo UUIDv4 (`UUID PRIMARY KEY DEFAULT gen_random_uuid()`) mediante la extensión nativa `pgcrypto`.
   - Excepción en el Libro Mayor (`transactions`) y en la bitácora (`audit_logs`), donde se utiliza `BIGSERIAL` para optimizar inserciones de alto volumen e indexación por orden cronológico estricto.

3. **Inmutabilidad Financiera y Auditoría Append-Only:**
   - La tabla `audit_logs` cuenta con triggers que prohíben operaciones `UPDATE` y `DELETE`.
   - Las transacciones desembolsadas (`loans`), los cronogramas congelados (`schedules`, `installments`) y los pagos efectuados (`payments`) son inmutables. Toda corrección de cobro se efectúa exclusivamente mediante contra-asientos (`payment_kind = 'REVERSAL'`).

4. **Idempotencia Transaccional:**
   - Claves de idempotencia únicas (`idempotency_key UNIQUE`) en `loans`, `disbursements` y `payments`.
   - Identificador externo único por proveedor (`UNIQUE(provider, external_id)`) en `integration_events` para webhooks bancarios (BCP, BBVA, Yape, Plin).

5. **Zonas Horarias:**
   - Todos los campos de fecha y hora utilizan `TIMESTAMPTZ` (UTC en base de datos). Las fechas comerciales sin hora (vencimiento de cuotas, feriados, nacimientos) utilizan `DATE`.

---

## 2. Mapa de Documentación

Esta carpeta `/database/docs` está organizada en los siguientes documentos especializados:

| Documento | Descripción |
|---|---|
| [**`diagrama-entidad-relacion.md`**](diagrama-entidad-relacion.md) | Diagramas ER visuales completos en Mermaid (diagrama global y diagramas por subsistemas). |
| [**`diccionario-de-datos.md`**](diccionario-de-datos.md) | Especificación de las 28 tablas, sus columnas, tipos de datos, constraints y los 32 tipos `ENUM`. |
| [**`relaciones-y-claves-foraneas.md`**](relaciones-y-claves-foraneas.md) | Matriz completa de Foreign Keys, cardinalidad (1:1, 1:N, N:M), reglas de integridad y dependencias. |
| [**`triggers-y-automatizaciones.md`**](triggers-y-automatizaciones.md) | Documentación de funciones PL/pgSQL, triggers automáticos, reglas de negocio en BD y partial indexes. |

---

## 3. Resumen de Estructura de Archivos del Proyecto

```text
database.sql                                # (raíz del proyecto) Esquema en un solo archivo, GENERADO desde database/
database\
├── run_all.sql                             # Script maestro: atómico (BEGIN/COMMIT) y con ON_ERROR_STOP
├── functions\
│   └── fn_updated_at.sql                  # Funciones PL/pgSQL, secuencias y triggers
├── migrations\
│   ├── 00_extensions_and_enums.sql        # Extensiones y los 32 tipos ENUM
│   ├── 01_admin_config.sql                # Company, Company Bank Accounts, Currencies, Holidays, Catálogos
│   ├── 02_security_users.sql              # Users, Refresh Tokens, Audit Logs
│   ├── 03_customers_kyc.sql               # Customers, Customer Bank Accounts, Customer Documents, OTP
│   ├── 04_loan_products.sql               # Catálogo de Productos y Reglas Financieras
│   ├── 05_loan_applications.sql           # Solicitudes de Préstamo y Evaluación (DTI)
│   ├── 06_loans_schedules.sql             # Loans, Disbursements, Schedules (Snapshot), Installments, Prepago
│   ├── 07_payments_ledger.sql             # Payments, Payment Allocations (Prelación), Transactions
│   ├── 08_treasury_reconciliation.sql     # Bank Statements (extractos) — base de la conciliación bancaria
│   ├── 09_collections_notifications.sql   # Collection Actions, Promesas de Pago, Notificaciones
│   └── 10_integrations_reconciliation.sql # Integration Events (Webhooks, reintentos y DLQ)
├── indexes\
│   └── 11_indexes.sql                     # Índices B-Tree y Partial Indexes de alto rendimiento
├── seeds\
│   └── 12_seed_data.sql                   # Datos maestros iniciales, moneda, feriados, admin inicial
└── docs\                                  # Documentación técnica completa
    ├── README.md
    ├── diagrama-entidad-relacion.md
    ├── diccionario-de-datos.md
    ├── relaciones-y-claves-foraneas.md
    └── triggers-y-automatizaciones.md
```

---

## 4. Instrucciones de Despliegue en PostgreSQL

Para inicializar la base de datos limpia desde la consola o terminal:

```bash
# Crear base de datos
createdb -U postgres sistema_prestamos

# Ejecutar el script orquestador maestro
psql -U postgres -d sistema_prestamos -f database/run_all.sql
```

Alternativamente, dentro de una sesión interactiva de `psql`:

```sql
\c sistema_prestamos
\i database/run_all.sql
```
