# Dominio 04: Originación, Simulación y Evaluación Crediticia

## 1. Descripción del Dominio
Este dominio gestiona el ciclo inicial del crédito: la simulación previa del financiamiento sin impacto en base de datos, la creación formal de la solicitud (*Loan Application*), la verificación de reglas de admisión (capacidad de endeudamiento) y el dictamen colegiado o manual por parte del analista de riesgos.

---

## 2. Historias de Usuario

### US-10: Registro de Solicitud de Préstamo
**Como** Analista de Crédito o Cajero (en Backoffice) o Cliente (vía Portal),  
**Quiero** ingresar una solicitud de crédito especificando cliente, producto, monto solicitado y plazo deseado,  
**Para** iniciar formalmente el proceso de evaluación y scoring crediticio.

#### Requerimientos Funcionales
- **RF-10.1:** Creación de solicitud (`LoanApplication`) con datos mínimos:
  - `customer_id` (referencia al prestatario registrado).
  - `product_id` (producto seleccionado).
  - `amount` (monto solicitado en la moneda del producto).
  - `term` (número de cuotas).
  - `frequency` (diaria, semanal, quincenal, mensual).
  - `destination` (destino del préstamo: capital de trabajo, consumo, vehículo, etc.).
- **RF-10.2:** Validaciones automáticas de admisión inicial:
  - El monto debe encontrarse dentro del rango `[min_amount, max_amount]` del producto.
  - El plazo debe encontrarse dentro de `[min_term, max_term]`.
  - La edad del cliente debe cumplir con el rango `[min_age, max_age]`.
  - Si no cumple los rangos, la API rechaza la solicitud de inmediato con error descriptivo.
- **RF-10.3:** Estados de la solicitud: `SOLICITADO`, `EN_EVALUACION`, `APROBADO`, `APROBADO_CON_CONDICIONES`, `RECHAZADO`, `ANULADO`.
- **RF-10.4:** Endpoint: `POST /api/loan-applications`.

#### Criterios de Aceptación
- **CA-10.1 (Solicitud Válida):**  
  *Dado que* se ingresa un monto de \$ 5,000 para un producto que admite de \$ 1,000 a \$ 20,000 y un plazo admisible,  
  *Cuando* se registra la solicitud,  
  *Entonces* se guarda con estado inicial `SOLICITADO` y genera un identificador único.
- **CA-10.2 (Monto Fuera de Rango):**  
  *Dado que* se solicita un monto de \$ 25,000 para un producto cuyo máximo es \$ 20,000,  
  *Cuando* se envía la solicitud,  
  *Entonces* el sistema retorna `400 Bad Request` indicando que excede el límite máximo.

---

### US-11: Simulador de Cuotas y Cronograma de Proyección
**Como** Analista de Crédito o Cliente interesado,  
**Quiero** simular un crédito con diferentes montos, plazos y frecuencias,  
**Para** proyectar el valor estimado de las cuotas y el cronograma tentativo sin persistir datos basura en la base transaccional.

#### Requerimientos Funcionales
- **RF-11.1:** Endpoint de cálculo dinámico en memoria: `POST /api/loan-applications/simulate` o `POST /api/loan-applications/{id}/simulate`.
- **RF-11.2:** Parámetros de entrada: `product_id`, `amount`, `term`, `first_due_date` (opcional).
- **RF-11.3:** Salida de la simulación:
  - Resumen financiero: Tasa TEA/TEM aplicada, Total Capital, Total Intereses a pagar, Total Comisiones/Seguros, Costo Total del Crédito.
  - Arreglo de cuotas proyectadas: `nro_cuota`, `fecha_vencimiento`, `capital`, `interes`, `comisiones`, `total_cuota`, `saldo_capital`.
- **RF-11.4:** El endpoint debe ser de lectura/cálculo puro; no debe crear ni alterar registros en la base de datos.

#### Criterios de Aceptación
- **CA-11.1 (Simulación Instantánea):**  
  *Dado que* se envían los datos de simulación válidos,  
  *Cuando* se ejecuta el endpoint,  
  *Entonces* retorna `200 OK` en menos de 50 ms con el desglose exacto de cada cuota y la suma de capital idéntica al monto ingresado.

---

### US-12: Motor de Evaluación Crediticia y Dictamen
**Como** Analista de Crédito o Jefe de Riesgos,  
**Quiero** que el sistema calcule automáticamente los indicadores de capacidad de pago y me permita dictaminar la solicitud con su justificación,  
**Para** asegurar que solo se aprueben préstamos a clientes con capacidad real de pago y mantener una cartera sana.

#### Requerimientos Funcionales
- **RF-12.1:** Cálculo automático de la **Capacidad de Pago / Ratio Cuota-Ingreso (DTI - *Debt-to-Income*)**:
  $$\text{DTI} = \frac{\text{Cuota Proyectada} + \text{Otras Cuotas Activas}}{\text{Ingreso Neto Mensual Declarado}} \times 100$$
- **RF-12.2:** Reglas automáticas del motor:
  - Si $\text{DTI} \le 35\%$: Recomendación automática `APROBABLE`.
  - Si $35\% < \text{DTI} \le 50\%$: Recomendación `REVISION_MANUAL` (requiere aval o reducción de monto).
  - Si $\text{DTI} > 50\%$ o el cliente presenta mora activa en otros créditos: Recomendación `RECHAZO_SUGERIDO`.
- **RF-12.3:** Dictamen formal: Endpoint `POST /api/loan-applications/{id}/decision`.
  - Acciones: `APROBADO`, `APROBADO_CON_CONDICIONES` (ej. monto aprobado menor al solicitado), `RECHAZADO`.
  - Campos obligatorios: `decision`, `notes` (justificación obligatoria), `approved_amount` (si difiere).
- **RF-12.4:** Auditoría estricta: Toda decisión debe registrar `user_id` del analista, `timestamp` exacto y justificación para fines de auditoría.

#### Criterios de Aceptación
- **CA-12.1 (Aprobación con Reducción de Monto):**  
  *Dado que* una solicitud pedía \$ 10,000 pero el DTI da 45%,  
  *Cuando* el analista aprueba con condiciones por \$ 7,000,  
  *Entonces* el estado pasa a `APROBADO_CON_CONDICIONES`, se fija `approved_amount: 7000` y queda lista para desembolso.
- **CA-12.2 (Rechazo sin Justificación):**  
  *Dado que* un analista intenta dictaminar `RECHAZADO` sin enviar el campo `notes`,  
  *Cuando* envía la petición,  
  *Entonces* el sistema rechaza la operación con `422 Unprocessable Entity` exigiendo el motivo del rechazo.

---

## 3. Requisitos No Funcionales del Dominio
- **RNF-08 (Idempotencia en Decisiones):** El endpoint de dictamen no debe permitir cambiar el estado de una solicitud que ya fue `APROBADO` o `RECHAZADO` salvo reapertura explícita autorizada por un administrador.
- **RNF-09 (Trazabilidad Integral):** Cada cálculo del motor de scoring y ratio DTI debe quedar congelado en la solicitud para sustentar la auditoría posterior.
