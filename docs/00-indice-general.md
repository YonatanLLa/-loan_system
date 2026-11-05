# Sistema de Préstamos — Especificación de Historias de Usuario y Requerimientos

Este directorio contiene la documentación formal de requerimientos e historias de usuario (US) para el desarrollo de la plataforma integral de gestión de préstamos y créditos, estructurada bajo una arquitectura de **Monolito Modular** y basada en el plan de negocio definido en el [`README.md`](../README.md) del proyecto.

> **Modelo operativo: Fintech 100% digital.** Desembolsos y pagos solo por canales digitales (transferencia/CCI, Yape/Plin, tarjeta y pasarela de pago). No existen sucursales, cajas, ventanillas ni efectivo.

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
| **Operador** | `ROLE_OPERATOR` | Seguimiento de desembolsos digitales, validación de pagos pendientes de confirmación, conciliación y atención remota. No hay cajas ni cobros presenciales. |
| **Gestor de Cobranza** | `ROLE_COLLECTOR` | Cobranza 100% remota: seguimiento de cartera vencida (aging), llamadas y mensajes, compromisos y promesas de pago. |
| **Auditor** | `ROLE_AUDITOR` | Consulta de bitácoras, pistas de auditoría de transacciones, reportería de cumplimiento. |
| **Cliente / Prestatario** | `ROLE_CLIENT` | Persona natural/jurídica que opera a través del portal cliente de autoservicio. |

---

## 3. Índice de Módulos Documentados

Cada módulo cuenta con su propio archivo `.md` detallando historias de usuario, requerimientos funcionales, reglas de negocio y criterios de aceptación:

| Archivo | Módulo / Dominio | Historias de Usuario Incluidas |
|---|---|---|
| [**`01-seguridad-y-accesos.md`**](01-seguridad-y-accesos.md) | Seguridad, Autenticación y Auditoría | US-01 (Autenticación), US-02 (RBAC), US-03 (Bitácora de Auditoría) |
| [**`02-gestion-de-clientes-y-kyc.md`**](02-gestion-de-clientes-y-kyc.md) | Clientes, Expediente y KYC | US-04 (Registro Clientes), US-05 (Validación KYC), US-06 (Vista 360) |
| [**`03-productos-de-credito.md`**](03-productos-de-credito.md) | Parametrización de Productos y Reglas | US-07 (Catálogo Productos), US-08 (Métodos Amortización), US-09 (Mora y Comisiones) |
| [**`04-originacion-y-evaluacion.md`**](04-originacion-y-evaluacion.md) | Originación, Simulación y Evaluación | US-10 (Solicitud), US-11 (Simulador sin persistencia), US-12 (Motor Evaluación) |
| [**`05-desembolso-y-cronogramas.md`**](05-desembolso-y-cronogramas.md) | Desembolso y Cronograma Inmutable | US-13 (Desembolso e Idempotencia), US-14 (Cronograma Snapshot), US-15 (Contrato) |
| [**`06-pagos-y-cobros.md`**](06-pagos-y-cobros.md) | Aplicación de Pagos y Prelación | US-16 (Registro y Cascada), US-17 (Sobrepagos y Parciales), US-18 (Reversos) |
| [**`07-mora-y-cobranzas.md`**](07-mora-y-cobranzas.md) | Motor de Mora, Aging y Cobranzas | US-19 (Cálculo Diario Mora), US-20 (Bandeja Aging), US-21 (Gestiones), US-22 (Castigo) |
| [**`08-tesoreria-digital.md`**](08-tesoreria-digital.md) | Tesorería Digital y Cuentas Bancarias | US-23 (Cuentas Bancarias), US-24 (Cuadre Diario vs. Extracto), US-25 (Seguimiento de Desembolsos) |
| [**`09-portal-del-cliente.md`**](09-portal-del-cliente.md) | Portal Web de Autoservicio | US-26 (Onboarding y Acceso), US-27 (Simulación/Solicitud), US-28 (Cronograma), US-29 (Pagos Online) |
| [**`10-refinanciamiento-y-reestructuracion.md`**](10-refinanciamiento-y-reestructuracion.md) | Cancelación Anticipada y Modificaciones | US-30 (Liquidación Prepago), US-31 (Reestructuración), US-32 (Refinanciamiento) |
| [**`11-reportes-y-estados-de-cuenta.md`**](11-reportes-y-estados-de-cuenta.md) | Reportería Operativa, Financiera y Estados | US-33 (Dashboard KPIs), US-34 (Reporte Cartera/Aging), US-35 (Estado de Cuenta) |
| [**`12-integraciones-y-conciliacion.md`**](12-integraciones-y-conciliacion.md) | Integraciones Bancarias y Conciliación | US-36 (Webhooks BCP/BBVA/Yape), US-37 (Conciliación Extractos), US-38 (Cola Reprocesos) |
| [**`13-administracion-y-configuracion.md`**](13-administracion-y-configuracion.md) | Administración General y Configuración | US-39 (Datos de Empresa), US-40 (Canales Digitales), US-41 (Feriados y Días No Hábiles), US-42 (Catálogos y Plantillas Legales), US-43 (Gestión de Colaboradores) |

