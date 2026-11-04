# Dominio 06: Aplicación de Pagos, Prelación y Reversos

## 1. Descripción del Dominio
Este dominio gobierna el núcleo transaccional más sensible del sistema: el procesamiento y la distribución de los abonos monetarios efectuados por los clientes. Garantiza la aplicación rigurosa de la regla de prelación financiera, el control de concurrencia para evitar dobles cobros y el principio de inmutabilidad transaccional mediante contra-asientos.

---

## 2. Historias de Usuario

### US-16: Registro de Pago con Cascada Estricta de Prelación
**Como** Cajero u Operador de Recaudación,  
**Quiero** asentar un abono monetario a un préstamo específico,  
**Para** amortizar las obligaciones del cliente siguiendo exactamente la jerarquía legal de imputación de pagos.

#### Requerimientos Funcionales
- **RF-16.1:** Endpoint de cobro: `POST /api/loans/{id}/payments`.
- **RF-16.2:** Parámetros de entrada: `amount`, `payment_method` (EFECTIVO, TRANSFERENCIA, YAPE, PLIN, TARJETA), `reference` (código de operación o voucher), `paid_at` (fecha/hora efectiva), `idempotency_key`.
- **RF-16.3:** Control de concurrencia: El procesamiento debe aplicar **bloqueo pesimista de fila** (`SELECT ... FOR UPDATE` sobre el registro del `Loan`) durante la transacción de cobro, evitando que dos solicitudes concurrentes imputen la misma cuota en simultáneo.
- **RF-16.4:** Regla de Oro de Prelación / Cascada de Imputación (Inviolable):
  El monto recibido se distribuye en las cuotas pendientes (ordenadas cronológicamente por fecha de vencimiento más antigua) bajo este orden jerárquico estricto:
  1. **Mora y penalidades acumuladas** (`late_fee`).
  2. **Intereses vencidos y corrientes** (`interest`).
  3. **Comisiones y seguros vencidos** (`fee`).
  4. **Amortización a Capital** (`principal`).
  *Bajo ninguna circunstancia un pago amortizará capital antes de liquidar la mora e intereses vencidos correspondientes.*
- **RF-16.5:** Generación de detalle de imputación (`PaymentAllocation`):
  Por cada cuota afectada se registra cuánto dinero fue a mora (`to_mora`), cuánto a interés (`to_interest`), cuánto a comisión (`to_fee`) y cuánto a capital (`to_principal`).
- **RF-16.6:** Actualización de estados de cuota:
  - Si el pago cubre la totalidad del importe adeudado de la cuota $\to$ `status = PAID`.
  - Si el pago es menor al saldo total de la cuota $\to$ `status = PARTIAL` (el saldo remanente continúa exigible).
- **RF-16.7:** Si todas las cuotas del cronograma quedan con estado `PAID` $\to$ El préstamo transiciona automáticamente a estado `PAGADO`.

#### Criterios de Aceptación
- **CA-16.1 (Aplicación en Cascada de Cuota en Mora):**  
  *Dado que* una cuota adeuda \$ 10 de mora, \$ 20 de interés, \$ 5 de comisión y \$ 100 de capital (total \$ 135),  
  *Cuando* el cliente realiza un abono parcial de \$ 30,  
  *Entonces* se liquidan \$ 10 de mora, \$ 20 de interés, la cuota pasa a `PARTIAL`, y el capital (\$ 100) y la comisión (\$ 5) quedan pendientes.
- **CA-16.2 (Pago Completo y Cierre):**  
  *Dado que* el saldo final del préstamo es \$ 135 y el cliente paga \$ 135,  
  *Cuando* se procesa el cobro,  
  *Entonces* la cuota queda `PAID`, el saldo del préstamo llega a \$ 0.00 y el préstamo pasa a estado `PAGADO`.

---

### US-17: Tratamiento de Pagos Mayores y Saldos a Favor (Sobrepagos)
**Como** Sistema Financiero,  
**Quiero** procesar adecuadamente cualquier abono que supere el monto de la cuota actual o del préstamo total,  
**Para** adelantar cuotas consecutivas o generar un saldo acreedor a favor del cliente sin descuadrar la contabilidad.

