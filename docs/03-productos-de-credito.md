# Dominio 03: Parametrización de Productos de Crédito y Reglas Financieras

## 1. Descripción del Dominio
Este dominio define el catálogo de productos de crédito de la institución. Permite a los administradores parametrizar las condiciones financieras, métodos matemáticos de amortización, tipos de tasas de interés, frecuencias de pago, comisiones y reglas de penalización por mora.

---

## 2. Historias de Usuario

### US-07: Configuración de Productos de Crédito
**Como** Administrador del sistema o Gerente de Producto,  
**Quiero** crear y configurar plantillas de productos de crédito con sus límites y condiciones,  
**Para** ofrecer alternativas de financiamiento estructuradas y adaptadas al perfil de los clientes.

#### Requerimientos Funcionales
- **RF-07.1:** Atributos de configuración del producto (`LoanProduct`):
  - Nombre comercial y descripción.
  - Moneda (`PEN`, `USD`, etc.).
  - Límites de capital: Monto mínimo y monto máximo permitido.
  - Límites de plazo: Número mínimo y máximo de cuotas permitidas.
  - Frecuencias de pago admitidas: `DIARIA`, `SEMANAL`, `QUINCENAL`, `MENSUAL`.
  - Reglas de elegibilidad base: Edad mínima (ej. 18), edad máxima (ej. 70), ingreso mínimo requerido.
  - Estado: `ACTIVO` o `INACTIVO`.
- **RF-07.2:** Endpoints: `POST /api/loan-products` para creación y `GET /api/loan-products` para listado de productos disponibles.

#### Criterios de Aceptación
- **CA-07.1 (Creación Exitosa):**  
  *Dado que* se definen los parámetros válidos de un producto "Microcrédito Diario",  
  *Cuando* el administrador envía la configuración,  
  *Entonces* el producto se almacena activo y queda disponible para generar solicitudes.
- **CA-07.2 (Validación de Límites):**  
  *Dado que* se intenta configurar un monto mínimo mayor al monto máximo,  
  *Cuando* se procesa el formulario,  
  *Entonces* el sistema rechaza la solicitud indicando inconsistencia de rangos.

---

### US-08: Definición de Métodos de Amortización y Tasas
**Como** Administrador o Especialista Financiero,  
**Quiero** asociar a cada producto un método matemático de cálculo de amortización y especificar las tasas de interés efectivas,  
**Para** garantizar la exactitud matemática de los cronogramas y el cumplimiento de las normas de transparencia financiera.

#### Requerimientos Funcionales
- **RF-08.1:** Soporte de 4 métodos de amortización:
  1. **Sistema Francés (Cuota Nivelada):** Cuota constante ($R$), interés decreciente sobre saldo, amortización creciente.
     $$\text{Cuota } R = \frac{P \cdot i}{1 - (1 + i)^{-n}}$$
  2. **Interés Simple / Flat (Cuota Fija):** Interés calculado sobre el capital original desembolsado. Habitual en microfinanzas diarias.
     $$\text{Interés Total} = P \cdot i \cdot n \quad ; \quad \text{Cuota} = \frac{P + \text{Interés Total}}{n}$$
  3. **Sistema Alemán:** Amortización de capital constante ($P / n$), cuota decreciente con interés calculado sobre el saldo insoluto.
  4. **Sistema Americano (Bullet):** Pago periódico exclusivo de intereses devengados y amortización del 100% del capital en la última cuota.
- **RF-08.2:** Estandarización de tasas: Todo producto debe almacenar su tasa equivalente anual (**TEA**) de referencia y convertir matemáticamente a la tasa del período según la frecuencia elegida (ej. tasa efectiva diaria o mensual).
- **RF-08.3:** Precisión: Las tasas de interés deben guardarse con al menos 6 decimales de precisión (`decimal(12, 6)`).

#### Criterios de Aceptación
- **CA-08.1 (Validación de Método Francés):**  
  *Dado que* un producto utiliza método Francés con TEA del 30% a 12 meses,  
  *Cuando* el motor genera las cuotas,  
  *Entonces* cada cuota mensual mantiene un valor nominal constante (ajustando centavos en la cuota final).

---

### US-09: Configuración de Comisiones, Días de Gracia y Políticas de Mora
**Como** Administrador o Responsable de Riesgos,  
**Quiero** establecer las comisiones aplicables, días de gracia antes de penalización y la fórmula de cálculo de mora para cada producto,  
**Para** automatizar el cobro de recargos conforme a las políticas de crédito aprobadas.

#### Requerimientos Funcionales
- **RF-09.1:** Configuración de comisiones:
  - Comisión de apertura/desembolso: Monto fijo o porcentaje del capital.
  - Modo de cobro: Deducido del desembolso líquido o financiado en el préstamo.
  - Gastos administrativos o seguros periódicos (monto fijo por cuota).
- **RF-09.2:** Días de gracia: Número de días posteriores a la fecha de vencimiento (`due_date`) durante los cuales no se aplica penalidad ni interés moratorio.
- **RF-09.3:** Parámetros de mora:
  - Tasa de mora diaria (`rate_mora_per_day`).
  - Base de cálculo: `SALDO_CAPITAL_VENCIDO` o `CUOTA_TOTAL_VENCIDA`.
  - Fórmula:
    $$\text{Mora} = \text{Base} \times \text{Tasa Mora Diaria} \times \text{Días de Atraso}$$
- **RF-09.4:** Anatocismo bloqueado: Por defecto, los intereses moratorios no capitalizan (no generan nuevos intereses sobre la mora).

#### Criterios de Aceptación
- **CA-09.1 (Aplicación de Días de Gracia):**  
  *Dado que* un producto tiene 3 días de gracia y una cuota vence el día 10,  
  *Cuando* el cliente paga el día 12 (dentro de la gracia),  
  *Entonces* no se liquida mora adicional. Si paga el día 14, se liquida la mora correspondiente a los 4 días de atraso acumulados.

---

## 3. Requisitos No Funcionales del Dominio
- **RNF-06 (Versionado de Productos):** Si se modifica la tasa o condiciones de un producto, los préstamos ya desembolsados deben mantener inalterables los parámetros con los que fueron pactados (congelamiento de condiciones contractuales).
- **RNF-07 (Precisión Decimal):** Cero tolerancia a discrepancias de centavos entre el valor nominal calculado y la suma acumulada de cuotas.
