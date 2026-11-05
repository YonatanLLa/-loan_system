# Dominio 07: Mora, Aging y Gestión de Cobranzas

## 1. Descripción del Dominio
Este dominio gestiona el seguimiento y recuperación de la cartera vencida. Incluye el motor automatizado que corre diariamente en segundo plano para clasificar créditos en mora, liquidar penalidades conforme a las reglas financieras pactadas, agrupar las deudas por tramos de vencimiento (*Aging*) y registrar los acuerdos o gestiones de cobranza.

---

## 2. Historias de Usuario

### US-19: Job Diario de Cálculo de Mora y Transición de Estados
**Como** Sistema Financiero Autónomo,  
**Quiero** ejecutar un proceso en lote (*Background Job*) todas las noches al cierre del día,  
**Para** evaluar las cuotas impagas, actualizar los días de atraso, calcular la mora devengada y actualizar el estado general de los préstamos a `EN_MORA`.

#### Requerimientos Funcionales
- **RF-19.1:** El job debe ejecutarse de forma programada (cron diario, por ejemplo a las 00:05 AM hora local).
- **RF-19.2:** Identificación de cuotas vencidas:
  $$\text{Días de Atraso} = \text{Fecha Actual} - \text{Fecha Vencimiento} (\text{due\_date})$$
  Se evalúan todas las cuotas donde $\text{Fecha Actual} > \text{due\_date}$ y `status != PAID`.
- **RF-19.3:** Evaluación de Días de Gracia:
  - Si $\text{Días de Atraso} \le \text{días\_gracia\_del\_producto}$: La cuota se marca como `OVERDUE` pero no liquida mora monetaria.
  - Si $\text{Días de Atraso} > \text{días\_gracia\_del\_producto}$: Se computa la penalidad de mora aplicando la fórmula configurada por producto:
    $$\text{late\_fee} = \text{Base Vencida} \times \text{tasa\_mora\_diaria} \times \text{Días de Atraso}$$
- **RF-19.4:** Transición automática de estados del préstamo:
  - Si un préstamo en estado `VIGENTE` tiene al menos una cuota en mora $\to$ Su estado cambia a `EN_MORA`.
  - Si un préstamo en `EN_MORA` recibe pagos que liquidan todas las cuotas vencidas $\to$ Vuelve a `VIGENTE`.
- **RF-19.5:** Idempotencia del Job: El proceso debe ser reejecutable sin duplicar la mora del mismo día.

#### Criterios de Aceptación
- **CA-19.1 (Transición a Mora y Liquidación):**  
  *Dado que* una cuota de \$ 100 venció ayer y el producto tiene 0 días de gracia y tasa diaria de mora de 0.1%,  
  *Cuando* el job nocturno procesa la cartera,  
  *Entonces* la cuota pasa a `days_late = 1`, `late_fee = 0.10`, y el préstamo cambia a estado `EN_MORA`.
- **CA-19.2 (Respeto de Gracia):**  
  *Dado que* un producto tiene 3 días de gracia y la cuota tiene 2 días de vencida,  
  *Cuando* corre el job,  
  *Entonces* `late_fee` permanece en \$ 0.00 hasta que supere el tercer día.

---

### US-20: Bandeja de Cobranzas por Tramos de *Aging*
**Como** Gestor de Cobranza o Supervisor de Cartera,  
**Quiero** visualizar una bandeja operativa con los préstamos clasificados por tramos de días de atraso,  
**Para** priorizar llamadas, mensajes digitales o derivación prejudicial de acuerdo con la criticidad de la deuda (cobranza 100% remota, sin visitas de campo).

#### Requerimientos Funcionales
- **RF-20.1:** Clasificación estándar de tramos de *Aging*:
  - **Preventivo:** 1 a 5 días antes del vencimiento.
  - **Tramo 1 (Mora Temprana):** 1 a 30 días de atraso.
  - **Tramo 2 (Mora Media):** 31 a 60 días de atraso.
  - **Tramo 3 (Mora Tardía):** 61 a 90 días de atraso.
  - **Tramo 4 (Cobranza Judicial / Pre-castigo):** Más de 90 días de atraso.