#### Requerimientos Funcionales
- **RF-17.1:** Cascada continua entre cuotas: Si el monto abonado excede la cuota exigible más antigua, el excedente se aplica automáticamente a la siguiente cuota pendiente en el cronograma bajo la misma jerarquía de prelación.
- **RF-17.2:** Saldo a Favor del Cliente: Si el monto pagado excede la deuda total restante de todo el crédito (capital + intereses devengados + mora):
  - El préstamo se marca como `PAGADO`.
  - El excedente neto se registra en la cuenta corriente del cliente como `saldo_a_favor` (*Customer credit balance*).
  - Se genera una transacción contable de tipo `CUSTOMER_CREDIT_BALANCE`.
- **RF-17.3:** El saldo a favor puede ser utilizado en el futuro para pagar otros créditos del mismo cliente o reembolsado mediante solicitud de egreso de caja.

#### Criterios de Aceptación
- **CA-17.1 (Adelanto de Cuota Siguiente):**  
  *Dado que* la cuota 1 es de \$ 100 y la cuota 2 es de \$ 100,  
  *Cuando* el cliente entrega \$ 150,  
  *Entonces* la cuota 1 queda `PAID` (cubre \$ 100) y la cuota 2 queda `PARTIAL` con \$ 50 amortizados.
- **CA-17.2 (Sobrepago Total):**  
  *Dado que* a un préstamo le restaban \$ 80 para cancelarse totalmente y el cliente deposita \$ 100,  
  *Cuando* se procesa el cobro,  
  *Entonces* el préstamo pasa a `PAGADO`, y los \$ 20 sobrantes se abonan a `Customer.credit_balance`.

---

### US-18: Reverso y Anulación de Pagos con Contra-Asientos
**Como** Administrador o Supervisor de Operaciones,  
**Quiero** revertir un pago registrado erróneamente (por ejemplo, digitación incorrecta del monto o cheque rebotado),  
**Para** restituir el saldo y las cuotas a su estado anterior sin alterar la historia ni borrar registros de la base de datos.

#### Requerimientos Funcionales
- **RF-18.1:** Principio de inmutabilidad contable: **Está terminantemente prohibido ejecutar sentencias `DELETE` o `UPDATE` destructivas sobre la tabla `Payment` o `PaymentAllocation`**.
- **RF-18.2:** Endpoint de reversión: `POST /api/payments/{id}/reverse`.
- **RF-18.3:** Requisitos para reversión:
  - Justificación obligatoria (`reversal_reason`).
  - Autorización exclusiva para roles con permiso `payments.reverse` (Supervisor / Admin).
- **RF-18.4:** Mecanismo técnico del reverso:
  - Se crea un nuevo registro en `Payment` con monto negativo o de tipo `REVERSAL`.
  - Se genera una transacción espejo en el libro mayor (`Transaction`) con dirección contraria (`OUTFLOW` si el pago original fue `INFLOW`).
  - Se devuelven los saldos a las cuotas (`Installment`): se resta lo pagado en `paid_principal`, `paid_interest`, etc., y se recalculan los estados correspondientes (`PARTIAL`, `OVERDUE` o `PENDING`).
  - Si el préstamo estaba en `PAGADO`, vuelve a su estado anterior (`VIGENTE` o `EN_MORA`).

#### Criterios de Aceptación
- **CA-18.1 (Reverso Exitoso con Contra-Asiento):**  
  *Dado que* se registró un pago erróneo de \$ 500 que marcó una cuota como `PAID`,  
  *Cuando* el supervisor ejecuta el reverso con motivo "Voucher digitado por error",  
  *Entonces* se genera el contra-asiento, la cuota regresa a su saldo original de \$ 500 y estado `OVERDUE`, y la auditoría registra la operación reversada.
- **CA-18.2 (Intento de Doble Reverso):**  
  *Dado que* un pago ya fue revertido con anterioridad,  
  *Cuando* se intenta invocar el reverso nuevamente,  
  *Entonces* el sistema retorna `409 Conflict` ("El pago ya se encuentra revertido").

---

## 3. Requisitos No Funcionales del Dominio
- **RNF-12 (Aislamiento y Concurrencia):** Nivel de aislamiento de transacciones de cobro en `ReadCommitted` con bloqueo pesimista de fila, garantizando resistencia a condiciones de carrera (*Race Conditions*).
- **RNF-13 (Comprobante Fiscal/Recibo Digital):** Todo pago exitoso debe generar inmediatamente un número de recibo correlativo único descargable en PDF y enviable por notificación al cliente.
