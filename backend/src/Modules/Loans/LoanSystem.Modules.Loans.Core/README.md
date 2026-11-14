# LoanSystem.Modules.Loans.Core

## 1. ¿Para qué sirve?
`LoanSystem.Modules.Loans.Core` es el **Núcleo de Dominio y Aplicación del Módulo de Préstamos y Cartera Activa (`Loans`)**. Dentro de nuestra Arquitectura Hexagonal, este proyecto representa el corazón del ciclo de vida de un crédito una vez que la solicitud ha sido aprobada en `Origination`: gestiona el contrato de préstamo (`Loan`), su desembolso (`Disbursement`), su cronograma vigente de cuotas (`LoanSchedule` e `Installment`), los estados del préstamo (`Active`, `Overdue`, `PaidOff`, `Refinanced`, `WrittenOff`) y las operaciones de reprogramación o refinanciamiento.

---

## 2. ¿Qué hace?

### Contratos públicos y puertos actuales (`Contracts/` y `Ports/`)

- **`Contracts/ILoanPortfolioQuery.cs`**
  - Contrato público (`GetByCustomerAsync`) y DTO `LoanSummary` expuestos por `Loans` para lectura entre módulos desde la Ficha 360 de `Customers` (`BE-CUS-08`), preservando el aislamiento entre esquemas.

- **`Ports/Outbound/ILoansUnitOfWork.cs`**
  - Puerto de salida que extiende `IUnitOfWork` para garantizar que la creación del préstamo, la generación de sus cuotas, el registro del desembolso y cualquier actualización de saldos de capital/interés/mora se guarden en una única transacción atómica.

- **`Ports/Outbound/ILoansOutbox.cs`**
  - Puerto de salida que extiende `IOutboxRepository` para publicar eventos de integración clave del ciclo de vida del crédito (ej. `LoanCreatedIntegrationEvent`, `LoanDisbursedIntegrationEvent`, `LoanPaidOffIntegrationEvent`, `LoanRefinancedIntegrationEvent`) dentro de la misma transacción de base de datos.

### Requisito obligatorio para el futuro caso de uso de desembolso (`BE-CUS-06`)
- Todo caso de uso de desembolso en `Loans` **debe** invocar `ICustomerDisbursementEligibility.CheckAsync(customerId, bankAccountId, ct)` dentro de su transacción:
  - Si `IsEligible` es `false`, rechazar con HTTP `422 Unprocessable Entity`, código `Loans.CustomerNotEligible`, `extensions.reasons` devolviendo los códigos recibidos (`KYC_NOT_VERIFIED`, `KYC_EXPIRED`, `ACCOUNT_NOT_FOUND`, `ACCOUNT_NOT_OWNED`, `ACCOUNT_NOT_VERIFIED`, `ACCOUNT_INACTIVE`, `ACCOUNT_IN_COOLING_PERIOD`), y detalle `"Cliente con KYC pendiente"` cuando corresponda a KYC (`CA-05.2`).
- Al diseñar o migrar la tabla `loans`, incorporar la FK compuesta `(customer_id, customer_bank_account_id)` hacia `uq_customer_bank_accounts_customer_id` en `customer_bank_accounts` como defensa adicional en BD (`DB-42`).

### ¿Qué se agrega aquí conforme crece el módulo?
- **Entidades y Raíces de Agregado (`Domain/`):**
  - `Loan` (Aggregate Root con número de préstamo correlativo, referencia al cliente y solicitud aprobada, monto principal en `Money`, TEA/TEM en `decimal`, saldo de capital vigente y estado del crédito).
  - `LoanSchedule` y `LoanInstallment` (cuotas numeradas con fecha de vencimiento `DateOnly`, capital programado, interés programado, mora acumulada, montos pagados y estado de cuota `Pending`, `Partial`, `Paid`, `Overdue`).
  - `LoanDisbursement` (orden de desembolso con canal, cuenta destino y estado `Pending`, `InTransit`, `Completed`, `Failed`).
  - `LoanRefinancing` (registro histórico y condiciones cuando un préstamo vencido se reestructura).
- **Puertos de Entrada (`Ports/Inbound/`) y Casos de Uso (`Application/`):**
  - `ICreateLoanFromApprovedApplicationUseCase`, `IDisburseLoanUseCase`, `IGetLoanDetailsUseCase`, `IGetLoanScheduleUseCase`, `IRefinanceLoanUseCase`.
  - Manejadores de eventos de integración cuando `Payments` aplica un cobro sobre las cuotas o cuando `Collections` actualiza la mora devengada.

---

## 3. ¿Por qué está diseñado así?

1. **Separación entre Originación (`Origination`) y Gestión de Cartera (`Loans`):**
   En la industria financiera, el flujo de evaluación y aprobación de una solicitud (*Origination/Underwriting*) tiene reglas, estados y actores (`ANALYST`, `APPROVER`) completamente distintos a la administración contable y financiera de un préstamo ya desembolsado (*Loan Servicing*). Separar `Origination` y `Loans` mantiene cada modelo de dominio cohesivo y enfocado.
2. **Precisión Matemática Estricta (`Money` y `decimal`):**
   Todos los saldos de capital insoluto, intereses devengados, cargos y moras por cuota en `Loans.Core` se operan con `Money` (`RoundCurrency` a 2 decimales y `RoundCalculation` a 4 decimales), prohibiendo `float`/`double` a nivel de bytecode IL.
3. **Consistencia Eventual Segura vía Outbox:**
   Cuando un préstamo se desembolsa en `Loans`, el evento `LoanDisbursedIntegrationEvent` se inserta en `public.outbox_messages` en la misma transacción que actualiza el préstamo, permitiendo que `Treasury`, `Payments` (asiento contable) y `Notifications` reaccionen con garantía de entrega (*at-least-once*).
