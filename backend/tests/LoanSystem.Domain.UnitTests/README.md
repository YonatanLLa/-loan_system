# LoanSystem.Domain.UnitTests

## 1. ¿Para qué sirve?
`LoanSystem.Domain.UnitTests` es la **Suite de Pruebas Unitarias del Dominio Financiero**. Dentro de nuestra estrategia de testing en la Arquitectura Hexagonal, este proyecto verifica de forma aislada, ultrarrápida (en milisegundos y sin base de datos ni red) las reglas de negocio más críticas del corazón del sistema: la aritmética monetaria (`Money`) y los algoritmos financieros como el **Cronograma de Amortización Francés (`FrenchAmortizationSchedule`)**.

---

## 2. ¿Qué hace?

### Responsabilidades actuales y archivos `.cs`

- **`MoneyTests.cs`**
  - Verifica exhaustivamente todas las invariantes del Value Object `Money` (`LoanSystem.Common.Core`):
    - **Redondeo de moneda a 2 decimales (`RoundCurrency`)** y **redondeo de cálculo intermedio a 4 decimales (`RoundCalculation`)** usando `MidpointRounding.AwayFromZero`.
    - Operaciones aritméticas seguras (`+`, `-`, `*`, asignación/prorrateo `Allocate` sin pérdida de céntimos).
    - Rechazo inmediato de operaciones entre monedas distintas (ej. intentar sumar `"PEN"` con `"USD"`).
    - Normalización de códigos de moneda ISO de 3 letras.

- **`FrenchAmortizationScheduleTests.cs`**
  - Prueba el motor matemático de generación de cronogramas en `LoanSystem.Modules.Products.Core`:
    - Verifica que para cualquier combinación válida de capital, tasa anual y plazo en meses, **la suma del capital amortizado de todas las cuotas sea exactamente igual al monto principal desembolsado** ($\sum \text{Principal}_i = \text{Principal Total}$).
    - Comprueba que el **ajuste de la última cuota (*last-installment remainder adjustment*)** deje el saldo insoluto final (`RemainingBalance`) exactamente en `S/ 0.00`.
    - Valida casos borde: préstamos con tasa `0%` (interés cero), plazos de 1 sola cuota y rechazo de parámetros inválidos (capital negativo o cero, plazo menor a 1 mes).

### ¿Qué se agrega aquí conforme crece el sistema?
- Pruebas unitarias para nuevas entidades y servicios de dominio de los 12 módulos:
  - Algoritmo de prelación de imputación de pagos (`Mora → Interés → Capital`) de `Payments.Core`.
  - Cálculo de mora diaria y clasificación por *aging buckets* (`1-30`, `31-60`, `61-90`, `90+`) de `Collections.Core`.
  - Máquina de estados de solicitudes de crédito (`Origination.Core`) y préstamos (`Loans.Core`).

---

## 3. ¿Por qué está diseñado así?

1. **Cero Mocks de Infraestructura:**
   Como los proyectos `*.Core` son 100% libres de infraestructura y efectos secundarios externos, las pruebas en `LoanSystem.Domain.UnitTests` son funciones puras: dados unos valores de entrada (`Money`, tasas `decimal`, fechas `DateOnly`), producen siempre el mismo resultado matemático.
2. **Garantía contra Errores de Céntimos (*Penny Drift*):**
   En sistemas crediticios, un error de redondeo de `S/ 0.01` multiplicado por miles de cuotas descuadra la contabilidad y genera reclamos regulatorios. Estas pruebas blindan las fórmulas financieras ante cualquier refactorización futura.
