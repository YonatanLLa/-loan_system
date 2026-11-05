# Dominio 05: Desembolso y Cronogramas Inmutables

## 1. Descripción del Dominio
Este dominio formaliza el nacimiento jurídico y financiero del préstamo. Se encarga de la ejecución del desembolso de fondos, la deducción de comisiones iniciales, la asignación de claves de idempotencia y la generación de la **versión definitiva e inmutable del cronograma de pagos** (*Schedule Snapshot*).

---

## 2. Historias de Usuario

### US-13: Ejecución de Desembolso de Préstamo con Idempotencia
**Como** Operador de Tesorería (o el sistema, tras la aceptación digital del contrato por el cliente),  
**Quiero** procesar el desembolso de una solicitud aprobada utilizando una clave de idempotencia,  
**Para** transferir los fondos a la cuenta verificada del cliente (CCI o billetera digital) y crear la entidad oficial de préstamo sin riesgo de duplicar la operación por caídas de red. No existen desembolsos en efectivo.

#### Requerimientos Funcionales
- **RF-13.1:** Endpoint de desembolso: `POST /api/loans`.
- **RF-13.2:** Cabecera obligatoria: `Idempotency-Key` (UUIDv4). Si la API recibe una petición con una clave ya procesada en las últimas 24 horas, debe responder el resultado idéntico anterior sin volver a crear otro crédito.
- **RF-13.3:** Validaciones previas al desembolso:
  - La solicitud debe encontrarse en estado `APROBADO` o `APROBADO_CON_CONDICIONES`.
  - El cliente debe tener `kyc_status = VERIFIED`.
  - El cliente aceptó digitalmente el contrato y la hoja resumen (OTP de conformidad, US-27 / RF-27.4).
  - La cuenta destino (`customer_bank_accounts`) pertenece al cliente, está verificada (`is_verified = true`) y opera en la moneda del préstamo.
  - La cuenta de origen (`company_bank_accounts`) está activa y habilitada para desembolsos (US-23).
- **RF-13.4:** Creación de la entidad `Loan` y de su registro `disbursements` en estado `PENDING`; la transferencia se envía al banco o pasarela de forma asíncrona (`PROCESSING`) y se confirma por webhook o extracto (`COMPLETED`). El seguimiento y los rechazos (`FAILED`) se gestionan en la bandeja de tesorería (US-25).
- **RF-13.4.1:** La fecha de inicio de intereses (`disbursement_date`) es la fecha en que el banco **confirma** la transferencia, no la fecha en que se inició.
  > Pendiente de modelado: hoy `loans.status` nace en `VIGENTE` y no existe un estado previo a la confirmación del desembolso; si la transferencia falla, el préstamo no debe quedar vigente ni devengar intereses.
- **RF-13.5:** Tratamiento de la comisión de apertura:
  - Si la política es deductiva: `Monto Entregado = Monto Capital - Comisión Apertura`.
  - Si es financiada: Se agrega al capital o se programa como cargo en cuota.
- **RF-13.6:** Asiento en el libro mayor (`Transaction`): Genera una transacción inmutable de tipo `DISBURSEMENT` con dirección `OUTFLOW`, vinculada al `disbursement_id` y a la `company_bank_account_id` de origen, cuando el desembolso queda `COMPLETED`.

#### Criterios de Aceptación
- **CA-13.1 (Desembolso Exitoso):**  
  *Dado que* una solicitud de \$ 10,000 está aprobada y el cliente tiene KYC verificado,  
  *Cuando* el operador envía `POST /api/loans` con su `Idempotency-Key`,  
  *Entonces* se crea el préstamo y su desembolso en `PENDING`, se envía la transferencia a la cuenta verificada del cliente y retorna `201 Created`; al confirmarse la transferencia se genera el cronograma inmutable, se asienta la salida en el libro mayor y se notifica al cliente.
- **CA-13.2 (Reintento de Red con Misma Idempotency-Key):**  
  *Dado que* un desembolso fue procesado pero el cliente perdió la conexión antes de recibir el `201`,  
  *Cuando* el frontend reintenta la misma llamada con el mismo `Idempotency-Key`,  
  *Entonces* el sistema retorna la misma respuesta `201` original sin generar un segundo préstamo ni duplicar la salida de dinero.

---

### US-14: Generación y Congelamiento del Cronograma (*Snapshot*)
**Como** Motor Financiero del Sistema,  
**Quiero** calcular el plan de amortización exacto al momento del desembolso y congelarlo como un snapshot inmutable,  
**Para** garantizar que los compromisos de pago queden fijos y no sufran alteraciones no controladas a futuro.

