# LoanSystem.Architecture.Tests

## 1. ¿Para qué sirve?
`LoanSystem.Architecture.Tests` es el **Guardián Automatizado de la Arquitectura y de la Precisión Financiera** del sistema. En un Monolito Modular con 12 módulos (24 proyectos `.Core` y `.Adapters`), confiar únicamente en revisiones manuales de código (*code reviews*) para evitar que un desarrollador acople dos módulos por error o utilice un tipo `double` en un cálculo de intereses es riesgoso.

Este proyecto utiliza **NetArchTest.Rules** y un **escáner de bytecode IL (`System.Reflection.Metadata`)** para hacer que cualquier violación arquitectónica o financiera **rompa inmediatamente el build y el pipeline de CI**.

---

## 2. ¿Qué hace?

### Responsabilidades actuales y archivos `.cs`

- **`ArchitectureTests.cs`**
  - Ejecuta 4 reglas estructurales inquebrantables sobre los ensamblados compilados de la solución:
    1. **`Core_Projects_Must_Not_Depend_On_Adapters_Or_Infrastructure`:**
       Verifica que `LoanSystem.Common.Core` y los 12 proyectos `LoanSystem.Modules.*.Core` **no tengan ninguna dependencia** hacia proyectos `*.Adapters`, `EntityFrameworkCore`, `Npgsql`, `AspNetCore`, `Quartz` o `StackExchange.Redis`. Garantiza que el núcleo hexagonal permanezca 100% puro.
    2. **`Modules_Must_Not_Reference_Other_Modules_Directly`:**
       Recorre los 12 módulos (`Administration`, `Security`, `Customers`, `Products`, `Origination`, `Loans`, `Payments`, `Treasury`, `Collections`, `Notifications`, `Integrations`, `Reports`) y verifica que ningún módulo referencie ni el `.Core` ni el `.Adapters` de otro módulo. Toda comunicación inter-modular debe ocurrir mediante contratos en `Common.Core` y eventos del Transactional Outbox.
    3. **`Module_Adapters_Should_Only_Expose_Module_Registration_As_Public`:**
       Verifica que dentro de cada `LoanSystem.Modules.<Module>.Adapters`, las clases de persistencia (`*DbContext`), repositorios, jobs y endpoints sean `internal`, exponiendo únicamente la clase estática `<Module>Module` como `public`.
    4. **`Core_And_Domain_Must_Never_Use_Float_Or_Double_In_Compiled_IL`:**
       Invoca `FloatingPointIlScanner` sobre todos los ensamblados `*.Core` para certificar que no exista ni una sola referencia a punto flotante binario.

- **`FloatingPointIlScanner.cs`**
  - Analizador de bajo nivel basado en `PEReader` y `MetadataReader` (`System.Reflection.Metadata`) que inspecciona directamente el **bytecode CIL compilado (`.dll`)** de cada ensamblado `*.Core`:
    - Revisa las firmas de todos los **campos (`FieldDefinition`)**, **propiedades**, **parámetros y retornos de métodos (`MethodDefinition`)**, **variables locales (`StandaloneSignature`)** y **referencias a miembros externos (`MemberRef`, como `Math.Pow` o `Convert.ToDouble`)**.
    - Si detecta el código de tipo primitivo `PrimitiveTypeCode.Single` (`float`) o `PrimitiveTypeCode.Double` (`double`) en cualquier parte del IL, reporta exactamente el tipo y método infractor y hace fallar el test.

### ¿Qué se agrega aquí conforme crece el sistema?
- Nuevas reglas de arquitectura (por ejemplo, exigir que todo `IUseCase` sea `internal sealed`, que todo `IIntegrationEvent` sea inmutable o que todo `ModuleDbContext` defina su esquema).

---

## 3. ¿Por qué está diseñado así?

1. **Inspección de Bytecode IL en vez de Búsqueda de Texto (`grep`):**
   Un simple análisis de texto sobre archivos `.cs` puede ser engañado por `var rate = 0.05;` (donde el compilador infiere `double` sin que la palabra `double` aparezca escrita en el código fuente) o por llamadas a `Math.Pow(...)`. Al inspeccionar el binario compilado con `MetadataReader`, `FloatingPointIlScanner` detecta con 100% de certeza cualquier variable local, parámetro o llamada que use `float` o `double`.
2. **Fronteras Modulares Preparadas para el Futuro:**
   Al prohibir referencias cruzadas entre los 12 módulos hoy, el Monolito Modular mantiene una modularidad real: si en el futuro un módulo (ej. `Notifications` o `Payments`) necesitara extraerse a un microservicio independiente, ya está completamente desacoplado a nivel de código y esquema de base de datos.
