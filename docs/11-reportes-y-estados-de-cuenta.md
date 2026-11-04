# Dominio 11: Reportería Operativa, Financiera y Estados de Cuenta

## 1. Descripción del Dominio
Este dominio proporciona inteligencia de negocio, control financiero y transparencia a través de un panel de control con indicadores clave (KPIs), reportes exportables de cartera y morosidad, flujo de caja proyectado vs. real, y la emisión de los estados de cuenta oficiales exigidos por los prestatarios y entes reguladores.

---

## 2. Historias de Usuario

### US-33: Panel de Control Gerencial con KPIs en Tiempo Real (*Dashboard*)
**Como** Administrador, Gerente General o Jefe de Operaciones,  
**Quiero** visualizar en un tablero interactivo los principales indicadores de desempeño del negocio,  
**Para** supervisar la salud de la cartera crediticia, el volumen de colocaciones y la recaudación del día.

#### Requerimientos Funcionales
- **RF-33.1:** Métricas y tarjetas de indicadores (KPIs):
  - **Saldo de Cartera Total Colocada:** Desglose entre cartera `Vigente`, `En Mora` y `Castigada`.
  - **Índice de Morosidad (%):**
    $$\text{Ratio de Mora} = \frac{\text{Saldo de Capital en Mora}}{\text{Saldo de Cartera Total}} \times 100$$
  - **Recaudación del Día:** Monto recaudado real vs. Meta/Expectativa de cobro del día.
  - **Colocaciones / Desembolsos del Día:** Monto total y número de operaciones otorgadas.
  - **Saldo Consolidado en Cajas y Bancos.**
- **RF-33.2:** Gráficas interactivas:
  - Evolución mensual de colocaciones vs. recuperaciones.
  - Distribución de la cartera por producto y por tramo de mora (*Aging Chart*).
- **RF-33.3:** Endpoint: `GET /api/reports/dashboard-kpis` con caché de corta duración (Redis, TTL de 60 segundos).

#### Criterios de Aceptación
- **CA-33.1 (Cálculo Correcto del Ratio de Mora):**  
  *Dado que* la cartera total es de \$ 1,000,000 y el capital con cuotas vencidas suma \$ 50,000,  
  *Cuando* el directivo abre el dashboard,  
  *Entonces* el sistema muestra un índice de morosidad de 5.00%.

---

### US-34: Reportes de Cartera, *Aging* y Flujo de Caja
**Como** Analista Financiero, Contador o Auditor,  
**Quiero** generar reportes consolidados y detallados con filtros por fecha, sucursal o producto, y exportarlos en formatos PDF y Excel,  
**Para** realizar análisis de solvencia, conciliación contable y remitir informes a la gerencia.

#### Requerimientos Funcionales
- **RF-34.1:** **Reporte de Cartera General (`GET /api/reports/portfolio`):**
  - Lista detallada de todos los créditos: ID, cliente, fecha de inicio, capital original, saldo capital actual, cuotas pendientes, estado.
- **RF-34.2:** **Reporte de Morosidad por Antigüedad / *Aging* (`GET /api/reports/aging`):**
  - Clasificación de préstamos por tramos: Corriente / Al día, 1-30 días, 31-60 días, 61-90 días, >90 días.
  - Cuantificación de provisión exigida por nivel de riesgo.
- **RF-34.3:** **Reporte de Flujo de Caja (`GET /api/reports/cashflow`):**
  - Entradas de dinero (cobro de capital, cobro de intereses, cobro de mora, comisiones).
  - Salidas de dinero (desembolsos, gastos operativos, comisiones bancarias).
  - Flujo neto del período seleccionado.
- **RF-34.4:** Formatos de exportación obligatorios:
  - `Excel (.xlsx)` con tablas dinámicas y fórmulas nativas de suma.
  - `PDF` con membrete corporativo, firma digital de generación y paginado formal.

#### Criterios de Aceptación
- **CA-34.1 (Exportación a Excel sin Truncar):**  
  *Dado que* se filtra la cartera del mes con 5,000 registros,  
  *Cuando* el usuario presiona "Exportar a Excel",  
  *Entonces* el archivo se descarga en formato .xlsx válido conservando los tipos numéricos con 2 decimales.

---

### US-35: Emisión de Estado de Cuenta Oficial por Préstamo
**Como** Cliente o Cajero de Atención al Usuario,  
**Quiero** consultar y emitir el Estado de Cuenta detallado e histórico de un préstamo,  
**Para** conocer la cronología completa de pagos efectuados, saldos amortizados y obligaciones pendientes.

#### Requerimientos Funcionales
- **RF-35.1:** Endpoint: `GET /api/reports/account-statement/{loanId}`.
- **RF-35.2:** Contenido del Estado de Cuenta:
  - Datos de cabecera: Datos del cliente, número de contrato, fecha de desembolso, monto original, tasa TEA, plazo.
  - Resumen ejecutivo: Total pagado a la fecha (desglosado en capital, interés, comisiones, mora) y saldo de capital pendiente.
  - Tabla histórica de movimientos: Todas las transacciones ordenadas cronológicamente (desembolso, abonos de pago con fecha y número de recibo, cuotas vencidas).
  - Próximo vencimiento exigible.
- **RF-35.3:** Opción de descarga en PDF con código QR de verificación de autenticidad.

#### Criterios de Aceptación
- **CA-35.1 (Consistencia Matemática del Estado de Cuenta):**  
  *Dado que* un cliente solicita su estado de cuenta tras realizar 5 pagos,  
  *Cuando* se genera el documento,  
  *Entonces* la sumatoria de capital amortizado en los 5 pagos más el saldo de capital restante equivale exactamente al capital desembolsado original.

---

## 3. Requisitos No Funcionales del Dominio
- **RNF-22 (Optimización de Consultas Analíticas):** Los reportes masivos de cartera deben ejecutarse contra réplicas de lectura de base de datos o vistas materializadas indexadas para no degradar las transacciones OLTP de caja y cobros.
- **RNF-23 (Streaming de Datos en Exportación):** Las exportaciones masivas a Excel deben generarse por transmisión en streaming (OpenXML / FastExcel) para evitar desbordamientos de memoria en el servidor backend.
