# Dominio 01: Seguridad, Autenticación y Auditoría

## 1. Descripción del Dominio
Este dominio gestiona la autenticación, control de accesos y la pista inmutable de auditoría para todos los actores del sistema administrativo (Backoffice). Garantiza la trazabilidad absoluta de operaciones financieras y administrativas, el cumplimiento normativo y la protección de datos sensibles.

---

## 2. Historias de Usuario

### US-01: Autenticación Segura de Usuarios del Backoffice
**Como** usuario del sistema administrativo (Administrador, Analista, Operador, Gestor de Cobranza, Auditor),  
**Quiero** iniciar sesión con mis credenciales corporativas (correo y contraseña segura) y autenticación reforzada,  
**Para** acceder a las funcionalidades autorizadas de acuerdo con mi rol asignado.

#### Requerimientos Funcionales
- **RF-01.1:** El sistema debe autenticar credenciales mediante contraseñas hasheadas con algoritmo seguro (BCrypt / Argon2id con salt).
- **RF-01.2:** Generación de tokens de sesión mediante `JWT` (Access Token con vida corta de 15 a 60 minutos) y `Refresh Token` rotativo almacenado de forma segura e invalidable.
- **RF-01.3:** Bloqueo temporal de cuenta tras 5 intentos fallidos consecutivos durante un lapso de 15 minutos, registrando la IP de procedencia.
- **RF-01.4:** Endpoint para revocación explícita de sesión (`POST /api/auth/logout`), invalidando el Refresh Token.
- **RF-01.5:** Endpoint de perfil activo (`GET /api/me`) que retorne los datos del usuario logueado, rol y lista de permisos efectivos.

#### Criterios de Aceptación
- **CA-01.1 (Login Exitoso):**  
  *Dado que* un usuario activo ingresa su correo registrado y contraseña correcta,  
  *Cuando* presiona "Iniciar Sesión",  
  *Entonces* el sistema retorna código `200 OK`, emite el token JWT, retorna sus permisos y redirige a su panel inicial.
- **CA-01.2 (Credenciales Inválidas):**  
  *Dado que* se ingresa una contraseña incorrecta,  
  *Cuando* se envía la petición,  
  *Entonces* el sistema retorna `401 Unauthorized` con un mensaje genérico ("Credenciales inválidas") e incrementa el contador de fallos.
- **CA-01.3 (Bloqueo por Fuerza Bruta):**  
  *Dado que* un usuario acumula 5 intentos fallidos en menos de 15 minutos,  
  *Cuando* realiza el 6º intento,  
  *Entonces* el sistema retorna `423 Locked` y genera una alerta de seguridad en la bitácora.

---

### US-02: Control de Acceso Basado en Roles (RBAC)
**Como** Administrador del sistema,  
**Quiero** asignar y gestionar roles predefinidos con permisos específicos a los usuarios,  
**Para** garantizar el principio de mínimo privilegio y que ningún colaborador ejecute acciones fuera de su competencia.

#### Requerimientos Funcionales
- **RF-02.1:** Soporte de roles base: `Administrador`, `Analista de Crédito`, `Operador` (desembolsos y pagos digitales; no existe el rol de cajero), `Gestor de Cobranza`, `Auditor`.
- **RF-02.2:** Validación estricta a nivel de API: cada endpoint debe estar protegido por un middleware de autorización que verifique la directiva o claim de permiso (`Policies` / `Scopes`).
- **RF-02.3:** Si un usuario intenta acceder a un recurso no autorizado, el backend debe responder de inmediato con `403 Forbidden` y no procesar la petición.
- **RF-02.4:** Capacidad de activar o suspender usuarios inmediatamente, cortando la validez de los Refresh Tokens en el acto.

#### Criterios de Aceptación
- **CA-02.1 (Acceso Concedido):**  
  *Dado que* un usuario con rol `Operador` intenta invocar `POST /api/loans/{id}/payments`,  
  *Cuando* el middleware evalúa sus claims,  
  *Entonces* valida que tiene el permiso `payments.create` y permite la ejecución.
- **CA-02.2 (Acceso Denegado por Rol Insuficiente):**  
  *Dado que* un usuario con rol `Operador` intenta invocar `POST /api/loan-products` (crear producto),  
  *Cuando* el middleware evalúa la petición,  
  *Entonces* retorna `403 Forbidden` y aborta la transacción.

---

### US-03: Bitácora Inmutable de Auditoría (*AuditLog*)
**Como** Auditor de Cumplimiento o Administrador,  
**Quiero** que el sistema registre de forma automática e inalterable cada creación, modificación o transacción crítica,  
**Para** disponer de un historial fidedigno que permita investigar incidentes, conocer qué usuario operó y ver el estado anterior y posterior de los datos.

#### Requerimientos Funcionales
- **RF-03.1:** Estructura obligatoria de cada registro de auditoría (`AuditLog`):
  `id`, `entity_name`, `entity_id`, `action` (CREATE, UPDATE, DELETE, REVERSE, APPROVE), `state_before` (JSON), `state_after` (JSON), `user_id`, `user_ip`, `user_agent`, `created_at` (UTC).
- **RF-03.2:** Interceptores automáticos en capa de persistencia (Entity Framework Core / SaveChanges Interceptor) para capturar cambios sin depender de código manual en cada controlador.
- **RF-03.3:** La tabla `AuditLog` debe ser de solo inserción (*Append-Only*). Las operaciones de `UPDATE` o `DELETE` sobre esta tabla deben estar prohibidas a nivel de base de datos.
- **RF-03.4:** Endpoint de consulta (`GET /api/audit-logs`) con filtros por entidad, rango de fechas, usuario y acción, exclusivo para rol `Auditor` y `Administrador`.

#### Criterios de Aceptación
- **CA-03.1 (Registro de Cambio de Estado en Préstamo):**  
  *Dado que* un Analista aprueba una solicitud de préstamo cambiando el estado de `EN_EVALUACION` a `APROBADO`,  
  *Cuando* la transacción se confirma en la base de datos,  
  *Entonces* se inserta un registro en `AuditLog` con `entity_name: LoanApplication`, `state_before: {"status": "EN_EVALUACION"}`, `state_after: {"status": "APROBADO"}` y el ID del analista.
- **CA-03.2 (Inmutabilidad del Log):**  
  *Dado que* un usuario intenta modificar o borrar un registro de auditoría,  
  *Cuando* se ejecuta la instrucción,  
  *Entonces* la base de datos rechaza la operación y el log permanece intacto.

---

## 3. Requisitos No Funcionales del Dominio
- **RNF-01 (Seguridad Criptográfica):** Claves privadas JWT rotadas periódicamente; tokens firmados con RS256 o HS512.
- **RNF-02 (Rendimiento del Log):** La persistencia del `AuditLog` debe ejecutarse dentro de la misma transacción de base de datos o mediante cola asíncrona garantizada para no degradar el tiempo de respuesta por debajo de 200 ms.
- **RNF-03 (Protección de Secretos):** Variables de entorno sensibles (connection strings, JWT secrets) gestionadas vía almacén de secretos (Key Vault o variables protegidas).
