# LoanSystem.Modules.Payments.Core

## 1. ¿Para qué sirve?
`LoanSystem.Modules.Payments.Core` es el **Núcleo de Dominio y Aplicación del Módulo de Pagos y Libro Mayor Contable (`Payments`)**. Dentro de nuestra Arquitectura Hexagonal, este proyecto modela la recepción y aplicación de cobros de préstamos, el motor determinista de **prelación de imputación de pagos (`Mora → Interés → Capital`)**, la emisión de comprobantes/recibos de pago (`PaymentReceipt`) y la generación de asientos contables inmutables de partida doble (`AccountingEntry`) en el libro mayor (*Ledger*).

---

## 2. ¿Qué hace?

### Responsabilidades actuales y archivos `.cs`

- **`Ports/Outbound/IPaymentsUnitOfWork.cs`**
  - Puerto de salida que hereda de `IUnitOfWork` para garantizar que el registro del pago, el desglose de imputación por cuota, la emisión del recibo y los asientos contables (`accounting_entries`) se persistan en una única transacción atómica ACID.

- **`Ports/Outbound/IPaymentsOutbox.cs`**
  - Puerto de salida que hereda de `IOutboxRepository` para publicar eventos de integración (`PaymentAppliedIntegrationEvent`, `PaymentReversedIntegrationEvent`) hacia `Loans` (actualización de saldos de cuota), `Collections` (cierre de promesas de pago o salida de mora), `Treasury` (recaudación del día) y `Notifications` (envío de constancia de pago).

### ¿Qué se agrega aquí conforme crece el módulo?
- **Entidades y Servicios de Dominio (`Domain/`):**
  - `Payment` (Aggregate Root con monto total pagado en `Money`, canal de pago, referencia de operación/idempotencia, cajero ejecutor y estado).
  - `PaymentAllocation` (desglose exacto en `Money` aplicado a cada cuota siguiendo la prelación estricta de negocio: **1° Interés Moratorio / Mora (`late_fee_paid`) → 2° Interés Compensatorio (`interest_paid`) → 3° Capital Principal (`principal_paid`)**).
  - `PaymentReceipt` (comprobante inmutable emitido al cliente).
  - `AccountingEntry` (asiento contable *append-only* con cuentas Debe/Haber y monto en `Money`, donde las correcciones se realizan exclusivamente mediante asientos de extorno/reversión y jamás con `UPDATE`/`DELETE`).
- **Puertos de Entrada (`Ports/Inbound/`) y Casos de Uso (`Application/`):**
  - `IRegisterLoanPaymentUseCase`, `IPreviewPaymentAllocationUseCase`, `IReversePaymentUseCase`, `IGetPaymentHistoryUseCase`.

---

## 3. ¿Por qué está diseñado así?

1. **Prelación Legal y Financiera (`Mora → Interés → Capital`):**
   Cuando un cliente realiza un pago parcial o total sobre cuotas vencidas, la regla financiera exige cubrir primero la mora acumulada, luego el interés compensatorio devengado y finalmente amortizar el capital principal, comenzando por la cuota más antigua vencida. Encapsular este algoritmo en `Payments.Core` utilizando `Money` (`decimal` puro con redondeo `RoundCurrency` a 2 decimales) asegura precisión al céntimo.
2. **Inmutabilidad del Libro Mayor (`accounting_entries`):**
   En contabilidad bancaria, los asientos contables nunca se sobrescriben ni se borran (la tabla `payments.accounting_entries` tiene un trigger en PostgreSQL que bloquea `UPDATE` y `DELETE`). Cualquier anulación o extorno genera un nuevo movimiento compensatorio en `Payments.Core`.
3. **Cero Punto Flotante Binario:**
   Toda distribución de pagos utiliza `Money.Allocate` y aritmética `decimal`, validada en CI por `FloatingPointIlScanner`.
