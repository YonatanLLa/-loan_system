# LoanSystem.Modules.Reports.Core

## 1. ¿Para qué sirve?
`LoanSystem.Modules.Reports.Core` es el **Núcleo de Dominio y Aplicación del Módulo de Reportes y Analítica Operativa/Regulatoria (`Reports`)**. Dentro de nuestra Arquitectura Hexagonal, este proyecto modela las definiciones, filtros, indicadores clave (KPIs de cartera, cosechas, morosidad PAR 30/60/90, recaudación) y contratos de generación de reportes ejecutivos y regulatorios sin sobrecargar los módulos transaccionales.

---

## 2. ¿Qué hace?

### Responsabilidades actuales y archivos `.cs`

- **`Ports/Outbound/IReportsUnitOfWork.cs`**
  - Puerto de salida que hereda de `IUnitOfWork` para persistir instantáneas de reportes (*report snapshots*), ejecuciones programadas o proyecciones de lectura materializadas del módulo.

- **`Ports/Outbound/IReportsOutbox.cs`**
  - Puerto de salida que hereda de `IOutboxRepository` para publicar eventos de integración cuando un reporte pesado o cierre regulatorio ha terminado de generarse (por ejemplo, para que `Notifications` envíe el enlace de descarga al usuario solicitante).

### ¿Qué se agrega aquí conforme crece el módulo?
- **Modelos de Lectura y Contratos (`Contracts/` y `Domain/`):**
  - `PortfolioSummaryReport` (cartera activa, cartera atrasada por tramos `1-30`, `31-60`, `61-90`, `90+`, índice de morosidad en `decimal`), `DailyCashFlowReport`, `RegulatoryCreditReport`.
- **Puertos de Entrada (`Ports/Inbound/`) y Casos de Uso (`Application/`):**
  - `IGetPortfolioSummaryUseCase`, `IGenerateAgingReportUseCase`, `IGenerateDisbursementsAndCollectionsReportUseCase`.
- **Puertos de Salida de Consulta (`Ports/Outbound/`):**
  - Interfaces de lectura optimizada (`IPortfolioReadRepository`) implementadas en `Reports.Adapters`.

---

## 3. ¿Por qué está diseñado así?

1. **Separación CQRS (Consultas Analíticas vs Transacciones Operativas):**
   Los reportes gerenciales y regulatorios suelen requerir agregaciones masivas que, si se mezclaran con las entidades de dominio de `Loans.Core` o `Payments.Core`, complicarían los agregados transaccionales. Aislar `Reports.Core` permite optimizar las proyecciones y cálculos de KPIs manteniendo intacta la pureza de los demás módulos.
2. **Exactitud en Indicadores Financieros (`decimal` / `Money`):**
   Todos los totales de cartera y porcentajes de morosidad (PAR) se calculan con `Money` y `decimal`, evitando discrepancias de redondeo entre los reportes gerenciales y el libro mayor.
