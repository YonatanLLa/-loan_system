# LoanSystem.Modules.Treasury.Core

## 1. ¿Para qué sirve?
`LoanSystem.Modules.Treasury.Core` es el **Núcleo de Dominio y Aplicación del Módulo de Tesorería y Conciliación (`Treasury`)**. Dentro de nuestra Arquitectura Hexagonal, este proyecto modela el control de liquidez, el cierre y cuadre diario de saldos (`DailyBalance`), el seguimiento de desembolsos bancarios en tránsito (`DisbursementsInTransit`) y las reglas de conciliación entre los registros internos del sistema y los extractos/confirmaciones bancarias.

---

## 2. ¿Qué hace?

### Responsabilidades actuales y archivos `.cs`

- **`Ports/Inbound/IRunDailyBalanceUseCase.cs`**
  - Define el puerto de entrada para ejecutar el cuadre diario de tesorería (`IUseCase<RunDailyBalanceResponse>`) y su respuesta `RunDailyBalanceResponse(DateOnly BalanceDateLima, Money TotalDisbursed, Money TotalCollected, bool IsBalanced)`.

- **`Application/RunDailyBalanceUseCase.cs`**
  - Caso de uso `internal sealed` que ejecuta el cierre y cuadre de saldos diarios de tesorería:
    - Utiliza `IClock.LimaToday` para determinar la fecha de corte contable en Perú.
    - Consolida los totales desembolsados (`TotalDisbursed`) y recaudados (`TotalCollected`) utilizando exclusivamente el Value Object `Money` (`decimal` con redondeo financiero exacto).
    - Determina si la posición de tesorería está cuadrada (`IsBalanced`) frente a los movimientos esperados y confirma la operación mediante `ITreasuryUnitOfWork`.

- **`Ports/Inbound/ICheckDisbursementsInTransitUseCase.cs`**
  - Define el puerto de entrada para verificar desembolsos en tránsito (`IUseCase<CheckDisbursementsInTransitResponse>`) y el contrato `CheckDisbursementsInTransitResponse(DateTimeOffset CheckedAtUtc, int CheckedCount, int ConfirmedCount)`.

- **`Application/CheckDisbursementsInTransitUseCase.cs`**
  - Caso de uso `internal sealed` encargado de monitorear periódicamente (cada 15 minutos) las órdenes de desembolso que fueron enviadas a canales bancarios y permanecen en estado `in_transit`, confirmando su liquidación o disparando alertas/eventos si superan el SLA operativo.

- **`Ports/Outbound/ITreasuryUnitOfWork.cs` y `Ports/Outbound/ITreasuryOutbox.cs`**
  - Puertos de salida del módulo para asegurar transacciones atómicas y publicación de eventos de integración en el Outbox (por ejemplo, `DisbursementSettledIntegrationEvent` o `DailyBalanceClosedIntegrationEvent`).

### ¿Qué se agrega aquí conforme crece el módulo?
- **Entidades de Dominio (`Domain/`):** `DailyBalance`, `TreasuryAccount`, `BankReconciliationBatch`, `ReconciliationItem`.
- **Casos de Uso adicionales (`Application/`):** Carga y conciliación manual/automática de extractos bancarios, autorización de fondeo de cuentas operativas y resolución de partidas conciliatorias pendientes.

---

## 3. ¿Por qué está diseñado así?

1. **Exactitud Monetaria en Conciliaciones (`Money` / `decimal`):**
   Un descuadre de `S/ 0.01` en tesorería impide cerrar el balance diario (`IsBalanced = false`). Por ello, todos los acumulados de desembolsos y recaudaciones en `Treasury.Core` operan con `Money` y jamás con tipos de punto flotante binario.
2. **Separación entre Cierre Diario y Monitoreo Continuo:**
   El negocio de tesorería tiene dos ritmos operativos distintos que se reflejan en dos casos de uso separados:
   - Un proceso intradía de alta frecuencia (`CheckDisbursementsInTransitUseCase`, cada 15 min) para destrabar desembolsos de clientes.
   - Un proceso de corte diario (`RunDailyBalanceUseCase`, 06:00 AM hora Lima) para consolidar los saldos antes de la apertura de agencias/operaciones.
