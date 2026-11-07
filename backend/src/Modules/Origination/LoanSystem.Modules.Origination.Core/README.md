# LoanSystem.Modules.Origination.Core

## 1. ¿Para qué sirve?
`LoanSystem.Modules.Origination.Core` es el **Núcleo de Dominio y Aplicación del Módulo de Originación y Evaluación Crediticia (`Origination`)**. Dentro de nuestra Arquitectura Hexagonal, este proyecto modela todo el flujo de admisión de una solicitud de préstamo (*Loan Application Workflow*) antes de convertirse en un crédito activo: desde el ingreso de la solicitud, la validación de requisitos KYC y producto, la evaluación financiera por parte del analista (`ANALYST`), hasta la decisión final del comité o aprobador (`APPROVER`).

---

## 2. ¿Qué hace?

### Responsabilidades actuales y archivos `.cs`

- **`Ports/Outbound/IOriginationUnitOfWork.cs`**
  - Puerto de salida que hereda de `IUnitOfWork` para persistir de manera atómica las solicitudes de préstamo, sus evaluaciones de riesgo y las transiciones de estado en la máquina de estados del workflow.

- **`Ports/Outbound/IOriginationOutbox.cs`**
  - Puerto de salida que hereda de `IOutboxRepository` para publicar eventos de integración cuando una solicitud cambia de estado (especialmente `LoanApplicationApprovedIntegrationEvent` para que el módulo `Loans` genere el contrato y habilite el desembolso, y `LoanApplicationRejectedIntegrationEvent` para que `Notifications` informe al cliente).

### ¿Qué se agrega aquí conforme crece el módulo?
- **Entidades y Máquina de Estados (`Domain/`):**
  - `LoanApplication` (Aggregate Root con monto solicitado en `Money`, plazo en meses, tasa propuesta, destino del crédito y máquina de estados estricta: `Draft` → `Submitted` → `InReview` → `PendingApproval` → `Approved` / `Rejected` / `Cancelled`).
  - `ApplicationEvaluation` (dictamen técnico del analista con cálculo de ratio cuota/ingreso, capacidad de pago en `Money` y recomendaciones).
  - `ApplicationApprovalDecision` (registro de firma de aprobación o rechazo por usuario con rol `APPROVER`, garantizando segregación de funciones: quien evalúa no puede aprobar su propia solicitud).
- **Puertos de Entrada (`Ports/Inbound/`) y Casos de Uso (`Application/`):**
  - `ISubmitLoanApplicationUseCase`, `IAssignAnalystUseCase`, `IRecordEvaluationUseCase`, `IApproveLoanApplicationUseCase`, `IRejectLoanApplicationUseCase`.

---

## 3. ¿Por qué está diseñado así?

1. **Segregación de Funciones (*Four-Eyes Principle* / Maker-Checker):**
   En banca y microfinanzas, el analista (`ANALYST`) que estructura y evalúa una solicitud de crédito nunca debe ser el mismo actor que la aprueba (`APPROVER`). Modelar `Origination.Core` como un dominio dedicado permite imponer esta invariante de negocio directamente en el Aggregate Root `LoanApplication` devolviendo `Error.Forbidden` o `Error.Validation` a través de `Result<T>`.
2. **Transiciones de Estado Deterministas sin Excepciones:**
   Intentar aprobar una solicitud que aún está en borrador (`Draft`) o que ya fue rechazada (`Rejected`) retorna un `Result.Failure` con `ErrorType.Conflict`, que el adaptador HTTP traduce limpiamente a `409 Conflict` (`ProblemDetails`).