#### Requerimientos Funcionales
- **RF-14.1:** Cálculo de cuotas basado en la fecha exacta de desembolso (`disbursement_date = inicio de intereses`) y la fecha de primera cuota (`first_due_date`).
- **RF-14.2:** Regla de ajuste de días feriados / fines de semana:
  - Si una fecha de vencimiento coincide con día no hábil (según calendario configurado), la fecha de cobro se desplaza al día hábil siguiente (o regla configurada por producto).
- **RF-14.3:** Regla obligatoria de redondeo y cuadre de centavos:
  - Cada cuota calcula sus componentes (`principal`, `interest`, `fee`) redondeados a 2 decimales (*Half-Up*).
  - La sumatoria de las amortizaciones de capital de todas las cuotas debe ser estrictamente idéntica al capital desembolsado:
    $$\sum_{k=1}^{n} \text{principal}_k = \text{Loan.principal}$$
  - En caso de diferencia de centavos producida por redondeo periódico, **el centavo residual se ajusta obligatoriamente en el capital de la última cuota ($n$)**, garantizando que el saldo final sea exactamente \$ 0.00.
- **RF-14.4:** Creación de registros `Installment` asociados a la versión del cronograma (`Schedule.snapshot_version = 1`).
  - Atributos por cuota: `installment_number`, `due_date`, `principal`, `interest`, `fee`, `total_due`, `paid_principal (0.00)`, `paid_interest (0.00)`, `paid_fee (0.00)`, `late_fee (0.00)`, `status = PENDING`.
- **RF-14.5:** Endpoints: `GET /api/loans/{id}/schedule`.

#### Criterios de Aceptación
- **CA-14.1 (Cuadre Perfecto de Centavos):**  
  *Dado que* un préstamo de \$ 1,000 a 3 cuotas mensuales genera cuotas de capital con decimales periódicos,  
  *Cuando* el cronograma se congela,  
  *Entonces* la suma de `principal` de las 3 cuotas suma exactamente \$ 1,000.00, cuadrando el saldo restante a \$ 0.00.
- **CA-14.2 (Inmutabilidad de la Proyección):**  
  *Dado que* un cronograma fue generado y congelado,  
  *Cuando* ocurren pagos posteriores,  
  *Entonces* las columnas proyectadas originales (`principal`, `interest`, `total_due`) nunca se sobreescriben; los pagos se acumulan en los campos de realización (`paid_principal`, `paid_interest`).

---

### US-15: Emisión de Hoja Resumen y Contrato de Crédito
**Como** Operador, Asesor o Cliente (desde el portal),  
**Quiero** generar y descargar en formato PDF el contrato de crédito, pagaré y la hoja resumen con el cronograma oficial,  
**Para** formalizar legalmente la obligación con aceptación electrónica y entregar la copia digital al prestatario (portal y correo).

#### Requerimientos Funcionales
- **RF-15.1:** Generación en el acto del documento contractual en PDF a partir de una plantilla formal con los datos del préstamo recién desembolsado.
- **RF-15.2:** Inclusión obligatoria de:
  - Identificación del prestatario y del prestamista.
  - Monto del crédito, moneda, tasa TEA y TEM aplicada.
  - Tabla completa del cronograma (número de cuota, fecha de vencimiento, desglose de capital, interés, comisiones y cuota total).
  - Términos legales de mora, comisiones por cobranza y cláusulas de aceleración de deuda.
- **RF-15.3:** Endpoint: `GET /api/loans/{id}/documents/contract.pdf`.

#### Criterios de Aceptación
- **CA-15.1 (Descarga de Contrato Consistente):**  
  *Dado que* un préstamo fue desembolsado exitosamente,  
  *Cuando* el usuario solicita el PDF del contrato,  
  *Entonces* el sistema genera el archivo con los mismos valores exactos de cuotas registrados en la base de datos.

---

## 3. Requisitos No Funcionales del Dominio
- **RNF-10 (Consistencia con el Banco):** Una transferencia bancaria no puede formar parte de una transacción de base de datos. Por eso el desembolso se procesa en dos fases:
  1. **Transacción local atómica:** se crean `loans` y `disbursements` (`PENDING`) y el evento de envío en una tabla *outbox*; si algo falla, *Rollback* total y no se envía nada al banco.
  2. **Confirmación asíncrona:** un worker envía la transferencia (`PROCESSING`); el webhook o el extracto la confirma (`COMPLETED`) y, en una segunda transacción atómica, se genera el cronograma y se asienta el `DISBURSEMENT` en el libro mayor. Un `FAILED` no genera asiento ni cronograma.
- **RNF-11 (Regla de Oro Financiera):** El dinero se calcula una sola vez al generar el cronograma y se congela; los pagos solo consumen la deuda, nunca recalculan los valores del pasado hacia atrás.
