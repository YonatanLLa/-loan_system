# Dominio 13: Administración General, Sucursales y Parámetros Globales

## 1. Descripción del Dominio
Este dominio centraliza la configuración corporativa y operativa del Backoffice. Permite al Administrador gestionar los datos fiscales de la empresa, sucursales y agencias físicas, zonas de cobertura, el calendario oficial de feriados (crucial para el cálculo de cronogramas sin vencimientos en días inhábiles), la parametrización de monedas, métodos de pago admitidos y las plantillas legales de los contratos.

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

### US-40: Gestión de Sucursales, Zonas y Asignación de Personal
**Como** Administrador de Operaciones,  
**Quiero** crear y estructurar las agencias físicas, sucursales y zonas geográficas de atención,  
**Para** segmentar la operación de cajas, analistas, carteras de crédito y reportes territoriales.

#### Requerimientos Funcionales
- **RF-40.1:** Creación de Sucursal (`Branch`):
  - Código único de agencia, nombre de la sucursal, dirección física, zona/distrito y estado (`ACTIVA`, `INACTIVA`).
- **RF-40.2:** Creación de Zonas Geográficas (`Zone`):
  - Agrupación territorial para asignación de gestores de cobranza y analistas de campo.
- **RF-40.3:** Asignación de usuarios internos:
  - Cada cajero debe estar vinculado a una sucursal específica para operar sus cajas físicas.
  - Los analistas y cobradores pueden asociarse a una o múltiples sucursales/zonas.
- **RF-40.4:** Endpoints: `POST /api/admin/branches`, `GET /api/admin/branches`, `PUT /api/admin/branches/{id}`.

#### Criterios de Aceptación
- **CA-40.1 (Creación de Agencia):**  
  *Dado que* se ingresan los datos de la "Sucursal Norte" con código `SUC-02`,  
  *Cuando* el administrador confirma la creación,  
  *Entonces* la sucursal queda disponible para asignarle cajas físicas y personal de ventanilla.

---

### US-41: Calendario de Feriados y Días No Hábiles
**Como** Administrador o Especialista de Operaciones,  
**Quiero** registrar el calendario oficial de feriados nacionales y locales del año,  
**Para** que el motor de cronogramas desplace automáticamente los vencimientos que coincidan con días no laborables.

#### Requerimientos Funcionales
- **RF-41.1:** Entidad `Holiday`:
  - `date`: Fecha específica (año-mes-día).
  - `name`: Motivo (ej. "Año Nuevo", "Fiestas Patrias", "Feriado Local").
  - `scope`: `NACIONAL` o `SUCURSAL` (feriado patronal local).
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
  - Activación/desactivación de métodos: `EFECTIVO`, `TRANSFERENCIA`, `YAPE`, `PLIN`, `TARJETA`, `DEPOSITO_VENTANILLA`.
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
**Quiero** gestionar el ciclo de vida de los colaboradores internos (creación, asignación de roles, bloqueo de acceso y reasignación de sucursales),  
**Para** asegurar que solo el personal autorizado y activo tenga acceso al panel de control.

#### Requerimientos Funcionales
- **RF-43.1:** Alta de colaborador:
  - Nombres, apellidos, correo corporativo, teléfono, rol asignado (`ROLE_ADMIN`, `ROLE_ANALYST`, `ROLE_CASHIER`, `ROLE_COLLECTOR`, `ROLE_AUDITOR`) y sucursal base.
- **RF-43.2:** Invitación y contraseña:
  - Generación de enlace de invitación seguro por correo electrónico para que el colaborador defina su contraseña inicial cumpliendo las políticas de complejidad.
- **RF-43.3:** Suspensión inmediata:
  - Capacidad de suspender o desactivar un usuario con efecto inmediato, invalidando sus tokens activos en tiempo real.
- **RF-43.4:** Restablecimiento de credenciales asistido por el administrador en caso de bloqueo.

#### Criterios de Aceptación
- **CA-43.1 (Bloqueo Inmediato de Empleado):**  
  *Dado que* un cajero es dado de baja en la empresa,  
  *Cuando* el administrador marca su usuario como `INACTIVO`,  
  *Entonces* cualquier petición subsecuente del cajero es rechazada con `401 Unauthorized` impidiendo cualquier operación en caja.

---

## 3. Requisitos No Funcionales del Dominio
- **RNF-26 (Segregación de Funciones):** Ningún usuario puede auto-aprobarse permisos o cambiar su propio rol administrativo sin la intervención de otro administrador (*Principio de Cuatro Ojos*).
- **RNF-27 (Caché Distribuida de Parámetros):** Los parámetros de la empresa, feriados y catálogos deben cachearse en Redis con invalidación inmediata al actualizarse, evitando consultas repetitivas a la base de datos en cada transacción.