- **RF-20.2:** Endpoint: `GET /api/collections/aging` con filtros por tramo, gestor asignado, monto y producto.
- **RF-20.3:** Datos visibles por crédito: Nombre del cliente, teléfono verificado, días de mora, cuotas vencidas, saldo de capital impago, total exigible (capital + interés + mora).

#### Criterios de Aceptación
- **CA-20.1 (Filtro por Tramo 2):**  
  *Dado que* existen 15 clientes con atraso de entre 31 y 60 días,  
  *Cuando* el cobrador filtra por `tramo = 31-60`,  
  *Entonces* el sistema muestra únicamente esos 15 registros ordenados de mayor a menor monto adeudado.

---

### US-21: Registro de Acciones y Promesas de Pago
**Como** Gestor de Cobranza,  
**Quiero** registrar el resultado de cada contacto con el cliente (llamada, WhatsApp, SMS, correo o compromiso de pago),  
**Para** documentar la trazabilidad de la gestión y programar recordatorios de seguimiento.

#### Requerimientos Funcionales
- **RF-21.1:** Endpoint: `POST /api/collections/actions`.
- **RF-21.2:** Estructura de la acción (`CollectionAction`):
  - `loan_id` (préstamo intervenido).
  - `action_type`: `LLAMADA_TELEFONICA`, `WHATSAPP`, `EMAIL`, `SMS`, `NOTIFICACION_PUSH`, `PROMESA_DE_PAGO`, `ACUERDO_DE_PAGO_DIGITAL`.
  - `note` (detalle del contacto y argumentos del cliente).
  - `next_action_at` (fecha/hora de próxima acción o cumplimiento de promesa).
  - `promise_amount` y `promise_date` (si aplica promesa de pago).
- **RF-21.3:** Alerta de promesa vencida: Si llega la fecha fijada y no se detecta el pago en el sistema, se resalta en la bandeja del gestor como `PROMESA_INCUMPLIDA`.

#### Criterios de Aceptación
- **CA-21.1 (Registro de Promesa de Pago):**  
  *Dado que* un cliente moroso promete pagar \$ 200 el viernes próximo,  
  *Cuando* el cobrador registra la acción con tipo `PROMESA_DE_PAGO`,  
  *Entonces* se guarda el registro en la bitácora del préstamo y se calendariza la próxima alerta.

---

### US-22: Castigo Contable de Deuda (*Write-Off*)
**Como** Administrador o Comité de Riesgos,  
**Quiero** registrar el castigo de un crédito calificado como irrecuperable (típicamente mora > 180 días),  
**Para** depurar la cartera contable activa sin condonar legalmente la deuda ni borrar el historial crediticio del deudor.

#### Requerimientos Funcionales
- **RF-22.1:** Endpoint de castigo: `POST /api/loans/{id}/write-off`.
- **RF-22.2:** Condiciones:
  - Préstamo en estado `EN_MORA` con atraso significativo.
  - Aprobación explícita con acta de comité y justificación obligatoria.
- **RF-22.3:** Efectos del castigo:
  - Estado del préstamo pasa a `CASTIGADO`.
  - Deja de devengar intereses corrientes en los reportes operativos de ingresos.
  - **No se elimina el registro**: la deuda permanece registrada para que, en caso de cobranza judicial posterior o recuperación extraordinaria, pueda asentarse el pago.

#### Criterios de Aceptación
- **CA-22.1 (Pase a Castigado):**  
  *Dado que* un préstamo de \$ 3,000 tiene 200 días de atraso sin respuesta,  
  *Cuando* el comité autoriza el castigo contable,  
  *Entonces* el estado cambia a `CASTIGADO` y se genera el asiento de provisión/castigo correspondiente.

---

## 3. Requisitos No Funcionales del Dominio
- **RNF-14 (Eficiencia en Jobs Masivos):** El cálculo diario de mora debe procesarse mediante procesamiento por lotes (*Batch Chunking* de 500 registros) para soportar carteras de más de 100,000 préstamos en menos de 10 minutos.
- **RNF-15 (Notificaciones Omnicanal Desacopladas):** Las alertas y recordatorios preventivos de vencimiento deben encolarse de forma asíncrona (RabbitMQ/Redis) hacia proveedores de SMS, WhatsApp o Correo.
