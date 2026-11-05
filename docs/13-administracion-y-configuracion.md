# Dominio 13: Administración General, Canales Digitales y Parámetros Globales

## 1. Descripción del Dominio
Este dominio centraliza la configuración corporativa y operativa del Backoffice. La operación es 100% digital y centralizada: no hay sucursales, agencias ni zonas físicas. Permite al Administrador gestionar los datos fiscales de la empresa, los canales digitales de pago y desembolso, el calendario oficial de feriados (crucial para el cálculo de cronogramas sin vencimientos en días inhábiles), la parametrización de monedas, métodos de pago admitidos y las plantillas legales de los contratos.

---

## 2. Historias de Usuario

### US-39: Configuración Institucional de la Empresa
**Como** Administrador del Sistema,  
**Quiero** registrar y actualizar la información corporativa, fiscal y de contacto de la entidad financiera,  
**Para** que se refleje automáticamente en los contratos legales, pagarés, recibos de pago y notificaciones oficiales a los clientes.

#### Requerimientos Funcionales
- **RF-39.1:** Atributos de configuración institucional:
  - Razón social y nombre comercial.
  - Identificación fiscal (RUC / NIT / RFC / CIF).
  - Dirección fiscal, teléfono de soporte y correo institucional.
  - Representante legal y número de documento de identidad.
  - Carga de logotipo oficial (alta resolución para contratos y recibos PDF).
  - Moneda principal o funcional por defecto del sistema.
- **RF-39.2:** Endpoints: `GET /api/admin/company` y `PUT /api/admin/company`.
- **RF-39.3:** Acceso exclusivo: Solo usuarios con rol `ROLE_ADMIN` y permiso `company.manage`.
- **RF-39.4:** Registro de cambios en `AuditLog`.

#### Criterios de Aceptación
- **CA-39.1 (Actualización Exitosa):**  
  *Dado que* el administrador actualiza el teléfono de atención y el logo corporativo,  
  *Cuando* guarda los cambios,  
  *Entonces* los nuevos contratos y recibos generados a partir de ese momento incorporan el nuevo logotipo y datos actualizados.

---

### US-40: Configuración de Canales Digitales de Pago y Desembolso
**Como** Administrador de Operaciones,  
**Quiero** habilitar y parametrizar los canales digitales por los que el cliente paga y recibe su préstamo (bancos, billeteras y pasarelas),  
**Para** operar de forma 100% digital y centralizada, sin sucursales, cajas ni zonas físicas.

#### Requerimientos Funcionales
- **RF-40.1:** Canales de cobro (`payment_methods`): nombre, tipo (`TRANSFERENCIA_BANCARIA`, `YAPE`, `PLIN`, `TARJETA_DEBITO`, `TARJETA_CREDITO`, `PASARELA_PAGO`), código del proveedor (`provider_code`), indicador de confirmación automática por webhook (`webhook_enabled`) y estado activo/inactivo.
- **RF-40.2:** Canales de desembolso (`disbursement_channel`): `TRANSFERENCIA_BANCARIA`, `DEPOSITO_CCI`, `YAPE`, `PLIN`, cada uno vinculado a una cuenta bancaria de la Fintech habilitada para desembolsos (US-23).
- **RF-40.3:** Un canal sin webhook (`webhook_enabled = false`, ej. transferencia por CCI) solo confirma pagos mediante conciliación con el extracto bancario (US-37); nunca por la sola declaración o voucher del cliente.
- **RF-40.4:** Las credenciales técnicas de cada proveedor (API keys, secretos de firma HMAC) se gestionan en el gestor de secretos y se rotan sin intervención en base de datos.
- **RF-40.5:** Endpoints: `GET /api/admin/payment-methods`, `PUT /api/admin/payment-methods/{id}`.

#### Criterios de Aceptación
- **CA-40.1 (Desactivación de Canal):**  
  *Dado que* la pasarela de tarjetas presenta fallas,  
  *Cuando* el administrador desactiva el método `PASARELA_PAGO`,  
  *Entonces* el portal del cliente deja de ofrecerlo de inmediato y los demás canales siguen operando.

---

### US-41: Calendario de Feriados y Días No Hábiles
**Como** Administrador o Especialista de Operaciones,  
**Quiero** registrar el calendario oficial de feriados nacionales del año,  
**Para** que el motor de cronogramas desplace automáticamente los vencimientos que coincidan con días no laborables.

#### Requerimientos Funcionales
- **RF-41.1:** Entidad `Holiday`:
  - `date`: Fecha específica (año-mes-día).
  - `name`: Motivo (ej. "Año Nuevo", "Fiestas Patrias").
  - Alcance único nacional: al no existir sucursales no hay feriados locales por agencia (una fecha = un registro).
  - `is_recurring`: Indica si se repite todos los años en la misma fecha.
