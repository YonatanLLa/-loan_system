# Dominio 02: Gestión de Clientes, Expedientes y KYC

## 1. Descripción del Dominio
Este dominio administra la información maestra de los clientes (personas naturales y personas jurídicas), su perfil sociodemográfico y económico, la validación de cumplimiento normativo (KYC / *Know Your Customer*) y el resguardo de expedientes digitales de soporte.

---

## 2. Historias de Usuario

### US-04: Registro y Actualización de Clientes
**Como** Analista de Crédito u Operador (o el propio cliente desde el portal, US-26),  
**Quiero** registrar a nuevos solicitantes (personas naturales o jurídicas) y actualizar sus datos de contacto y financieros,  
**Para** mantener una base de datos fidedigna y habilitarlos para la solicitud de préstamos.

#### Requerimientos Funcionales
- **RF-04.1:** Registro de cliente con los siguientes atributos obligatorios:
  - Tipo de persona: `NATURAL` | `JURIDICA`.
  - Tipo de documento: `DNI`, `CE` (Carné de Extranjería), `RUC`, `PASAPORTE`.
  - Número de documento (con validación de formato y dígito verificador según país/tipo).
  - Nombre completo / Razón social.
  - Datos de contacto: Correo electrónico verificado, teléfono móvil principal y secundario, dirección de domicilio.
  - Perfil socioeconómico: Ingreso mensual neto declarado, ocupación, empresa/empleador y antigüedad laboral.
- **RF-04.2:** Restricción de unicidad: No se puede registrar dos veces el mismo tipo y número de documento.
- **RF-04.3:** Historial de modificaciones de datos críticos (ingresos, dirección, teléfono) registrado en auditoría.
- **RF-04.4:** Endpoint de creación: `POST /api/customers` y consulta por ID: `GET /api/customers/{id}`.

#### Criterios de Aceptación
- **CA-04.1 (Registro Exitoso):**  
  *Dado que* se ingresan los datos válidos de un nuevo cliente con DNI no existente,  
  *Cuando* se invoca `POST /api/customers`,  
  *Entonces* se crea el registro con `kyc_status = PENDING`, se asigna ID único y retorna `201 Created`.
- **CA-04.2 (Documento Duplicado):**  
  *Dado que* ya existe un cliente con DNI `12345678`,  
  *Cuando* se intenta registrar otro cliente con el mismo documento,  
  *Entonces* el sistema rechaza la operación con código `409 Conflict` e indica el conflicto.

---

### US-05: Validación KYC y Expediente Documental
**Como** Oficial de Cumplimiento o Analista de Crédito,  
**Quiero** verificar la identidad del cliente, revisar listas de prevención de lavado de activos (AML/PEP) y adjuntar sus documentos probatorios,  
**Para** mitigar riesgos de fraude, cumplir la regulación y habilitar al cliente para el desembolso de fondos.

#### Requerimientos Funcionales
- **RF-05.1:** Carga y almacenamiento seguro de archivos de soporte (fotos del documento de identidad por ambos lados, recibo de servicios para comprobante de domicilio, boletas de pago / PDT de ingresos).
- **RF-05.2:** Gestión de estados KYC del cliente: `PENDING` (Pendiente), `VERIFIED` (Verificado), `REJECTED` (Rechazado), `EXPIRED` (Expirado).
- **RF-05.3:** Regla de negocio crítica: **El sistema debe impedir el desembolso de cualquier préstamo si el cliente no cuenta con `kyc_status = VERIFIED`**.
- **RF-05.4:** Registro de evidencia de consentimiento informado para tratamiento de datos personales y consulta de historial crediticio.

#### Criterios de Aceptación
- **CA-05.1 (Aprobación KYC):**  
  *Dado que* un cliente tiene cargados sus documentos válidos y no figura en listas restrictivas,  
  *Cuando* el analista aprueba el KYC con notas de verificación,  
  *Entonces* el estado cambia a `VERIFIED`, se guarda fecha y usuario aprobador, y se desbloquea el desembolso.
- **CA-05.2 (Bloqueo de Desembolso por KYC Incompleto):**  
  *Dado que* un préstamo fue aprobado pero el cliente aún tiene `kyc_status = PENDING`,  
  *Cuando* el operador o el sistema intenta procesar el desembolso (`POST /api/loans`),  
  *Entonces* la operación se bloquea con código `422 Unprocessable Entity` ("Cliente con KYC pendiente").

---

### US-06: Ficha Integral 360 del Cliente
**Como** Analista de Crédito o Gestor de Cobranza,  
**Quiero** visualizar en una sola pantalla todo el historial del cliente (préstamos históricos, préstamos vigentes, nivel de mora actual y comportamiento de pago),  
**Para** tomar decisiones informadas de aprobación o priorizar estrategias de cobro.

#### Requerimientos Funcionales
- **RF-06.1:** Vista consolidada que agregue:
  - Préstamos activos con saldo insoluto y estado (`VIGENTE`, `EN_MORA`).
  - Días de atraso máximos históricos y actuales.
  - Sumatoria total de deuda pendiente (Capital + Intereses + Mora acumulada).
  - Listado cronológico de solicitudes pasadas (aprobadas, rechazadas, liquidadas).
  - Alerta visual destacada si el cliente tiene mora activa en cualquiera de sus créditos.
- **RF-06.2:** Soporte explícito de la regla: **Un cliente puede mantener múltiples préstamos activos simultáneamente** siempre que su capacidad de endeudamiento lo permita.

#### Criterios de Aceptación
- **CA-06.1 (Visualización Multicrédito):**  
  *Dado que* un cliente tiene un préstamo vigente al día y otro con 12 días de atraso,  
  *Cuando* el operador consulta la ficha 360 del cliente (`GET /api/customers/{id}`),  
  *Entonces* la respuesta detalla ambos créditos, desglosa la deuda individual y calcula el total adeudado consolidado.

---

## 3. Requisitos No Funcionales del Dominio
- **RNF-04 (Almacenamiento de Documentos):** Los archivos adjuntos (imágenes, PDFs) deben guardarse en almacenamiento de objetos (S3 / Azure Blob / Local seguro) referenciando únicamente el hash y URI en la base de datos.
- **RNF-05 (Protección de Datos Personales):** Encriptación en reposo para campos sensibles y enmascaramiento de datos bancarios o números de cuenta en vistas no operativas.