---

## 4. Requerimientos por Capa

Las historias de usuario anteriores están separadas por capa técnica. Cada requerimiento indica de qué historia proviene, la fase y si ya estaba especificado o es una brecha nueva frente al negocio:

| Archivo | Capa | Contenido |
|---|---|---|
| [**`REQUERIMIENTOS_FRONTEND.md`**](REQUERIMIENTOS_FRONTEND.md) | Frontend | Backoffice (`FE-BO`), portal del cliente (`FE-CP`) y no funcionales (`FE-NF`) |
| [**`REQUERIMIENTOS_BACKEND.md`**](REQUERIMIENTOS_BACKEND.md) | Backend | API y jobs por módulo (`BE-SEC`, `BE-CUS`, `BE-PRO`, `BE-ORI`, `BE-LOA`, `BE-PAY`, `BE-TRE`, `BE-COL`, `BE-INT`, `BE-NOT`, `BE-REP`, `BE-ADM`) y no funcionales (`BE-NF`) |
| [**`REQUERIMIENTOS_BDD.md`**](REQUERIMIENTOS_BDD.md) | Base de datos | Requerimientos del esquema (`DB`) contrastados con lo que ya existe en `database/` |

### 4.1 Cobertura de lo que pide el negocio (README)

| Requisito del negocio | Historias | Cobertura |
|---|---|---|
| Productos configurables (tasa, método, plazo, frecuencia, comisiones, mora, elegibilidad) | US-07 a US-09 | Parcial: faltan el día de pago, la antigüedad laboral y la separación entre comisión de administración y seguro |
| Métodos Francés, Flat, Alemán y Americano | US-08 | Completa |
| Cronograma con redondeo, snapshot y feriados | US-14, US-41 | Completa |
| Desembolso | US-13, US-25 | Completa en historias; el esquema aún no tiene el estado previo a la confirmación |
| Prelación, parciales, sobrepagos y reversos | US-16 a US-18 | Completa; falta un pago repartido entre varios préstamos |
| Mora, gracia y aging | US-09, US-19, US-20 | Completa |
| Cancelación anticipada | US-30 | Parcial: falta el prepago parcial y hay que revisar la penalidad frente a la ley |
| Refinanciamiento y reestructuración | US-31, US-32 | Completa (con un ajuste en RNF-21 por el desembolso digital) |
| Estados del préstamo por job diario | US-19 | Completa; falta `DESEMBOLSADO` en el estado de la solicitud |
| Evaluación (elegibilidad, capacidad de pago, decisiones, auditoría) | US-10, US-12 | Parcial: DTI fijo en 35/50 %; admisión sin ingreso mínimo ni antigüedad |
| KYC, AML/PEP y consentimiento | US-05, US-26 | Parcial: sin verificaciones automáticas ni consentimientos por finalidad |
| Cobranza y recordatorios | US-20 a US-22 | Parcial: sin asignación de gestores, plantillas ni horarios |
| Panel administrativo (12 módulos) | US-01 a US-43 | Parcial: faltan cuentas por cobrar, productividad y parámetros globales |
| Portal del cliente | US-26 a US-29 | Parcial: faltan las cuentas del cliente, el cambio seguro de cuenta y la devolución del saldo a favor |
| KPIs (mora, aging, recaudación, aprobación, originación, duración, prepago) | US-33 | Parcial: faltan aprobación, tiempo de originación, duración media y cancelación anticipada |
| Operación 100% digital (firma, intención de pago, conciliación, Outbox) | US-13, US-25, US-29, US-36, US-37 | Parcial: falta modelar la firma, la intención de pago y el Outbox |
| Cumplimiento en Perú (TCEA, tasas máximas, SUNAT, Libro de Reclamaciones, PLAFT) | ninguna | **No cubierto**: validar con asesoría legal y contable |
