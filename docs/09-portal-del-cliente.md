# Dominio 09: Portal de Autoservicio del Cliente

## 1. Descripción del Dominio
Este dominio define la experiencia digital del prestatario (*Borrower*). Consiste en un portal web responsive y seguro, totalmente desacoplado del Backoffice administrativo pero conectado a la misma API de negocio. Permite al cliente registrarse, validar su identidad (KYC), solicitar préstamos, simular cuotas, firmar digitalmente contratos, consultar su cronograma y realizar pagos en línea con descarga de constancias.

---

## 2. Historias de Usuario

### US-26: Registro, Acceso y Verificación Digital del Cliente (*Onboarding*)
**Como** nuevo cliente o usuario recurrente,  
**Quiero** crear una cuenta con mi correo o teléfono y verificar mi identidad mediante código OTP y fotos de mi documento,  
**Para** acceder a mi panel personal de préstamos de manera segura y autónoma.

#### Requerimientos Funcionales
- **RF-26.1:** Registro de usuario cliente: `POST /api/client/register`.
  - Campos: Tipo y número de documento, nombres, apellidos, correo, celular, contraseña segura.
- **RF-26.2:** Verificación en dos pasos (OTP): Envío de código de 6 dígitos por SMS o WhatsApp para validar el número telefónico y correo.
- **RF-26.3:** Inicio de sesión del cliente: `POST /api/client/login` que genera un token JWT con rol exclusivo `ROLE_CLIENT` y scope acotado solo a sus propios recursos.
- **RF-26.4:** Carga de documentos para KYC desde el celular (cámara o galería): Foto de DNI frontal y reverso, más selfie para prueba de vida.
- **RF-26.5:** Seguridad de aislamiento: La API debe validar que un cliente autenticado **únicamente pueda leer y operar sobre registros cuyo `customer_id` coincida con su identidad**.

#### Criterios de Aceptación
- **CA-26.1 (Onboarding Completo):**  
  *Dado que* un usuario completa el formulario y valida su código OTP recibido en su móvil,  
  *Cuando* sube las fotos de su documento,  
  *Entonces* se crea su cuenta, su estado KYC queda `PENDING` para revisión y accede a su panel de autoservicio.
- **CA-26.2 (Aislamiento de Datos):**  
  *Dado que* el Cliente A intenta consultar `GET /api/client/loans/{id}` perteneciente al Cliente B,  
  *Cuando* la API evalúa los permisos,  
  *Entonces* responde `403 Forbidden` o `404 Not Found`, protegiendo la privacidad del tercero.

---

### US-27: Simulación y Solicitud de Préstamo Online
**Como** Cliente verificado,  
**Quiero** utilizar un simulador interactivo para elegir monto y plazo, y enviar mi solicitud de préstamo adjuntando mis sustentos de ingresos,  
**Para** obtener financiamiento sin necesidad de trasladarme físicamente a una oficina.

#### Requerimientos Funcionales
- **RF-27.1:** Simulador web dinámico que recalcula en tiempo real el valor estimado de las cuotas, intereses y comisiones a medida que el cliente desliza el monto y plazo.
- **RF-27.2:** Envío de solicitud: `POST /api/client/loan-applications`.
  - Envío de datos laborales: Empresa, ocupación, ingreso mensual y adjuntos de comprobantes de ingreso (boleta o recibo de honorarios).
- **RF-27.3:** Pantalla de seguimiento (*Tracking*):
  - Línea de tiempo visual con el estado de la solicitud: `SOLICITADO` $\to$ `EN_EVALUACION` $\to$ `APROBADO` $\to$ `DESEMBOLSADO`.
- **RF-27.4:** Aceptación digital de términos: Si la solicitud es aprobada, el cliente debe revisar las condiciones finales y aceptar el contrato mediante firma digital o código OTP de conformidad antes del desembolso.

#### Criterios de Aceptación
- **CA-27.1 (Solicitud Exitosa):**  
  *Dado que* el cliente selecciona \$ 3,000 a 6 meses y adjunta su boleta de pago,  
  *Cuando* confirma la solicitud,  
  *Entonces* la solicitud queda registrada en el sistema, aparece en la bandeja del analista y el cliente ve su estado en `EN_EVALUACION`.

---

### US-28: Consulta de Cronograma y Estado de Cuenta Personal
**Como** Cliente con un préstamo activo,  
**Quiero** visualizar mi calendario de pagos con las cuotas pagadas, la próxima cuota a vencer y el saldo de capital restante,  
**Para** planificar mis finanzas y mantener mis pagos puntuales.

#### Requerimientos Funcionales
- **RF-28.1:** Endpoint: `GET /api/client/loans` (lista préstamos activos e históricos) y `GET /api/client/loans/{id}/schedule` (cronograma detallado).
- **RF-28.2:** Tarjetas visuales de resumen:
  - Próximo vencimiento (fecha límite y monto exacto de la cuota).
  - Días restantes para el pago o días de atraso en caso de mora.
  - Saldo insoluto de capital y desglose de mora acumulada (si existiera).
- **RF-28.3:** Tabla interactiva de cuotas con indicadores claros por color:
  - Verde: Pagada (`PAID`).
  - Amarillo: Próxima a vencer (`PENDING`).
  - Rojo: Vencida en mora (`OVERDUE`).
  - Azul: Pago parcial (`PARTIAL`).

#### Criterios de Aceptación
- **CA-28.1 (Visualización Clara de Cuota en Mora):**  
  *Dado que* el cliente tiene una cuota con 4 días de atraso,  
  *Cuando* ingresa a su portal,  
  *Entonces* observa una alerta destacada en rojo indicando el monto de la cuota más el recargo exacto por mora devengado hasta el día de hoy.

---

### US-29: Pago de Cuotas en Línea y Descarga de Constancias
**Como** Cliente con cuotas pendientes,  
**Quiero** pagar mi cuota utilizando medios de pago electrónicos (tarjeta de débito, Yape, Plin o transferencia interbancaria) y descargar mi constancia de pago,  
**Para** liquidar mis compromisos al instante y contar con un comprobante oficial de cancelación.

#### Requerimientos Funcionales
- **RF-29.1:** Endpoint de iniciación de pago: `POST /api/client/payments`.
- **RF-29.2:** Opciones de canal:
  - Generación de código QR dinámico (Yape / Plin) con monto exacto precargado.
  - Pasarela de pagos con tarjeta de débito/crédito.
  - Registro de reporte de transferencia con carga de foto de voucher bancario para validación.
- **RF-29.3:** Actualización instantánea: Una vez confirmado el pago por la pasarela o webhook bancario, el portal actualiza el estado de la cuota a `PAID` en tiempo real.
- **RF-29.4:** Descarga de constancia: Generación en PDF del recibo oficial de pago con código de verificación QR (`GET /api/client/payments/{id}/receipt.pdf`).

#### Criterios de Aceptación
- **CA-29.1 (Pago Inmediato con QR):**  
  *Dado que* el cliente escanea y paga el QR de su cuota de \$ 150,  
  *Cuando* el webhook bancario notifica la acreditación,  
  *Entonces* el portal muestra confirmación de éxito y habilita el botón "Descargar Recibo en PDF".

---

## 3. Requisitos No Funcionales del Dominio
- **RNF-18 (Diseño Mobile-First):** El portal cliente debe estar 100% optimizado para visualización y uso fluido en teléfonos inteligentes (pantallas pequeñas, carga ligera).
- **RNF-19 (Seguridad de Sesiones en Clientes):** Tiempo de inactividad máximo de 15 minutos en el portal cliente para evitar accesos indebidos en dispositivos compartidos.
