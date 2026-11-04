# Sistema de Préstamos — Especificación de Historias de Usuario y Requerimientos

Este directorio contiene la documentación formal de requerimientos e historias de usuario (US) para el desarrollo de la plataforma integral de gestión de préstamos y créditos, estructurada bajo una arquitectura de **Monolito Modular** y basada en el plan de negocio definido en [`c:\Users\SISTEMAS\Desktop\PLAN`](file:///c:/Users/SISTEMAS/Desktop/PLAN/README.md).

---

## 1. Convenciones y Estándares

### 1.1 Nomenclatura
- **US-[NN]:** Historia de Usuario (*User Story*).
- **RF-[NN]:** Requerimiento Funcional.
- **RNF-[NN]:** Requerimiento No Funcional.
- **CA-[NN]:** Criterio de Aceptación (formulado en formato Gherkin: *Dado que... Cuando... Entonces...*).

### 1.2 Reglas Transversales de Negocio (Core Inviolable)
1. **Precisión Financiera:** Todas las operaciones monetarias se procesan con tipos de alta precisión (`decimal` / `numeric(18, 4)` en base de datos; `decimal` de 128 bits en .NET), con redondeo bancario *Half-Up* a 2 decimales en cuotas finales. Nunca utilizar tipos de punto flotante (`float`, `double`).
2. **Inmutabilidad y Auditoría:** Todo cronograma desembolsado y todo pago asentado es inmutable. Las correcciones se realizan exclusivamente mediante contra-asientos o eventos compensatorios.
3. **Prelación de Pagos:** Ningún pago amortiza capital antes de cubrir en orden estricto: `1. Mora y penalidades` → `2. Intereses vencidos/corrientes` → `3. Comisiones/seguros` → `4. Saldo a Capital`.
4. **Idempotencia:** Todos los endpoints de transacciones financieras críticas (desembolsos, pagos, reversos, webhooks) exigen cabecera `Idempotency-Key`.

---

## 2. Mapa de Actores y Roles

| Rol | Código | Descripción y Alcance |
|---|---|---|
| **Administrador** | `ROLE_ADMIN` | Configuración global, productos, tasas, usuarios, reglas y auditoría general. |
| **Analista de Crédito** | `ROLE_ANALYST` | Evaluación de solicitudes, análisis de capacidad de pago, scoring y dictamen. |
| **Cajero / Operador** | `ROLE_CASHIER` | Apertura/cierre de caja, registro manual de pagos, desembolsos y cobros presenciales. |
| **Gestor de Cobranza** | `ROLE_COLLECTOR` | Seguimiento de cartera vencida (aging), gestión de compromisos y promesas de pago. |
| **Auditor** | `ROLE_AUDITOR` | Consulta de bitácoras, pistas de auditoría de transacciones, reportería de cumplimiento. |
| **Cliente / Prestatario** | `ROLE_CLIENT` | Persona natural/jurídica que opera a través del portal cliente de autoservicio. |

---

## 3. Índice de Módulos Documentados

Cada módulo cuenta con su propio archivo `.md` detallando historias de usuario, requerimientos funcionales, reglas de negocio y criterios de aceptación:

| Archivo | Módulo / Dominio | Historias de Usuario Incluidas |
|---|---|---|
| [**`01-seguridad-y-accesos.md`**](file:///c:/Users/SISTEMAS/Documents/Apps/documents-pace/docs/01-seguridad-y-accesos.md) | Seguridad, Autenticación y Auditoría | US-01 (Autenticación), US-02 (RBAC), US-03 (Bitácora de Auditoría) |
| [**`02-gestion-de-clientes-y-kyc.md`**](file:///c:/Users/SISTEMAS/Documents/Apps/documents-pace/docs/02-gestion-de-clientes-y-kyc.md) | Clientes, Expediente y KYC | US-04 (Registro Clientes), US-05 (Validación KYC), US-06 (Vista 360) |
| [**`03-productos-de-credito.md`**](file:///c:/Users/SISTEMAS/Documents/Apps/documents-pace/docs/03-productos-de-credito.md) | Parametrización de Productos y Reglas | US-07 (Catálogo Productos), US-08 (Métodos Amortización), US-09 (Mora y Comisiones) |
| [**`04-originacion-y-evaluacion.md`**](file:///c:/Users/SISTEMAS/Documents/Apps/documents-pace/docs/04-originacion-y-evaluacion.md) | Originación, Simulación y Evaluación | US-10 (Solicitud), US-11 (Simulador sin persistencia), US-12 (Motor Evaluación) |
| [**`05-desembolso-y-cronogramas.md`**](file:///c:/Users/SISTEMAS/Documents/Apps/documents-pace/docs/05-desembolso-y-cronogramas.md) | Desembolso y Cronograma Inmutable | US-13 (Desembolso e Idempotencia), US-14 (Cronograma Snapshot), US-15 (Contrato) |
| [**`06-pagos-y-cobros.md`**](file:///c:/Users/SISTEMAS/Documents/Apps/documents-pace/docs/06-pagos-y-cobros.md) | Aplicación de Pagos y Prelación | US-16 (Registro y Cascada), US-17 (Sobrepagos y Parciales), US-18 (Reversos) |
| [**`07-mora-y-cobranzas.md`**](file:///c:/Users/SISTEMAS/Documents/Apps/documents-pace/docs/07-mora-y-cobranzas.md) | Motor de Mora, Aging y Cobranzas | US-19 (Cálculo Diario Mora), US-20 (Bandeja Aging), US-21 (Gestiones), US-22 (Castigo) |
| [**`08-caja-y-tesoreria.md`**](file:///c:/Users/SISTEMAS/Documents/Apps/documents-pace/docs/08-caja-y-tesoreria.md) | Caja, Arqueo y Tesorería | US-23 (Apertura/Cierre Ciego), US-24 (Movimientos), US-25 (Transferencias) |
| [**`09-portal-del-cliente.md`**](file:///c:/Users/SISTEMAS/Documents/Apps/documents-pace/docs/09-portal-del-cliente.md) | Portal Web de Autoservicio | US-26 (Onboarding y Acceso), US-27 (Simulación/Solicitud), US-28 (Cronograma), US-29 (Pagos Online) |
| [**`10-refinanciamiento-y-reestructuracion.md`**](file:///c:/Users/SISTEMAS/Documents/Apps/documents-pace/docs/10-refinanciamiento-y-reestructuracion.md) | Cancelación Anticipada y Modificaciones | US-30 (Liquidación Prepago), US-31 (Reestructuración), US-32 (Refinanciamiento) |
| [**`11-reportes-y-estados-de-cuenta.md`**](file:///c:/Users/SISTEMAS/Documents/Apps/documents-pace/docs/11-reportes-y-estados-de-cuenta.md) | Reportería Operativa, Financiera y Estados | US-33 (Dashboard KPIs), US-34 (Reporte Cartera/Aging), US-35 (Estado de Cuenta) |
| [**`12-integraciones-y-conciliacion.md`**](file:///c:/Users/SISTEMAS/Documents/Apps/documents-pace/docs/12-integraciones-y-conciliacion.md) | Integraciones Bancarias y Conciliación | US-36 (Webhooks BCP/BBVA/Yape), US-37 (Conciliación Extractos), US-38 (Cola Reprocesos) |
| [**`13-administracion-y-configuracion.md`**](file:///c:/Users/SISTEMAS/Documents/Apps/documents-pace/docs/13-administracion-y-configuracion.md) | Administración General y Configuración | US-39 (Datos de Empresa), US-40 (Sucursales/Zonas), US-41 (Feriados y Días No Hábiles), US-42 (Catálogos y Plantillas Legales), US-43 (Gestión de Colaboradores) |
