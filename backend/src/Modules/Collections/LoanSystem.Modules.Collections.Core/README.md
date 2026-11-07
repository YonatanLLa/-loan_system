# LoanSystem.Modules.Collections.Core

## 1. ¿Para qué sirve?
`LoanSystem.Modules.Collections.Core` es el **Núcleo de Dominio y Aplicación del Módulo de Cobranzas y Mora (`Collections`)**. En la Arquitectura Hexagonal del sistema, este proyecto concentra la lógica de negocio para el cálculo diario de intereses moratorios, la clasificación de cartera vencida por tramos de antigüedad (*aging buckets*: `1-30`, `31-60`, `61-90`, `90+` días), la apertura/seguimiento de casos de cobranza y el registro de promesas de pago.

---

## 2. ¿Qué hace?

### Responsabilidades actuales y archivos `.cs`

- **`Ports/Inbound/ICalculateDailyMoraUseCase.cs`**
  - Define el puerto de entrada para el proceso de cálculo diario de mora (`IUseCase<CalculateDailyMoraResponse>`) junto con el contrato `CalculateDailyMoraResponse(DateOnly ExecutionDateLima, int ProcessedSchedulesCount, Money TotalMoraAccrued)`.

- **`Application/CalculateDailyMoraUseCase.cs`**
  - Caso de uso `internal sealed` que orquesta el corte diario de mora:
    - Obtiene la fecha contable actual en zona horaria peruana (`_clock.LimaToday`) a través del puerto `IClock`.
    - Coordina la evaluación de cuotas vencidas, el devengo de mora usando aritmética `Money` (`decimal` puro, redondeo a 4 decimales en cálculo intermedio y 2 decimales en moneda final) y la actualización de los tramos de morosidad (*aging buckets*).
    - Persiste los cambios y eventos de integración de forma atómica mediante `ICollectionsUnitOfWork` e `ICollectionsOutbox`.

- **`Ports/Outbound/ICollectionsUnitOfWork.cs` y `Ports/Outbound/ICollectionsOutbox.cs`**
  - Puertos de salida del módulo que heredan de `IUnitOfWork` e `IOutboxRepository` para garantizar que toda actualización de mora, cambio de tramo de cobranza o registro de promesa de pago se confirme transaccionalmente junto con sus eventos en el Outbox.

### ¿Qué se agrega aquí conforme crece el módulo?
- **Entidades y Objetos de Valor (`Domain/`):**
  - `CollectionCase` (caso de cobranza activo por préstamo en mora), `AgingBucket` (`Days1To30`, `Days31To60`, `Days61To90`, `Days90Plus`), `CollectionAction` (gestiones telefónicas/visitas de cobradores con rol `COLLECTOR`) y `PaymentPromise` (compromisos de pago con fecha límite y monto).
- **Casos de Uso adicionales (`Application/`):**
  - Asignación de cartera a gestores (`AssignCollectorUseCase`), registro de gestión de cobranza (`RecordCollectionActionUseCase`), registro y verificación de promesas de pago (`RegisterPaymentPromiseUseCase`) y manejadores de eventos de integración cuando el módulo `Payments` notifica que un cliente pagó su deuda vencida (`PaymentAppliedIntegrationEventHandler`).

---

## 3. ¿Por qué está diseñado así?

1. **Independencia del Disparador (Cron Job o HTTP):**
   `CalculateDailyMoraUseCase` implementa `ICalculateDailyMoraUseCase` sin saber que es invocado a las `00:05 America/Lima` por un Job de Quartz en `LoanSystem.Workers`. Gracias a ello, el mismo caso de uso puede probarse unitariamente en milisegundos o ejecutarse manualmente desde un endpoint administrativo de contingencia.
2. **Corte Contable Exacto con `IClock.LimaToday`:**
   En el negocio de préstamos en Perú, una cuota entra en mora al primer minuto del día siguiente a su fecha de vencimiento (`due_date < LimaToday`). Al depender de `IClock`, el dominio nunca usa la hora local del servidor físico sino siempre la fecha oficial de Lima.
3. **Integración Asíncrona vía Outbox:**
   Cuando un préstamo entra en mora o cambia de *aging bucket* (ej. pasa a `61-90` o `90+`), `Collections` no modifica directamente las tablas de `Loans` ni llama directamente a `Notifications`; encola un evento de integración en `ICollectionsOutbox` para mantener el bajo acoplamiento entre módulos.
