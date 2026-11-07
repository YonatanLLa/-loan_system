# LoanSystem.Common.Core

## 1. ¿Para qué sirve?
`LoanSystem.Common.Core` es el **Kernel Compartido de Dominio y Aplicación** (*Shared Kernel*) de todo el sistema de préstamos. Dentro de nuestra **Arquitectura Hexagonal (Puertos y Adaptadores)** combinada con un **Monolito Modular**, este proyecto provee las primitivas fundamentales, abstracciones base y contratos transversales de los que dependen exclusivamente los proyectos `*.Core` de los 12 módulos de negocio.

Su propósito es estandarizar cómo todos los módulos representan dinero (`Money`), entidades (`Entity<TId>`), objetos de valor (`ValueObject`), resultados de casos de uso (`Result<T>`, `Error`), eventos de dominio/integración y puertos comunes de entrada y salida, **sin introducir ninguna dependencia hacia frameworks de infraestructura, bases de datos, HTTP o librerías externas**.

---

## 2. ¿Qué hace?

### Responsabilidades actuales y archivos `.cs`

- **`Domain/Money.cs`**
  - Define el `readonly record struct Money`, la primitiva financiera más crítica del sistema.
  - Almacena montos exclusivamente en `decimal` junto con el código de moneda ISO (`Currency`, por defecto `"PEN"` - Soles Peruanos).
  - Expone dos niveles de precisión matemática:
    - **`RoundCurrency` (2 decimales, `MidpointRounding.AwayFromZero`)**: Para montos finales cobrables, saldos de cuotas y asientos contables.
    - **`RoundCalculation` (4 decimales, `MidpointRounding.AwayFromZero`)**: Para cálculos financieros intermedios (tasas diarias, factores de capitalización, intereses devengados antes del cierre de cuota).
  - Valida invariantes estrictas al sumar, restar o comparar: impide operar entre monedas distintas y ofrece helpers seguros (`Zero`, `FromRounded`, `Allocate`, `IsNegative`, `IsPositive`).

- **`Domain/Entity.cs`**
  - Clase base abstracta `Entity<TId>` para todas las entidades de dominio y raíces de agregado (*Aggregate Roots*).
  - Define igualdad basada en identidad (`Id`) y mantiene una lista interna encapsulada de eventos de dominio (`IReadOnlyCollection<IDomainEvent> DomainEvents`) con métodos `RaiseDomainEvent` y `ClearDomainEvents` para que la capa de aplicación o persistencia los despache transaccionalmente.

- **`Domain/ValueObject.cs`**
  - Clase base para objetos de valor complejos que requieren igualdad estructural por componentes (`GetEqualityComponents()`) cuando un `readonly record struct` no es suficiente.

- **`Domain/IDomainEvent.cs`**
  - Interfaz marcadora para eventos ocurridos dentro de los límites de un mismo agregado/módulo (`EventId`, `OccurredOnUtc`).

- **`Application/Error.cs`**
  - Define el `sealed record Error(string Code, string Message, ErrorType Type)` y el enumerado `ErrorType` (`Failure`, `Validation`, `NotFound`, `Conflict`, `Unauthorized`, `Forbidden`).
  - Proporciona fábricas estáticas semánticas (`Error.Validation(...)`, `Error.NotFound(...)`, `Error.Conflict(...)`, etc.) que permiten mapear errores de negocio de manera determinista a respuestas HTTP `ProblemDetails` (RFC 7807) en la capa de adaptadores sin acoplar el dominio a códigos de estado HTTP.

- **`Application/Result.cs`**
  - Implementa el patrón `Result` y `Result<TValue>` para el manejo funcional de errores (*Railway-Oriented Programming*).
  - Garantiza que un caso de uso devuelva explícitamente éxito (`Result.Success(value)`) o fallo (`Result.Failure<T>(error)`), eliminando el uso de excepciones (`throw new ...`) para controlar el flujo de reglas de negocio.

