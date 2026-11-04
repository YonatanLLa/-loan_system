# Dominio 08: Control de Caja, Arqueo y Tesorería

## 1. Descripción del Dominio
Este dominio gestiona el control físico y contable del dinero en efectivo y cuentas operativas de la entidad. Supervisa la asignación de cajas físicas a cajeros, aperturas, cierres diarios con arqueo ciego, cuadre de diferencias (sobrantes/faltantes), transferencias entre cajas y registro de gastos menores o egresos operativos.

---

## 2. Historias de Usuario

### US-23: Apertura y Cierre de Caja con Arqueo Ciego
**Como** Cajero u Operador de Ventanilla,  
**Quiero** realizar la apertura matutina de mi caja con su saldo inicial y el cierre formal al final de la jornada mediante conteo de billetes y monedas,  
**Para** garantizar la custodia y cuadre exacto del efectivo en ventanilla.

#### Requerimientos Funcionales
- **RF-23.1:** Entidad `CashBox`: Asignada a una sucursal (`branch_id`), moneda (`currency_id`) y usuario responsable.
  - Estados: `CERRADA`, `ABIERTA`.
- **RF-23.2:** Apertura de caja (`POST /api/cashboxes/{id}/open`):
  - Requiere indicar el monto inicial de efectivo en bóveda/fondo fijo (`opening_balance`).
  - Solo se puede abrir una caja si actualmente está en estado `CERRADA`.
- **RF-23.3:** Cierre de caja y Arqueo Ciego (`POST /api/cashboxes/{id}/close`):
  - El cajero ingresa el detalle físico del dinero contado (cantidad de billetes de cada denominación y monedas).
  - El sistema calcula la suma física ingresada y la compara contra el **saldo teórico esperado del sistema**:
    $$\text{Saldo Esperado} = \text{Saldo Inicial} + \sum \text{Cobros} - \sum \text{Desembolsos} + \sum \text{Ingresos Varios} - \sum \text{Gastos}$$
  - El sistema detecta automáticamente:
    - `CUADRADO`: Diferencia = 0.
    - `SOBRANTE`: Físico > Teórico (diferencia positiva).
    - `FALTANTE`: Físico < Teórico (diferencia negativa).
- **RF-23.4:** Generación del reporte de cierre de caja en PDF con el acta de arqueo firmable por el cajero y supervisor.

#### Criterios de Aceptación
- **CA-23.1 (Apertura Exitosa):**  
  *Dado que* una caja está cerrada y el cajero cuenta con un fondo inicial de \$ 500,  
  *Cuando* envía `POST /api/cashboxes/{id}/open` con saldo \$ 500,  
  *Entonces* la caja cambia a estado `ABIERTA` y queda habilitada para registrar cobros y desembolsos.
- **CA-23.2 (Cierre con Faltante):**  
  *Dado que* el sistema calculó que la caja debe tener \$ 1,200 al final del día y el cajero declara \$ 1,180 en el arqueo,  
  *Cuando* se confirma el cierre,  
  *Entonces* la caja se cierra, se genera un registro de `FALTANTE` por \$ 20.00 y se envía notificación al supervisor.

---

### US-24: Registro de Movimientos de Caja y Gastos Operativos
**Como** Cajero o Administrador,  
**Quiero** registrar ingresos y egresos extraordinarios de efectivo no vinculados a préstamos (ej. gastos de papelería, limpieza o comisiones misceláneas),  
**Para** mantener el saldo de la caja estrictamente cuadrado con todos los flujos reales de dinero.

#### Requerimientos Funcionales
- **RF-24.1:** Entidad `CashMovement`:
  - `cashbox_id` (caja afectada).
  - `type`: `INGRESO_EXTRAORDINARIO` | `EGRESO_GASTO`.
  - `concept`: Descripción del gasto o ingreso (ej. "Compra de útiles de oficina", "Aporte de capital").
  - `amount`: Monto positivo de alta precisión.
  - `voucher_number`: Número de comprobante o factura de sustento.
  - `created_by`: Usuario que ejecuta el movimiento.
- **RF-24.2:** Restricción: No se puede registrar ningún egreso o gasto si el monto excede el saldo en efectivo disponible en la caja.
- **RF-24.3:** Asiento inmediato: Cada movimiento actualiza el saldo actual de la caja en tiempo real dentro de una transacción segura.

#### Criterios de Aceptación
- **CA-24.1 (Gasto Operativo Válido):**  
  *Dado que* la caja tiene \$ 1,000 en efectivo y se efectúa un gasto de fotocopias de \$ 15,  
  *Cuando* el cajero registra el egreso con su comprobante,  
  *Entonces* el saldo disponible de la caja pasa a \$ 985 y el movimiento queda asentado.
- **CA-24.2 (Egreso Superior al Saldo):**  
  *Dado que* una caja tiene \$ 100 de saldo disponible,  
  *Cuando* se intenta registrar un retiro de \$ 150,  
  *Entonces* el sistema rechaza la operación con código `422` ("Saldo insuficiente en caja").

---

### US-25: Transferencias de Efectivo entre Cajas y Bóveda
**Como** Supervisor de Tesorería o Cajero Principal,  
**Quiero** transferir fondos entre diferentes cajas (ej. de Bóveda Central a Ventanilla 1 o remesa a Banco),  
**Para** fondear las ventanillas de atención o resguardar el exceso de efectivo en la bóveda de seguridad.

#### Requerimientos Funcionales
- **RF-25.1:** Endpoint: `POST /api/cashboxes/transfers`.
- **RF-25.2:** Atributos: `source_cashbox_id`, `destination_cashbox_id`, `amount`, `notes`.
- **RF-25.3:** Mecanismo en dos pasos (*Two-Phase Handshake*):
  1. **Envío:** Se descuenta de la caja origen y queda en estado `TRANSFERENCIA_EN_TRANSITO`.
  2. **Recepción/Confirmación:** El cajero receptor debe aceptar la transferencia tras contar el dinero físico para acreditar su caja (`POST /api/cashboxes/transfers/{id}/accept`).
- **RF-25.4:** Validación de moneda: Ambas cajas deben operar con la misma divisa.

#### Criterios de Aceptación
- **CA-25.1 (Flujo Completo de Transferencia):**  
  *Dado que* la Bóveda transfiere \$ 2,000 a Ventanilla 1,  
  *Cuando* el cajero de Ventanilla 1 presiona "Aceptar Transferencia",  
  *Entonces* se acredita el saldo a su caja, se debita formalmente de la bóveda y la transferencia finaliza en estado `COMPLETED`.

---

## 3. Requisitos No Funcionales del Dominio
- **RNF-16 (Bloqueo Operativo):** Toda operación que involucre efectivo (desembolso presencial, cobro en caja) debe verificar en milisegundos que la caja esté `ABIERTA`; de lo contrario, se bloquea la transacción de inmediato.
- **RNF-17 (Inmutabilidad del Arqueo):** Un cierre de caja cerrado con firma de arqueo es inalterable. Cualquier ajuste posterior debe realizarse como movimiento de la siguiente sesión de caja.
