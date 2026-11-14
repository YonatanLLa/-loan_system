# DECISIÓN 01: la tasa anual es efectiva (TEA), no nominal

| Campo | Valor |
|---|---|
| Fecha | 2026-11-10 |
| Estado | Aplicada |
| Módulo | Products (motor de amortización) |
| Requerimientos | BE-PRO-02, BE-ORI-03, BE-LOA-04 |
| Archivos | `Products.Core/Domain/InterestRate.cs`, `Products.Core/Domain/FrenchAmortizationSchedule.cs`, `Products.Core/Application/SimulateScheduleUseCase.cs` |

## 1. Problema detectado

El motor calculaba la tasa mensual así:

```csharp
var monthlyRate = annualRatePercentage / 100m / 12m;   // TNA / 12
```

Esa fórmula solo es válida para una **Tasa Nominal Anual (TNA)**. En el Perú los productos de crédito se publican con **TEA (Tasa Efectiva Anual)**, que es lo que exigen la SBS y las normas de transparencia de información al usuario. El esquema de base de datos ya lo asume:

- `database/migrations/04_loan_products.sql`: `rate_type DEFAULT 'EFECTIVA'`, con el comentario *"Siempre se almacena la TEA… La app convierte a tasa del período."*
- `database/docs/diccionario-de-datos.md`: `annual_rate` = "Tasa Efectiva Anual (TEA en decimal)".

**Consecuencia:** al dividir la TEA entre 12, el interés cobrado es **mayor** que la tasa anunciada al cliente. La tasa efectiva real resultaba en `(1 + TEA/12)^12 − 1`, que siempre es mayor que la TEA. Eso es un riesgo regulatorio, porque se cobra por encima de la tasa informada.

| TEA anunciada | Tasa mensual incorrecta (TEA/12) | TEA real cobrada | Tasa mensual correcta |
|---|---|---|---|
| 12% | 1.0000% | 12.68% | 0.9489% |
| 30% | 2.5000% | 34.49% | 2.2104% |
| 60% | 5.0000% | 79.59% | 3.9944% |
| 100% | 8.3333% | 161.30% | 5.9463% |

Ejemplo: préstamo de S/ 10,000 a 12 meses con TEA 12%:
- Antes: cuota de **S/ 888.49**, porque se aplicaba 1% mensual.
- Ahora: cuota de **S/ 885.62**, con 0.9489% mensual.

## 2. Decisión

1. **La tasa anual se interpreta como TEA por defecto** (`RateType.Efectiva`, igual que el `DEFAULT` de la base de datos):

   $$i_{mensual} = (1 + TEA)^{1/12} - 1$$

2. **Se soporta TNA de forma explícita** (`RateType.Nominal`) porque el enum de PostgreSQL `rate_type` la incluye:

   $$i_{mensual} = \frac{TNA}{12}$$

3. **Sin punto flotante.** `Math.Pow(double, double)` está prohibido en Core por los tests de arquitectura. La raíz duodécima se calcula con **Newton-Raphson en `decimal`**:
   - Iteración: $x_{k+1} = x_k - \dfrac{x_k^{12} - a}{12\,x_k^{11}}$, con $a = 1 + TEA$.
   - Punto de partida: $x_0 = 1 + TEA/12$. Por la desigualdad de Bernoulli queda por encima de la raíz, así que la convergencia es monotónica.
   - Tolerancia: $10^{-22}$, con un máximo de 100 iteraciones. En la práctica converge en menos de 10.

4. **La tasa del periodo NO se redondea.** Se usa con la precisión completa de `decimal` (28 o 29 dígitos). El redondeo half-up a 2 decimales ocurre por componente (interés, capital) al convertir a `Money`, y el residuo lo absorbe la última cuota (BE-LOA-04).

## 3. Cambios en la API

`POST /api/products/simulate` acepta un campo opcional `rateType`:

```json
{
  "amount": 10000,
  "annualRatePercentage": 12,
  "termMonths": 12,
  "method": "FRENCH",
  "rateType": "EFECTIVA"
}
```

| `rateType` | Significado | Alias aceptados |
|---|---|---|
| `EFECTIVA` (por defecto) | `annualRatePercentage` es TEA | `TEA` |
| `NOMINAL` | `annualRatePercentage` es TNA | `TNA` |

Si llega cualquier otro valor, la API responde 400 con el código `Products.InvalidRateType`.

> ⚠️ **Cambio de comportamiento:** un cliente que no envía `rateType` ahora recibe cuotas calculadas con TEA. Antes se calculaban con TNA/12. El frontend debe mostrar la etiqueta "TEA" junto a la tasa.

## 4. Pruebas

- `tests/LoanSystem.Domain.UnitTests/InterestRateTests.cs`:
  - Las tasas mensuales para TEA de 12%, 30%, 60% y 100% coinciden con un cálculo de referencia hasta 10 decimales.
  - Propiedad de ida y vuelta: $(1 + i)^{12} - 1 = TEA$, con error menor a $10^{-18}$.
  - Casos NOMINAL, tasa cero, tasa negativa y el parser.
- `FrenchAmortizationScheduleTests.cs`: TEA 12% da cuota 885.62 y TNA 12% da cuota 888.49.
- `SimulateScheduleUseCaseTests.cs`: valor por defecto EFECTIVA, opción NOMINAL y error ante un tipo de tasa inválido.

## 5. Pendientes a tener en cuenta

- [ ] **Otras periodicidades** (`payment_frequency`: DIARIA, SEMANAL, QUINCENAL). La conversión debe generalizarse a $i_p = (1+TEA)^{d/360} - 1$, donde *d* son los días del periodo, para alinearse con la convención SBS de año de 360 días. Hoy solo existe la periodicidad mensual (12 periodos por año).
- [ ] **TCEA** (Tasa de Costo Efectivo Anual): debe incluir comisiones y seguro de desgravamen. Es obligatoria en la hoja resumen.
- [ ] **Topes de tasa del BCRP:** validar la TEA contra el tope vigente. Falta la tabla `regulatory_rate_caps`.
- [ ] **Validación con Excel patrón** (BE-NF-07): comparar el cronograma céntimo a céntimo contra una planilla de referencia.
- [ ] **Persistir la tasa del periodo usada** (TEM) en el préstamo o cronograma para auditoría, con suficiente precisión. La columna `NUMERIC(12,6)` puede resultar corta para tasas diarias.
- [ ] **Nombres del método de amortización:** el parser acepta `"FRENCH"`, pero el enum de PostgreSQL usa `'FRANCES'`. Hay que alinearlos antes de persistir productos.
