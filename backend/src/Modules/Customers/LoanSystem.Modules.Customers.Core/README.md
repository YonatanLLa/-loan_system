# LoanSystem.Modules.Customers.Core

## 1. ¿Para qué sirve?
`LoanSystem.Modules.Customers.Core` es el **Núcleo de Dominio y Aplicación del Módulo de Clientes, KYC y Cuentas (`Customers`)**. Modela las personas naturales y jurídicas, la validación documental peruana (`DNI`, `CE`, `RUC` con dígito verificador módulo 11 y `PASAPORTE`), su ciclo de vida **KYC**, las verificaciones automáticas vía `IIdentityVerifier`, los consentimientos LPDP por finalidad y versión (Ley N° 29733), las cuentas bancarias/billeteras de desembolso cifradas con periodo de enfriamiento (*cooling period*), los códigos OTP por propósito y la consulta consolidada Ficha 360.

---

## 2. ¿Qué hace?

### Contratos públicos (`Contracts/`)
- **`CustomerContracts.cs`**: DTOs y comandos de perfil, registro (`RegisterCustomerRequest`), búsqueda (`SearchCustomersRequest`), actualización parcial con `ExpectedVersion` (`UpdateCustomerRequest`), historial (`CustomerChangeEntry`, `FieldChange`), Ficha 360 (`CustomerOverviewResponse`, `ConsolidatedDebt`) y `ICustomerPortalLookup`.
- **`KycContracts.cs`**: DTOs de documentos (`UploadCustomerDocumentRequest`, `CustomerDocumentResponse`, `DocumentSignedUrlResponse`, `LocalDocumentContent`), verificación KYC (`ApproveKycRequest`, `RejectKycRequest`, `KycVerificationResponse`, `CustomerKycStatusResponse`) y comandos internos.
- **`ConsentContracts.cs` y `ICustomerConsentQuery.cs`**: catálogo público `CurrentConsentTextResponse`, aceptación por versión `ConsentAcceptance`, historial `CustomerConsentResponse` y contrato entre módulos `ICustomerConsentQuery` (`HasActiveAsync`).
- **`BankAccountContracts.cs` e `ICustomerDisbursementEligibility.cs`**: DTOs de cuentas (`RegisterBankAccountRequest`, `VerifyBankAccountRequest`, `BankAccountResponse` sin exponer CCI/hash, `SetPrimaryBankAccountResult`, `ConfirmPrimaryBankAccountRequest`) y contrato `ICustomerDisbursementEligibility` (`DisbursementEligibility` / `DisbursementEligibilityReasons`).
- **`OtpContracts.cs`**: contrato público `ICustomerOtpService` (`IssueAsync`, `VerifyAsync`), `OtpIssued` y solicitudes de verificación de contacto (`StartContactVerificationRequest`, `ConfirmContactVerificationRequest`).
- **`IntegrationEvents/CustomerIntegrationEvents.cs`**: `CustomerRegisteredIntegrationEvent`, `CustomerKycSubmittedIntegrationEvent`, `CustomerKycVerifiedIntegrationEvent`, `CustomerKycRejectedIntegrationEvent`, `CustomerKycExpiredIntegrationEvent`, `CustomerKycExpiringSoonIntegrationEvent`, `CustomerDisbursementAccountChangedIntegrationEvent`, `CustomerMarketingConsentRevokedIntegrationEvent` y `OtpDeliveryRequestedIntegrationEvent`.