- **`Contracts/IIntegrationEvent.cs` y `Contracts/IIntegrationEventHandler.cs`**
  - Contratos para la comunicación asíncrona entre módulos mediante el patrón **Transactional Outbox**.
  - `IIntegrationEvent` exige `EventId` (`Guid`) y `OccurredOnUtc` (`DateTimeOffset`).
  - `IIntegrationEventHandler<TEvent>` define el consumidor desacoplado que procesa eventos publicados por otros módulos.

- **`Ports/Inbound/IUseCase.cs`**
  - Define las interfaces estándar para puertos de entrada (casos de uso): `IUseCase<in TRequest, TResponse>` y `IUseCase<TResponse>`, retornando siempre `Task<Result<TResponse>>` y aceptando `CancellationToken`.

- **`Ports/Outbound/OutboundPorts.cs`**
  - Agrupa los puertos de salida transversales que cualquier caso de uso puede necesitar inyectar:
    - `IClock`: Abstracción del reloj del sistema (`UtcNow`, `LimaNow`, `LimaToday`) para que la lógica financiera y el cálculo de mora a medianoche hora Lima (`America/Lima`) sean 100% testeables y deterministas.
    - `ICurrentUser`: Abstracción del usuario autenticado (`UserId`, `Username`, `Roles`, `IpAddress`) usada para trazabilidad y auditoría.
    - `IUnitOfWork` e `ITransactionScope`: Contratos para confirmar cambios y manejar transacciones atómicas.
    - `IOutboxRepository`: Puerto de salida para encolar `IIntegrationEvent` dentro de la misma transacción de base de datos del módulo emisor.

### ¿Qué se agrega aquí conforme crece el sistema?
- Nuevos `ValueObject` verdaderamente universales (por ejemplo, `TaxId`/`DocumentoIdentidad` si aplica a todos los módulos, o `PercentageRate` con escala `NUMERIC(12,6)`).
- Primitivas de paginación o especificación (`PagedResult<T>`, `PageRequest`) compartidas por los casos de uso de consulta.
- **Nunca** se deben agregar aquí entidades específicas de un módulo (como `Loan`, `Customer` o `Payment`) ni paquetes NuGet de infraestructura (`EntityFrameworkCore`, `Npgsql`, `AspNetCore`).

---

## 3. ¿Por qué está diseñado así?

1. **Pureza del Dominio y Cero Dependencias de Infraestructura:**
   `LoanSystem.Common.Core` no referencia ningún paquete de base de datos, ORM ni framework web. Esto garantiza que las reglas financieras y los contratos base jamás se contaminen con detalles técnicos.
2. **Prohibición estricta de `float` y `double` (verificada en IL):**
   En un sistema de préstamos, el tipo coma flotante binario IEEE 754 (`float`/`double`) produce errores de redondeo inaceptables (ej. `0.1 + 0.2 != 0.3`). Por diseño, `Money` encapsula `decimal` y exige redondeo explícito a 2 o 4 decimales con `MidpointRounding.AwayFromZero`. Además, el proyecto `LoanSystem.Architecture.Tests` escanea el bytecode IL compilado para bloquear cualquier instrucción `float`/`double`.
3. **Patrón `Result<T>` en lugar de Excepciones para Flujo de Negocio:**
   Las excepciones en .NET son costosas en rendimiento y actúan como `GOTO` invisibles en la firma de los métodos. Con `Result<T>` y `Error`, el compilador obliga a quien invoca un caso de uso a manejar tanto el escenario exitoso como los errores de validación, conflicto o no encontrado.
4. **Independencia Temporal (`IClock` con soporte nativo para `America/Lima`):**
   Las reglas crediticias en Perú dependen de la fecha contable local (`America/Lima`, UTC-5), como el corte de mora a las 00:05. Abstraer el tiempo en `IClock` evita llamadas directas a `DateTime.Now` o `DateTime.UtcNow`, permitiendo simular pasos del tiempo exactos en pruebas unitarias.
