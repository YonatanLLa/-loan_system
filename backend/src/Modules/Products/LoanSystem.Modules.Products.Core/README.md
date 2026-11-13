# LoanSystem.Modules.Products.Core

## 1. ¿Para qué sirve?
`LoanSystem.Modules.Products.Core` es el **Núcleo de Dominio y Aplicación del Módulo de Productos Crediticios (`Products`)**. Dentro de nuestra Arquitectura Hexagonal, este proyecto modela las reglas financieras de los productos de préstamo (tasas efectivas/nominales, plazos permitidos, montos mínimos/máximos, comisiones y métodos de amortización) y contiene el **motor de cálculo del Cronograma de Amortización Francés (`FrenchAmortizationSchedule`)**.

No tiene ninguna dependencia hacia bases de datos, HTTP ni infraestructura; todo su cálculo financiero es 100% puro, determinista y basado en aritmética `decimal`.

---

## 2. ¿Qué hace?

### Responsabilidades actuales y archivos `.cs`

- **`Domain/AmortizationMethod.cs`**
  - Define el enumerado de métodos de amortización soportados por el dominio (actualmente `French` - Cuota Fija / Sistema Francés, preparado para extenderse si el negocio incorpora otros esquemas).

- **`Domain/FrenchAmortizationSchedule.cs`**
  - Implementa el algoritmo financiero de **Amortización Francesa (Cuota Constante)** cumpliendo estrictamente las reglas contables del sistema:
    - Recibe el capital (`Money principal`), la tasa anual en porcentaje (`decimal annualRatePercentage`, ej. `24m` para 24%), el número de cuotas mensuales (`int termMonths`) y el tipo de tasa (`RateType`).
    - Convierte la tasa anual a tasa mensual con `InterestRate.ToMonthlyRate`. Para **TEA** (por defecto) usa `(1+TEA)^(1/12) − 1`, **no** `TEA/12`; ver [DECISION_01](../../../../docs/DECISION_01_TASA_EFECTIVA_TEA.md). La tasa no se redondea, y la cuota fija se calcula con potencias enteras sobre `decimal` (sin usar jamás `Math.Pow`, que opera en `double`).

- **`Domain/InterestRate.cs`**
  - `RateType` (`Efectiva` | `Nominal`, refleja el enum `rate_type` de PostgreSQL), su parser y el conversor `InterestRate.ToMonthlyRate`. La raíz duodécima de la TEA se calcula con Newton-Raphson en `decimal`.
    - Genera cada cuota (`Installment`) desglosando `Principal` (capital amortizado), `Interest` (interés del periodo redondeado a 2 decimales con `Money.RoundCurrency`) y `RemainingBalance` (saldo de capital insoluto).
    - **Ajuste de la Última Cuota (*Last-Installment Remainder Adjustment*):** En la última cuota (`number == termMonths`), ajusta el capital amortizado exactamente al saldo insoluto remanente (`remainingBalance`), garantizando la invariante financiera fundamental:
      $$\sum \text{Capital de Cuotas} = \text{Monto Principal Desembolsado}$$
      y que el saldo final después de la última cuota sea exactamente `S/ 0.00`.

- **`Contracts/SimulateScheduleContracts.cs`**
  - Define los DTOs inmutables (`sealed record`) del caso de uso de simulación:
    - `SimulateScheduleRequest(decimal Amount, decimal AnnualRatePercentage, int TermMonths, string Method = "FRENCH", string RateType = "EFECTIVA")`
    - `SimulateScheduleResponse` e `InstallmentDto` con el resumen financiero (total principal, total intereses, total a pagar y detalle cuota por cuota).

- **`Ports/Inbound/ISimulateScheduleUseCase.cs`**
  - Puerto de entrada que hereda de `IUseCase<SimulateScheduleRequest, SimulateScheduleResponse>` para exponer la simulación de cronogramas al adaptador HTTP o a otros consumidores.

- **`Ports/Outbound/IProductsUnitOfWork.cs` y `Ports/Outbound/IProductsOutbox.cs`**
  - Puertos de salida específicos del módulo para persistencia transaccional y publicación de eventos de integración cuando se creen o modifiquen productos crediticios en el catálogo.

- **`Application/SimulateScheduleUseCase.cs`**
  - Implementación `internal sealed` del caso de uso de simulación. Valida los parámetros de entrada (capital positivo, tasa válida, plazo mayor a cero, método y tipo de tasa), invoca `FrenchAmortizationSchedule.Build(...)` y retorna un `Result<SimulateScheduleResponse>` sin lanzar excepciones de negocio.

### ¿Qué se agrega aquí conforme crece el módulo?
- Entidades de dominio adicionales del catálogo (`LoanProduct`, `ProductFee`, reglas de elegibilidad por rango de monto/plazo).
- Casos de uso para crear, actualizar, activar o desactivar productos crediticios (`CreateLoanProductUseCase`, `GetActiveProductsUseCase`).
- Puertos de salida de repositorio (`ILoanProductRepository`).

---

## 3. ¿Por qué está diseñado así?

1. **Cero Punto Flotante Binario (`decimal` puro sin `Math.Pow`):**
   La fórmula clásica de anualidades $C = P \cdot \frac{i(1+i)^n}{(1+i)^n - 1}$ suele implementarse en tutoriales con `Math.Pow((double)(1 + i), n)`. Sin embargo, convertir a `double` introduce imprecisiones binarias IEEE 754 y está prohibido por nuestro escáner de bytecode IL (`FloatingPointIlScanner`). Por ello, `FrenchAmortizationSchedule` calcula la potenciación mediante multiplicación iterativa en `decimal` de alta precisión.
2. **Invariante de Cierre de Saldo Cero en la Última Cuota:**
   Debido al redondeo bancario a 2 decimales en cada cuota mensual, la suma de capitales de las $n-1$ primeras cuotas puede diferir por unos céntimos respecto al principal original. Ajustar la última cuota absorbiendo esa diferencia de redondeo es el estándar bancario para asegurar que el préstamo cierre exactamente en `0.00` sin dejar saldos fantasma.
3. **Encapsulamiento con `internal` + `InternalsVisibleTo`:**
   `SimulateScheduleUseCase` es `internal` para impedir que otros módulos lo instancien directamente saltándose los puertos. Solo `LoanSystem.Modules.Products.Adapters` y los proyectos de pruebas tienen acceso autorizado vía `InternalsVisibleTo`.
