# Dominio 10: Cancelación Anticipada, Reestructuración y Refinanciamiento

## 1. Descripción del Dominio
Este dominio regula las alteraciones contractuales en la vida de un crédito activo: la liquidación anticipada total (prepago con descuento de intereses no devengados), el abono extraordinario a capital con recálculo de cuotas futuras, la reestructuración de condiciones (plazo/tasa) ante problemas de liquidez y el refinanciamiento mediante un nuevo crédito consolidado.

---

## 2. Historias de Usuario

### US-30: Cotización y Liquidación por Cancelación Anticipada (*Prepago*)
**Como** Cliente o Cajero de Ventanilla,  
**Quiero** solicitar una cotización exacta de liquidación total a la fecha de hoy y registrar el pago cancelatorio,  
**Para** extinguir la deuda de forma prematura ahorrando los intereses futuros no devengados.

#### Requerimientos Funcionales
- **RF-30.1:** Endpoint de cotización: `POST /api/loans/{id}/quote-prepayment`.
  - Parámetro de entrada: `as_of_date` (fecha proyectada de pago).
- **RF-30.2:** Reglas de cálculo según método de amortización del producto:
  - **Método Francés / Alemán:**
    $$\text{Monto Liquidación} = \text{Saldo Capital Insoluto} + \text{Interés Devengado a la Fecha} + \text{Mora Acumulada} + \text{Penalidad Prepago (si aplica)}$$
    *Los intereses pactados de las cuotas futuras no devengadas se eliminan/condonan en favor del prestatario.*
  - **Método Flat / Simple (Interés Precomputado):** Depende de la política configurada en el producto (cobro del total pactado o descuento proporcional reglamentario).
- **RF-30.3:** Vigencia de la cotización: La liquidación emitida tiene validez estricta hasta la fecha indicada (`quote_valid_until`), típicamente el fin de la jornada.
- **RF-30.4:** Al registrarse el pago por el monto exacto de la liquidación cotizada:
  - Todas las cuotas futuras pasan a estado `CANCELLED_BY_PREPAYMENT`.
  - El préstamo transiciona a estado `PAGADO` con motivo `LIQUIDACION_ANTICIPADA`.

#### Criterios de Aceptación
- **CA-30.1 (Ahorro por Prepago Francés):**  
  *Dado que* un cliente con crédito francés debe \$ 4,000 de capital insoluto y \$ 1,200 de intereses futuros no devengados,  
  *Cuando* solicita la cotización de prepago al día de hoy,  
  *Entonces* el sistema cotiza \$ 4,000 (más intereses devengados hasta hoy), exonerando los \$ 1,200 futuros.
- **CA-30.2 (Cancelación Exitosa):**  
  *Dado que* se cotizó una liquidación de \$ 4,050 válida por el día de hoy,  
  *Cuando* el cliente realiza el abono de \$ 4,050,  
  *Entonces* el préstamo se extingue, queda en `PAGADO` y emite constancia de no adeudo.

---

### US-31: Reestructuración de Crédito Vigente
**Como** Analista de Crédito o Gestor de Cobranzas,  
**Quiero** renegociar las condiciones de un crédito vigente (ampliar plazo, ajustar tasa o conceder período de gracia) y generar un nuevo cronograma,  
**Para** adecuar la carga financiera a la nueva capacidad de pago del cliente evitando que caiga en impago definitivo.

#### Requerimientos Funcionales
- **RF-31.1:** Endpoint de reestructuración: `POST /api/loans/{id}/restructure`.
- **RF-31.2:** Validaciones y condiciones de negocio:
  - El préstamo debe estar en estado `VIGENTE` o `EN_MORA` dentro del límite de días permitido por política (ej. no más de 60 días de mora).
  - Puede requerirse un pago inicial obligatorio (abono de la mora previa) como condición sine qua non para reestructurar.
- **RF-31.3:** Trazabilidad e historial de cronogramas:
  - El cronograma anterior se congela y archiva con su versión (`snapshot_version = 1`).
  - Se genera una nueva versión del cronograma (`snapshot_version = 2`) con el nuevo plazo y tasa acordados, tomando como saldo inicial el capital adeudado actual.
  - El identificador del préstamo (`loan_id`) se conserva para mantener el historial.

#### Criterios de Aceptación
- **CA-31.1 (Reestructuración con Ampliación de Plazo):**  
  *Dado que* a un cliente le restaban 6 cuotas de \$ 500 y no puede pagar,  
  *Cuando* el analista reestructura el saldo restante a 12 cuotas con nueva tasa aprobada,  
  *Entonces* se crea el `Schedule v2` con cuotas de menor importe, el préstamo vuelve a estado `VIGENTE` y se archiva el histórico.

---

### US-32: Refinanciamiento / Consolidación de Deudas
**Como** Analista de Crédito,  
**Quiero** originar un nuevo préstamo que cancele automáticamente uno o más préstamos anteriores del cliente,  
**Para** unificar sus obligaciones en una sola cuota accesible y, opcionalmente, otorgarle capital de trabajo adicional (*Cash-Out*).

#### Requerimientos Funcionales
- **RF-32.1:** Proceso de solicitud con tipo especial `REFINANCING` o `CONSOLIDATION`.
- **RF-32.2:** Selección de préstamos a cancelar: El analista selecciona los créditos activos del cliente a liquidar.
- **RF-32.3:** Desembolso y cancelación atómica:
  - `Monto Nuevo Préstamo = Suma Liquidaciones de Préstamos Previos + Efectivo Adicional al Cliente`.
  - En la transacción atómica de desembolso:
    1. Se liquidan y marcan como `PAGADO` los préstamos anteriores por concepto `REFINANCIAMIENTO`.
    2. Se crea y activa el nuevo préstamo con su propio cronograma.
    3. Si hubo monto adicional, se entrega al cliente o se deposita en su cuenta.

#### Criterios de Aceptación
- **CA-32.1 (Consolidación Exitosa de Dos Préstamos):**  
  *Dado que* un cliente tiene Préstamo A (saldo \$ 1,000) y Préstamo B (saldo \$ 1,500),  
  *Cuando* se aprueba un refinanciamiento por \$ 3,000 (incluyendo \$ 500 de nuevo desembolso),  
  *Entonces* los Préstamos A y B quedan en estado `PAGADO`, se crea el Préstamo C por \$ 3,000 y se desembolsan \$ 500 líquidos al cliente.

---

## 3. Requisitos No Funcionales del Dominio
- **RNF-20 (Auditoría Reforzada):** Toda reestructuración o refinanciamiento debe registrar un dictamen formal firmado digitalmente por el comité o jefe de créditos en el `AuditLog`.
- **RNF-21 (Consistencia Transaccional):** La cancelación de préstamos anteriores y el desembolso del crédito nuevo deben ocurrir en la misma unidad atómica transaccional de PostgreSQL.
