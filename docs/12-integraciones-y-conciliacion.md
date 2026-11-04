# Dominio 12: Integraciones Bancarias, Webhooks y Conciliación

## 1. Descripción del Dominio
Este dominio capacita al sistema para operar en un ecosistema bancario y fintech moderno. Gestiona la recepción de notificaciones de pago en tiempo real vía Webhooks (bancos locales como BCP, BBVA, Scotiabank y billeteras digitales como Yape / Plin), la verificación criptográfica de firmas, el procesamiento resiliente con reintentos y la conciliación contable entre los extractos bancarios externos y las transacciones internas del sistema.

---

## 2. Historias de Usuario

### US-36: Recepción y Procesamiento Seguro de Webhooks Bancarios
**Como** Sistema de Pagos Automatizado,  
**Quiero** recibir y procesar notificaciones HTTP (Webhooks) de pasarelas y bancos aliados con validación de firma y control de duplicados,  
**Para** asentar los pagos de los clientes de manera automática e inmediata las 24 horas del día.

#### Requerimientos Funcionales
- **RF-36.1:** Endpoint receptor de webhooks: `POST /api/integrations/webhooks/{provider}` (ej. `bcp`, `bbva`, `yape`, `plin`, `niubiz`).
- **RF-36.2:** Verificación criptográfica de firma:
  - Cada proveedor debe enviar su firma en una cabecera HTTP (ej. `X-Signature` o `X-Hub-Signature-256`).
  - El sistema valida el HMAC-SHA256 del cuerpo de la petición contra el secreto compartido del proveedor.
  - Si la firma no coincide $\to$ Se rechaza de inmediato con `401 Unauthorized` y se emite alerta de seguridad.
- **RF-36.3:** Persistencia del Evento (`IntegrationEvent`):
  - Atributos: `id`, `provider`, `event_type`, `external_id` (código de operación del banco), `payload` (JSON crudo), `status` (`RECEIVED`, `PROCESSED`, `FAILED`), `processed_at`.
- **RF-36.4:** Control estricto de idempotencia por `external_id`:
  - Si el banco reenvía un webhook con un `external_id` que ya fue procesado exitosamente $\to$ La API responde de inmediato `200 OK` (acuse de recibo) sin volver a aplicar el dinero ni duplicar el pago.
- **RF-36.5:** Enrutamiento a la cascada de cobros: Una vez validado, se ejecuta la lógica de aplicación de pagos (`US-16`) imputando a las cuotas del cliente identificado por su código o referencia de pago.

#### Criterios de Aceptación
- **CA-36.1 (Webhook Válido y Cobro Asentado):**  
  *Dado que* llega un webhook de BCP con firma válida notificando un pago de \$ 120 con referencia `OP-998877`,  
  *Cuando* el sistema procesa la notificación,  
  *Entonces* se guarda el `IntegrationEvent`, se aplica el abono al préstamo del cliente y responde `200 OK` en menos de 500 ms.
- **CA-36.2 (Webhook Duplicado):**  
  *Dado que* el banco reintenta el envío de la notificación de `OP-998877`,  
  *Cuando* el sistema detecta que el `external_id` ya está en estado `PROCESSED`,  
  *Entonces* responde `200 OK` de inmediato sin generar una segunda cuota pagada.

---

### US-37: Importación de Extractos Bancarios y Conciliación
**Como** Analista de Tesorería o Auditor Financiero,  
**Quiero** cargar archivos de extractos bancarios (Excel, CSV o formato TXT estándar bancario) y ejecutar la conciliación automática contra los cobros del sistema,  
**Para** verificar que cada centavo registrado en el software ingresó efectivamente en las cuentas bancarias de la empresa.

#### Requerimientos Funcionales
- **RF-37.1:** Endpoints: `POST /api/reconciliation/import` y `GET /api/reconciliation`.
- **RF-37.2:** Carga de extracto de cuenta bancaria (`BankAccount`):
  - Importación de transacciones bancarias: Fecha valor, número de operación bancaria, descripción/glosa, monto, tipo (abono/cargo).
