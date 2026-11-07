# LoanSystem.Application.UnitTests

## 1. ¿Para qué sirve?
`LoanSystem.Application.UnitTests` es la **Suite de Pruebas Unitarias de la Capa de Aplicación (Casos de Uso)**. Dentro de nuestra Arquitectura Hexagonal, este proyecto verifica la orquestación de los casos de uso (`IUseCase`), las validaciones de entrada, el manejo funcional de errores mediante el patrón **`Result<T>` / `Error`** y la interacción con los puertos de salida (`IClock`, `IUnitOfWork`, `IOutboxRepository`) sin tocar bases de datos reales ni servidores HTTP.

---

## 2. ¿Qué hace?

### Responsabilidades actuales y archivos `.cs`

- **`ResultTests.cs`**
  - Verifica el comportamiento de las primitivas de control de flujo de `LoanSystem.Common.Core`:
    - Creación correcta de `Result.Success()`, `Result.Success<T>(value)`, `Result.Failure(error)` y `Result.Failure<T>(error)`.
    - Garantía de que un `Result` exitoso siempre tenga `Error.None` y `IsSuccess == true`, mientras que un `Result` fallido exponga el `ErrorType` semántico correspondiente (`Validation`, `NotFound`, `Conflict`, `Unauthorized`, `Forbidden`).
    - Protección contra el acceso a `.Value` cuando `IsFailure == true`.

- **`SimulateScheduleUseCaseTests.cs`**
  - Prueba el caso de uso `SimulateScheduleUseCase` (`LoanSystem.Modules.Products.Core`):
    - Verifica que ante un `SimulateScheduleRequest` válido retorne `Result.IsSuccess == true` con el desglose completo de cuotas y totales cuadrados.
    - Comprueba que ante entradas inválidas (monto principal $\le 0$, tasa negativa, plazo $\le 0$ meses) **no lance excepciones**, sino que devuelva `Result.IsFailure == true` con `ErrorType.Validation` y el código de error específico del dominio.

### ¿Qué se agrega aquí conforme crece el sistema?
- Pruebas unitarias para todos los casos de uso de los 12 módulos (`CalculateDailyMoraUseCaseTests`, `RunDailyBalanceUseCaseTests`, `SendDailyRemindersUseCaseTests`, originación, desembolsos, aplicación de pagos, etc.) inyectando relojes deterministas (`FakeClock : IClock`) y dobles de prueba de los puertos de salida.

---

## 3. ¿Por qué está diseñado así?

1. **Acceso Directo a Casos de Uso `internal` vía `InternalsVisibleTo`:**
   Los casos de uso concretos (como `SimulateScheduleUseCase`) son `internal sealed` dentro de sus respectivos proyectos `*.Core` para que ningún otro módulo de negocio pueda instanciarlos directamente. Gracias a la configuración de `InternalsVisibleTo` hacia `LoanSystem.Application.UnitTests`, este proyecto puede probar cada caso de uso directamente sin romper el encapsulamiento de producción.
2. **Validación del Contrato `Result<T>` sin Excepciones:**
   Estas pruebas aseguran que ningún caso de uso utilice excepciones para errores de negocio, garantizando que `ProblemDetailsResultMapper` siempre reciba un `Error` tipado.