### Dominio (`Domain/` y `Domain/Validators/`)
- **`CustomersEnums.cs` y `CustomerErrors.cs`**: enumeraciones alineadas a PostgreSQL (`PersonType`, `DocumentType`, `KycStatus`, `AmlRiskLevel`, `KycCheckType`, `KycCheckResult`, `DocumentCategory`, `ConsentPurpose`, `OtpChannel`, `OtpPurpose`, `BankAccountType`, `BankAccountStatus`) y catálogo de errores `Customers.*`.
- **`Customer.cs`**: raíz de agregado con fábricas `RegisterNatural` (edad mínima 18 años por `LimaToday`) y `RegisterLegalEntity` (`RUC` `20...`), edición sin mutar documento (`UpdateContact`, `UpdateAddress`, `UpdateSocioeconomicProfile`), máquina de estados KYC (`SubmitKyc`, `ApproveKyc`, `RejectKyc`, `ResubmitKyc`), marcado PEP/AML y verificación de correo/teléfono.
- **`IdentityDocument.cs` y `DocumentTypeRule.cs`**: normalización y validación contra catálogo `document_types` y módulo 11 para `RUC`.
- **`PersonName.cs`, `ContactInfo.cs`, `SocioeconomicProfile.cs`**: Value Objects para nombre normalizado, correo/teléfono (`^9\d{8}$` admitiendo `+51`) e ingresos en `Money` (`PEN`, `decimal`).
- **`CustomerDocument.cs`, `KycVerification.cs`, `ConsentText.cs`, `CustomerConsent.cs`**: versionado lógico de documentos (`IsCurrent`, `MarkReplaced`), resultados automáticos de identidad/biometría/listas PEP-AML, y textos/consentimientos auditables.
- **`CustomerBankAccount.cs`**: cuentas `CUENTA_AHORRO`, `CUENTA_CORRIENTE`, `BILLETERA_DIGITAL` y `TARJETA_DEBITO` (solo `gatewayToken`, rechaza PAN), verificación de titularidad normalizada, promoción directa (primera cuenta) o con *cooling period*, y desactivación lógica.
- **`OtpCode.cs`**: generación de hash, expiración, máximo de intentos (bloqueo en 5.º fallo), vinculación estricta por `Purpose` + `ReferenceId` e invalidación.
- **`Validators/CciValidator.cs`, `FileSignatureSniffer.cs`, `PiiMasker.cs`**: validación de CCI de 20 dígitos contra `IBanksCatalog`, detección por *magic bytes* (`JPEG`, `PNG`, `WEBP`, `PDF`) y enmascarado PII según permisos (RNF-05).

### Puertos (`Ports/`) y Aplicación (`Application/`)
- **`Ports/Inbound/ICustomersUseCases.cs`**: interfaces públicas para todos los casos de uso HTTP, KYC (`IRunKycChecksUseCase`, `IExpireKycUseCase`) y archivos locales (`IDownloadLocalDocumentUseCase`).
- **`Ports/Outbound/` (`ICustomerRepository`, `ICustomersUnitOfWork`, `ICustomersOutbox`, `IDocumentStorage`, `IDocumentTypeCatalog`, `IIdentityVerifier`, `ICustomerReaders`, `CustomerInfraPorts`, `CustomerOutboundPorts`)**: repositorios, lectura Ficha 360/auditoría, catálogo de bancos `IBanksCatalog`, `IOtpHasher`, `IOtpSender`, `IFieldProtector`, `ICustomerAuditWriter` y `ICustomerDiagnostics`.
- **`Application/`**: `CustomersOptions`, `CustomerMapper`, `CustomerProfileUseCases`, `DocumentUseCases`, `KycUseCases` (`RunKycChecksUseCase`, `ApproveKycUseCase`, `RejectKycUseCase`, `ExpireKycUseCase`, etc.), `ConsentUseCases`, `BankAccountUseCases` (`CustomerDisbursementEligibilityService`, etc.), `OtpUseCases` (`CustomerOtpService`, verificación de contacto) y `OverviewUseCase` (`GetCustomerOverviewUseCase`).

---

## 3. ¿Por qué está diseñado así?
1. **Aislamiento entre Seguridad (`Security`) y Clientes (`Customers`):**
   Las credenciales y roles residen en `Security`, mientras que `Customers` gestiona identidad civil, expediente KYC y cuentas de desembolso. La única integración de portal es `ICustomerPortalLookup`, que aporta el claim `customer_id` al JWT de `ROLE_CLIENT`.
2. **Aritmética `decimal` (`Money`) y cero datos sensibles en claro (RNF-05):**
   Ingresos, saldos y deudas consolidadas operan en `Money` sin `float`/`double`. Los CCI se protegen con cifrado autenticado + HMAC, y el enmascarado depende de `customers.pii.unmasked`.
3. **Resiliencia mediante Outbox y puertos puros:**
   Las verificaciones KYC externas (`RunKycChecksHandler`), alertas antifraude y envíos OTP (`OtpDeliveryRequestedIntegrationEvent`) se desacoplan vía outbox transaccional dentro del núcleo sin dependencias de infraestructura.
