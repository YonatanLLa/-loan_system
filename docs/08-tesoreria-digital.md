# Dominio 08: Tesorería Digital y Cuentas Bancarias

## 1. Descripción del Dominio
La Fintech opera **100% en digital**: no existen cajas físicas, ventanillas, bóvedas ni manejo de efectivo. Todo el dinero entra y sale por **cuentas bancarias propias** (`company_bank_accounts`): los desembolsos salen por transferencia (CCI) o billetera digital hacia la cuenta verificada del cliente, y los cobros ingresan por transferencia, Yape/Plin, tarjeta o pasarela de pago.

Este dominio reemplaza al antiguo "Control de Caja y Arqueo". Su objetivo es que **el saldo contable del sistema (libro mayor) cuadre con el saldo real de cada cuenta bancaria**, y que ningún desembolso quede "en tránsito" sin seguimiento.

| Modelo físico (eliminado) | Equivalente digital |
|---|---|
| Caja / ventanilla (`cash_boxes`) | Cuenta bancaria de la Fintech (`company_bank_accounts`) |
| Apertura y cierre de caja | Operación continua 24/7; no hay jornada de caja |
| Arqueo ciego (conteo de billetes) | Cuadre diario: libro mayor vs. extracto bancario (`bank_statements`) |
| Transferencia entre cajas / remesa | Movimiento entre cuentas propias, identificado en el extracto |
| Gastos menores en efectivo | Fuera de alcance (sistema contable / ERP) |

---

## 2. Historias de Usuario

### US-23: Gestión de Cuentas Bancarias de la Fintech
**Como** Administrador o Analista de Tesorería,  
**Quiero** registrar y mantener las cuentas bancarias propias, indicando para qué se usa cada una,  
**Para** que el sistema sepa desde qué cuenta desembolsar y a qué cuenta deben llegar los cobros.

#### Requerimientos Funcionales
- **RF-23.1:** Entidad `company_bank_accounts`: banco, número enmascarado (solo últimos 4 dígitos visibles), CCI, moneda (`currency_id`), y banderas de uso `is_disbursement` (origen de desembolsos) e `is_collection` (destino de cobros).
- **RF-23.2:** Endpoints: `GET /api/treasury/bank-accounts`, `POST /api/treasury/bank-accounts`, `PATCH /api/treasury/bank-accounts/{id}`.
- **RF-23.3:** Una cuenta inactiva (`is_active = false`) no puede usarse para nuevos desembolsos ni publicarse como destino de cobro; los movimientos históricos se conservan.
- **RF-23.4:** Las credenciales de API bancaria o de pasarela asociadas a una cuenta se guardan en el gestor de secretos, nunca en la base de datos.

#### Criterios de Aceptación
- **CA-23.1 (Cuenta de Desembolso Válida):**  
  *Dado que* existe una cuenta BCP en PEN activa con `is_disbursement = true`,  
  *Cuando* un operador inicia el desembolso de un préstamo en PEN,  
  *Entonces* el sistema la propone como cuenta de origen.
- **CA-23.2 (Cuenta Inactiva):**  
  *Dado que* una cuenta fue desactivada,  
  *Cuando* se intenta usar como origen de un desembolso,  
  *Entonces* la API responde `422` ("La cuenta de origen no está habilitada para desembolsos").

---

### US-24: Cuadre Diario por Cuenta Bancaria (equivalente digital del arqueo)
**Como** Analista de Tesorería o Auditor,  
**Quiero** comparar cada día el saldo contable del sistema contra el extracto real de cada cuenta bancaria,  
**Para** detectar de inmediato pagos no recibidos, abonos no identificados o desembolsos sin confirmar.

#### Requerimientos Funcionales
- **RF-24.1:** Proceso diario (job o ejecución manual) por cuenta bancaria y fecha valor:
  $$\text{Saldo Esperado} = \text{Saldo Inicial} + \sum \text{Cobros confirmados} - \sum \text{Desembolsos confirmados} \pm \sum \text{Ajustes}$$
  calculado desde `transactions` filtrando por `company_bank_account_id`, y comparado contra la suma de `bank_statements` de esa cuenta y fecha.