- **RF-37.3:** Motor de conciliación automática (Reglas de Matching):
  1. Coincidencia exacta de `external_reference` (código de operación) + `monto` + `fecha`.
  2. Coincidencia por DNI/Código de cliente en la glosa + `monto exacto`.
- **RF-37.4:** Estados de conciliación:
  - `CONCILIADO_AUTOMATICO`: Coincidencia 100% confirmada.
  - `PENDIENTE_CONCILIACION`: Movimientos bancarios sin pago equivalente en el sistema (ej. abono no identificado).
  - `DESCUADRADO`: Misma referencia pero con montos distintos.
- **RF-37.5:** Herramienta de conciliación manual: Permite al tesorero asociar manualmente un abono bancario no identificado con un préstamo impago de un cliente.

#### Criterios de Aceptación
- **CA-37.1 (Conciliación Automática Exitosa):**  
  *Dado que* se importa un extracto con 200 operaciones y 195 coinciden con pagos del sistema,  
  *Cuando* se procesa la conciliación,  
  *Entonces* los 195 pagos se marcan como `RECONCILED` y los 5 restantes quedan en bandeja de revisión manual.

---

### US-38: Cola de Reprocesamiento y Manejo de Fallos (*Dead-Letter Queue*)
**Como** Ingeniero de Confiabilidad del Sistema (SRE) o Administrador,  
**Quiero** que los eventos de integración que fallen por caídas de red o errores temporales sean encolados y reintentados con retroceso exponencial,  
**Para** garantizar cero pérdidas de pagos en momentos de degradación de servicios bancarios.

#### Requerimientos Funcionales
- **RF-38.1:** Mecanismo de reintentos exponenciales (*Exponential Backoff*):
  - 1º reintento: a los 30 segundos.
  - 2º reintento: a los 2 minutos.
  - 3º reintento: a los 10 minutos.
  - 4º reintento: a los 30 minutos.
- **RF-38.2:** Desvío a Cola de Errores (*Dead-Letter Queue* - DLQ):
  - Si un evento falla tras agotar los 4 reintentos, su estado pasa a `DEAD_LETTER` o `FAILED_NEEDS_REVIEW`.
  - Se guarda el stack trace del error técnico y el payload original íntegro.
- **RF-38.3:** Panel operativo de reprocesamiento:
  - Interfaz administrativa que lista las transacciones en DLQ.
  - Botón de reintento manual asistido (*Retry Now*) una vez corregida la causa del fallo.

#### Criterios de Aceptación
- **CA-38.1 (Reintento Exitoso tras Caída de Base de Datos):**  
  *Dado que* un webhook falló porque la base de datos estaba saturada temporalmente,  
  *Cuando* la cola asíncrona ejecuta el segundo reintento a los 2 minutos,  
  *Entonces* procesa el cobro exitosamente y actualiza el evento a `PROCESSED`.
- **CA-38.2 (Derivación a DLQ por Error de Negocio Inesperado):**  
  *Dado que* un evento falló por un error de formato insalvable tras 4 intentos,  
  *Cuando* se agotan los reintentos,  
  *Entonces* se traslada a la bandeja de DLQ y notifica al equipo de soporte técnico.

---

## 3. Requisitos No Funcionales del Dominio
- **RNF-24 (Tiempo de Respuesta en Webhooks):** El endpoint de webhook debe recibir, verificar la firma y encolar el mensaje respondiendo `200 OK` al banco en menos de 300 ms, ejecutando el procesamiento pesado en segundo plano (asíncrono con Redis/RabbitMQ).
- **RNF-25 (Seguridad de Enlace):** Toda comunicación con bancos o billeteras debe estar forzada bajo TLS 1.3 con validación de listas de IPs permitidas (*Whitelisting*).
