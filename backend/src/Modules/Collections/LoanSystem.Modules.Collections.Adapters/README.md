# LoanSystem.Modules.Collections.Adapters

## 1. ¿Para qué sirve?
`LoanSystem.Modules.Collections.Adapters` es la **Capa de Adaptadores (Entrada y Salida)** del módulo de Cobranzas y Mora (`Collections`). Su propósito es conectar el núcleo `LoanSystem.Modules.Collections.Core` con:
1. **Adaptadores de Entrada (Inbound):** Los endpoints HTTP bajo `/api/v1/collections` (para gestores con rol `COLLECTOR`, analistas y administradores) y el Job programado `DailyMoraCalculationJob` en Quartz.NET.
2. **Adaptadores de Salida (Outbound):** La persistencia en PostgreSQL sobre el esquema `collections` (`CollectionsDbContext`).

---

## 2. ¿Qué hace?

### Responsabilidades actuales y archivos `.cs`

- **`CollectionsModule.cs`**
  - Clase pública de composición del módulo que registra en DI `CollectionsDbContext`, los puertos de salida (`ICollectionsUnitOfWork`, `ICollectionsOutbox`) y los casos de uso (`ICalculateDailyMoraUseCase` → `CalculateDailyMoraUseCase`), además de exponer `MapCollectionsEndpoints`.

- **`Inbound/Http/CollectionsEndpoints.cs`**
  - Define el grupo de endpoints Minimal API `/api/v1/collections` con tag OpenAPI `"Collections"`, listo para exponer las consultas de cartera en mora, asignación de casos y registro de promesas de pago.

- **`Inbound/Jobs/DailyMoraCalculationJob.cs`**
  - Adaptador primario para Quartz.NET (`IJob`) decorado con `[DisallowConcurrentExecution]`.
  - Se despierta según el cronograma configurado, inyecta `ICalculateDailyMoraUseCase` y `ILogger<DailyMoraCalculationJob>`, ejecuta el cálculo diario de mora y registra en el log estructurado la fecha de ejecución en Lima, la cantidad de cronogramas procesados y la mora total devengada (o el detalle del error si el `Result` falla).

- **`Inbound/Jobs/CollectionsJobs.cs`**
  - Método de extensión `AddCollectionsJobs(this IServiceCollectionQuartzConfigurator quartz)` invocado por `LoanSystem.Workers`.
  - Registra `DailyMoraCalculationJob` con la expresión Cron **`0 5 0 * * ?`** (todos los días a las **00:05 AM**) anclada explícitamente a **`LimaTimeZone.Instance` (`America/Lima`)**.

- **`Outbound/Persistence/CollectionsDbContext.cs`**
  - Hereda de `ModuleDbContext` y establece el esquema por defecto **`collections`** (`modelBuilder.HasDefaultSchema("collections")`), donde residen las tablas definidas en `database/migrations/09_collections_notifications.sql` (`collection_cases`, `collection_actions`, `payment_promises`, etc.).

### ¿Qué se agrega aquí conforme crece el módulo?
- Endpoints HTTP protegidos con políticas RBAC (`COLLECTOR`, `ADMIN`, `ANALYST`) para gestionar casos de cobranza y promesas de pago.
- Repositorios concretos (`CollectionCaseRepository`) y mapeos de entidades de EF Core o consultas SQL optimizadas con `NpgsqlConnectionFactory` para el cálculo masivo de mora por lotes.
- Manejadores de eventos de integración (`Inbound/Events/`) que reaccionen a pagos aplicados o desembolsos.

---

## 3. ¿Por qué está diseñado así?

1. **Prevención de Ejecución Concurrente (`[DisallowConcurrentExecution]`):**
   El cálculo de mora diaria no debe solaparse jamás consigo mismo si un lote tarda más de lo esperado. El atributo `[DisallowConcurrentExecution]` en `DailyMoraCalculationJob` garantiza que Quartz no inicie una segunda instancia simultánea.
2. **Cron Job como Adaptador Primario Puro:**
   `DailyMoraCalculationJob` tiene menos de 35 líneas porque no contiene lógica de negocio ni consultas SQL: simplemente adapta la interfaz `IJob` de Quartz hacia el puerto `ICalculateDailyMoraUseCase` del dominio.
3. **Ejecución a las 00:05 `America/Lima`:**
   Los 5 minutos posteriores a la medianoche (`00:05`) brindan una ventana de seguridad para que concluyan las transacciones de pago iniciadas a las `23:59:59` del día anterior antes de clasificar las cuotas impagas como vencidas (`overdue`).
