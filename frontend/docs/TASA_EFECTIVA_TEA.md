# La tasa es TEA (efectiva), no nominal: guía para el frontend

| Campo | Valor |
|---|---|
| Fecha | 2026-11-10 |
| Origen | Corrección en el backend, ver `backend/docs/DECISION_01_TASA_EFECTIVA_TEA.md` |
| Afecta a | portal (simulador público), mobile (FE-CP-06), backoffice (catálogo de productos) |
| Paquete | `@prestamos/api-client` (`packages/api-client/src/contracts.ts`) |

## 1. Qué cambió

El backend calculaba la tasa mensual como `tasa anual / 12`, que es la fórmula de una tasa **nominal** (TNA). En el Perú los créditos se anuncian con **TEA (Tasa Efectiva Anual)**, así que el interés cobrado era mayor que la tasa anunciada.

Ahora la tasa anual se interpreta como **TEA por defecto**, y la tasa mensual (TEM) se obtiene así:

```
TEM = (1 + TEA)^(1/12) − 1
```

| TEA | TEM correcta (actual) | TEM anterior (TEA/12) |
|---|---|---|
| 12% | 0.9489% | 1.0000% |
| 30% | 2.2104% | 2.5000% |
| 60% | 3.9944% | 5.0000% |
| 100% | 5.9463% | 8.3333% |

**Ejemplo:** S/ 10,000 a 12 meses con TEA 12% da una cuota de **S/ 885.62**. Antes daba S/ 888.49.

> Si alguna pantalla, mock o test del frontend tiene cuotas "esperadas" calculadas con TEA/12, hay que actualizarlas.

## 2. Reglas para el frontend

1. **El frontend NO calcula cuotas ni tasas.** Todo cálculo financiero lo hace el backend; el frontend solo formatea y muestra (`formatMoney` de `@prestamos/utils`). Así se evitan diferencias de céntimos y errores de punto flotante en JS.
2. **Etiquetar siempre la tasa como "TEA".** Nunca "tasa anual" a secas ni "interés anual". Cuando exista, mostrar también la **TCEA** (exigencia de transparencia SBS).
3. **Si se muestra la tasa mensual, rotularla "TEM"** y tomarla del backend (`summary.tem`). No calcularla como TEA/12 en el cliente.
4. **Cuidado con el formato de la tasa**, porque hoy conviven dos:

   | Lugar | Formato | Ejemplo para 30% |
   |---|---|---|
   | `LoanProduct.annualRate` (catálogo, base de datos) | decimal como string | `"0.300000"` |
   | `POST /api/products/simulate` → `annualRatePercentage` | porcentaje numérico | `30` |

   Para mostrar `"0.300000"` como porcentaje hay que multiplicar por 100 **solo para formatear**, por ejemplo `30.00 %`, sin hacer aritmética de negocio con ese número.

## 3. Endpoint disponible hoy

`POST /api/products/simulate` es anónimo y es el único simulador implementado en el backend.

```json
// Request
{
  "amount": 10000,
  "annualRatePercentage": 12,
  "termMonths": 12,
  "method": "FRENCH",
  "rateType": "EFECTIVA"
}
```

| Campo | Tipo | Obligatorio | Notas |
|---|---|---|---|
| `amount` | number | sí | Mayor a 0 |
| `annualRatePercentage` | number | sí | En **porcentaje** (12 = 12%); no puede ser negativo |
| `termMonths` | number | sí | Mayor a 0; la frecuencia es solo mensual por ahora |
| `method` | string | no | Por defecto `"FRENCH"`, el único soportado |
| `rateType` | string | no | Por defecto `"EFECTIVA"` (TEA). Acepta `"NOMINAL"` (TNA) y los alias `"TEA"`/`"TNA"` |

```json
// Response 200
{
  "amount": 10000,
  "totalInterest": "…",
  "totalPayment": "…",
  "installments": [
    { "number": 1, "principal": 790.73, "interest": 94.89, "total": 885.62, "remainingBalance": 9209.27 }
    // … 11 cuotas más; la última absorbe el residuo de céntimos
  ]
}
```

(En la respuesta real, `totalInterest` y `totalPayment` también son números.)

**Errores 400 (ProblemDetails):**

| Código | Causa |
|---|---|
| `Products.InvalidAmount` | El monto es menor o igual a 0 |
| `Products.InvalidTerm` | El plazo es menor o igual a 0 |
| `Products.InvalidRate` | La tasa es negativa |
| `Products.InvalidMethod` | El método no está soportado |
| `Products.InvalidRateType` | `rateType` no es `EFECTIVA` ni `NOMINAL` |

## 4. Diferencias con `contracts.ts` (pendiente de alinear)

El contrato tipado del frontend (`SimulateRequest` / `Simulation`) describe el simulador **objetivo** (`/api/loan-applications/simulate`), que aún **no existe** en el backend:

| Aspecto | Frontend (`contracts.ts`) | Backend actual |
|---|---|---|
| Ruta | `/api/client/loan-applications/simulate` | `/api/products/simulate` |
| Tasa | se toma del producto (`productId`) | se envía en `annualRatePercentage` |
| Plazo | `term` + `frequency` | `termMonths` (solo mensual) |
| Montos | `Money` = string (`"1250.50"`) | number |
| Método | `'FRANCES'` | `"FRENCH"` |
| Resumen | `tea`, `tem`, `tcea`, `totalFees`, `installmentAmount` | solo `totalInterest`, `totalPayment` |
| Cuota | `dueDate`, `fee`, `totalDue`, `balance` | sin fecha ni comisiones; `total`, `remainingBalance` |

**Recomendación:** no adaptar `contracts.ts` al endpoint temporal. Si una pantalla necesita el simulador ya, conviene crear un adaptador aislado en la feature correspondiente, y mantener `contracts.ts` como el contrato objetivo hasta que el backend implemente BE-ORI-03 completo (TEM, TCEA, fechas y comisiones).

## 5. Pendientes del backend que impactan al frontend

- [ ] Simulador definitivo por producto (`productId`), con `tea`, `tem` y `tcea` en el resumen.
- [ ] Otras frecuencias (`DIARIA`, `SEMANAL`, `QUINCENAL`). Hoy solo hay mensual.
- [ ] Fechas de vencimiento con ajuste por feriados (`dueDate`).
- [ ] Alinear el nombre del método (`FRENCH` en el backend, `FRANCES` en la base de datos y el frontend).
- [ ] Montos como string decimal en las respuestas, para coincidir con `Money` del frontend.