- **RF-24.2:** Resultado del cuadre:
  - `CUADRADO`: diferencia = 0 y todos los movimientos del extracto están conciliados.
  - `DESCUADRADO`: hay diferencia o movimientos sin conciliar. Se listan por tipo: pagos del sistema sin abono en el extracto, abonos del extracto sin pago en el sistema, desembolsos sin cargo bancario y cargos bancarios no identificados (comisiones, ITF, etc.).
- **RF-24.3:** Endpoint: `GET /api/treasury/daily-balance?bankAccountId={id}&date={yyyy-mm-dd}`; exportable a PDF/Excel.
- **RF-24.4:** Los movimientos entre cuentas propias (fondeo de la cuenta de desembolsos) se identifican en el extracto y se concilian como movimiento interno, sin afectar préstamos.

#### Criterios de Aceptación
- **CA-24.1 (Cuadre Exacto):**  
  *Dado que* el sistema registra S/ 12,500 en cobros confirmados y S/ 8,000 en desembolsos para la cuenta BCP del día,  
  *Cuando* el extracto importado contiene exactamente esos movimientos,  
  *Entonces* el cuadre del día queda `CUADRADO`.
- **CA-24.2 (Abono No Identificado):**  
  *Dado que* el extracto muestra un abono de S/ 150 sin pago equivalente en el sistema,  
  *Cuando* se ejecuta el cuadre,  
  *Entonces* el día queda `DESCUADRADO`, el abono aparece en la bandeja de conciliación manual (US-37) y se notifica a tesorería.

---

### US-25: Seguimiento de Desembolsos y Fondos en Tránsito
**Como** Operador o Analista de Tesorería,  
**Quiero** ver en una bandeja el estado de cada transferencia de desembolso,  
**Para** actuar a tiempo cuando un desembolso no se confirma o es rechazado por el banco.

#### Requerimientos Funcionales
- **RF-25.1:** Entidad `disbursements` con ciclo de vida: `PENDING` (generado) → `PROCESSING` (enviado al banco / pasarela) → `COMPLETED` (confirmado por webhook o extracto) | `FAILED` (rechazado: cuenta inválida, límite de billetera, etc.) | `REVERSED`.
- **RF-25.2:** Endpoints: `GET /api/treasury/disbursements?status=PENDING,PROCESSING,FAILED` y `GET /api/disbursements/{id}`.
- **RF-25.3:** Alertas automáticas:
  - Desembolso en `PROCESSING` por más de N horas (configurable) sin confirmación.
  - Desembolso `FAILED`: se muestra `bank_response_code` y `bank_response_message` para que el operador corrija la cuenta destino con el cliente.
- **RF-25.4:** El asiento `DISBURSEMENT` (`OUTFLOW`) en el libro mayor se registra recién cuando el desembolso pasa a `COMPLETED`.
- **RF-25.5:** Validaciones antes de enviar: la cuenta de origen está activa y con `is_disbursement = true`; la cuenta destino pertenece al cliente, está verificada y en la misma moneda; el monto respeta los límites del canal (las billeteras digitales tienen topes por operación y por día).

> Pendiente de modelado: hoy `disbursements.loan_id` es `UNIQUE`, lo que impide registrar un segundo intento tras un `FAILED`. Para reintentos se debe cambiar a un índice único parcial sobre los estados activos.

#### Criterios de Aceptación
- **CA-25.1 (Confirmación por Webhook):**  
  *Dado que* un desembolso de S/ 3,000 está en `PROCESSING`,  
  *Cuando* llega el webhook del banco con el número de operación,  
  *Entonces* el desembolso pasa a `COMPLETED`, se registra `confirmed_at`, se asienta la salida en el libro mayor y se notifica al cliente.
- **CA-25.2 (Rechazo Bancario):**  
  *Dado que* el CCI del cliente es inválido,  
  *Cuando* el banco rechaza la transferencia,  
  *Entonces* el desembolso pasa a `FAILED`, no se genera asiento contable y aparece en la bandeja con el motivo del rechazo.

---

## 3. Requisitos No Funcionales del Dominio
- **RNF-16 (Bloqueo Operativo):** Ningún desembolso se envía si la cuenta de origen está inactiva o no habilitada para desembolsos, o si la cuenta destino del cliente no está verificada. La validación ocurre antes de llamar al banco.
- **RNF-17 (Inmutabilidad del Cuadre):** Un cuadre diario cerrado no se modifica. Las diferencias detectadas después se registran como ajustes en el cuadre del día en que se identifican, con referencia al día original.
