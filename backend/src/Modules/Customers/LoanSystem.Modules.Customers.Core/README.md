# LoanSystem.Modules.Customers.Core

## 1. ¿Para qué sirve?
`LoanSystem.Modules.Customers.Core` es el **Núcleo de Dominio y Aplicación del Módulo de Clientes y KYC (`Customers`)**. Dentro de nuestra Arquitectura Hexagonal, este proyecto modela toda la información maestra del prestatario: datos personales/demográficos, validación de documentos de identidad (DNI, Carnet de Extranjería, RUC), direcciones domiciliarias/laborales, cuentas bancarias para desembolso, expediente digital **KYC (*Know Your Customer*)** y su historial de evaluación crediticia (*scoring* / clasificación de riesgo).

---

## 2. ¿Qué hace?

### Responsabilidades actuales y archivos `.cs`

- **`Ports/Outbound/ICustomersUnitOfWork.cs`**
  - Puerto de salida que extiende `IUnitOfWork` para confirmar transaccionalmente el alta o actualización de clientes, la validación de sus documentos KYC y el registro de sus cuentas bancarias.

- **`Ports/Outbound/ICustomersOutbox.cs`**
  - Puerto de salida que extiende `IOutboxRepository` para publicar eventos de integración hacia otros módulos (por ejemplo, `CustomerKycVerifiedIntegrationEvent` o `CustomerRiskProfileUpdatedIntegrationEvent` para que `Origination` habilite el flujo de solicitud de crédito).

### ¿Qué se agrega aquí conforme crece el módulo?
- **Entidades y Objetos de Valor (`Domain/`):**
  - `Customer` (Aggregate Root con estado de verificación KYC, ingresos declarados/verificados y segmento), `IdentityDocument` (Value Object que valida longitud y formato de `DNI` de 8 dígitos, `CE` y `RUC` de 11 dígitos), `CustomerAddress`, `CustomerBankAccount` (CCI de 20 dígitos y número de cuenta para desembolsos), `KycDocument` y `CreditAssessment`.
- **Puertos de Entrada (`Ports/Inbound/`) y Casos de Uso (`Application/`):**
  - `IRegisterCustomerUseCase`, `IUpdateCustomerKycStatusUseCase`, `IAddCustomerBankAccountUseCase`, `IRecordCreditAssessmentUseCase`, `IGetCustomerProfileUseCase`.
- **Puertos de Salida (`Ports/Outbound/`):**
  - `ICustomerRepository` para persistencia y consultas de unicidad por tipo/número de documento.

---

## 3. ¿Por qué está diseñado así?

1. **Separación entre Identidad de Seguridad (`Security`) y Perfil de Cliente (`Customers`):**
   Un usuario del sistema (`security.users`) representa credenciales de autenticación y roles RBAC (`CUSTOMER`, `ANALYST`, etc.), mientras que un cliente (`customers.customers`) es una entidad legal y financiera con expediente KYC, ingresos mensuales (`Money` en `decimal`), cuentas bancarias CCI y perfil de riesgo crediticio. Separarlos en módulos distintos respeta el principio de ** Contextos Delimitados (*Bounded Contexts*)** de DDD.
2. **Aritmética `decimal` para Ingresos y Capacidad de Endeudamiento:**
   Los ingresos mensuales, gastos declarados y límites de endeudamiento del cliente en `Customers.Core` utilizan el Value Object `Money` (`decimal`), cumpliendo con la prohibición total de `float`/`double`.
3. **Manejo Explícito de Errores con `Result<T>`:**
   Situaciones frecuentes del negocio (como intentar registrar un DNI duplicado → `Error.Conflict`, o presentar un CCI inválido → `Error.Validation`) se modelan con `Result<T>` en vez de excepciones.