- **RF-41.2:** Regla de negocio en la generación de cronogramas (`US-14`):
  - El motor verifica si la fecha calculada de vencimiento de una cuota cae en fin de semana (sábado/domingo) o en una fecha registrada en `Holiday`.
  - Si es día no hábil, **la fecha de vencimiento se desplaza al día hábil inmediatamente posterior** (o anterior, según la política configurada por el producto).
  - El cálculo de días para devengo de intereses se ajusta a la fecha efectiva desplazada.
- **RF-41.3:** Endpoints: `POST /api/admin/holidays`, `GET /api/admin/holidays`, `DELETE /api/admin/holidays/{id}`.

#### Criterios de Aceptación
- **CA-41.1 (Desplazamiento Automático por Feriado):**  
  *Dado que* el 1 de mayo está registrado como feriado en el calendario y una cuota mensual debía vencer ese día,  
  *Cuando* el motor genera el cronograma de un préstamo,  
  *Entonces* fija la fecha de vencimiento de esa cuota para el día 2 de mayo (o lunes hábil si cae fin de semana).

---

### US-42: Gestión de Catálogos Maestros y Plantillas Legales
**Como** Administrador del Sistema,  
**Quiero** configurar los catálogos auxiliares (tipos de documentos, medios de pago admitidos, monedas) y las cláusulas legales de contratos,  
**Para** mantener el sistema adaptado a regulaciones y cambios comerciales sin necesidad de modificar el código fuente.

#### Requerimientos Funcionales
- **RF-42.1:** Gestión de Tipos de Documentos de Identidad:
  - Definición de código (`DNI`, `CE`, `RUC`, `PAS`), nombre, longitud exacta de caracteres y máscara de validación regex.
- **RF-42.2:** Gestión de Métodos de Pago Habilitados:
  - Solo canales digitales (ver US-40): `TRANSFERENCIA_BANCARIA`, `YAPE`, `PLIN`, `TARJETA_DEBITO`, `TARJETA_CREDITO`, `PASARELA_PAGO`. No existen efectivo ni depósito en ventanilla.
- **RF-42.3:** Editor de Plantillas Contractuales (*Legal Templates*):
  - Gestión de textos y cláusulas legales en formato markdown o HTML con variables dinámicas reemplazables:
    `{{cliente_nombre}}`, `{{cliente_documento}}`, `{{monto_prestamo}}`, `{{tasa_tea}}`, `{{tasa_mora}}`, `{{cronograma_tabla}}`.
  - Versionado de plantillas: cada cambio genera una nueva versión (`v1`, `v2`) para que los contratos emitidos previamente preserven su texto original.

#### Criterios de Aceptación
- **CA-42.1 (Uso de Variables en Contrato):**  
  *Dado que* se actualiza la plantilla de contrato con la variable `{{tasa_mora}}`,  
  *Cuando* se desembolsa un nuevo préstamo,  
  *Entonces* el PDF generado reemplaza la variable por la tasa de mora configurada en el producto correspondiente.

---

### US-43: Administración Centralizada de Usuarios y Colaboradores del Backoffice
**Como** Administrador del Sistema,  
**Quiero** gestionar el ciclo de vida de los colaboradores internos (creación, asignación de roles y bloqueo de acceso),  
**Para** asegurar que solo el personal autorizado y activo tenga acceso al panel de control.

#### Requerimientos Funcionales
- **RF-43.1:** Alta de colaborador:
  - Nombres, apellidos, correo corporativo, teléfono, rol asignado (`ROLE_ADMIN`, `ROLE_ANALYST`, `ROLE_OPERATOR`, `ROLE_COLLECTOR`, `ROLE_AUDITOR`). Sin sucursal base: la operación es centralizada.
- **RF-43.2:** Invitación y contraseña:
  - Generación de enlace de invitación seguro por correo electrónico para que el colaborador defina su contraseña inicial cumpliendo las políticas de complejidad.
- **RF-43.3:** Suspensión inmediata:
  - Capacidad de suspender o desactivar un usuario con efecto inmediato, invalidando sus tokens activos en tiempo real.
- **RF-43.4:** Restablecimiento de credenciales asistido por el administrador en caso de bloqueo.

#### Criterios de Aceptación
- **CA-43.1 (Bloqueo Inmediato de Empleado):**  
  *Dado que* un operador es dado de baja en la empresa,  
  *Cuando* el administrador marca su usuario como `INACTIVO`,  
  *Entonces* cualquier petición subsecuente del operador es rechazada con `401 Unauthorized`, impidiendo procesar desembolsos o pagos.

---

## 3. Requisitos No Funcionales del Dominio
- **RNF-26 (Segregación de Funciones):** Ningún usuario puede auto-aprobarse permisos o cambiar su propio rol administrativo sin la intervención de otro administrador (*Principio de Cuatro Ojos*).
- **RNF-27 (Caché Distribuida de Parámetros):** Los parámetros de la empresa, feriados y catálogos deben cachearse en Redis con invalidación inmediata al actualizarse, evitando consultas repetitivas a la base de datos en cada transacción.
