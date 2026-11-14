# LoanSystem.Modules.Customers.Adapters

## 1. ¿Para qué sirve?
`LoanSystem.Modules.Customers.Adapters` es la **Capa de Adaptadores (Entrada y Salida)** del módulo `Customers`. Expone los 26+ endpoints REST (incluidos `/api/consents/current` y descarga local firmada `/api/customers/files/{token}`), el consumidor outbox de verificaciones KYC, el job programado de vencimiento KYC en Quartz, la persistencia EF Core/Dapper en PostgreSQL 17, los proveedores de almacenamiento (`Local` y `S3` con SigV4), el verificador de identidad `Fake` y los proveedores criptográficos HMAC/AES-GCM.

---

## 2. ¿Qué hace?

### Registro y validación de configuración
- **`CustomersModule.cs`**: registra `CustomersDbContext` (con `AddAuditLogging()` y traducción de enums vía `CustomersDbContext.EnumNameTranslator`), `CustomersOptions` + `CustomersOptionsValidator`, repositorios, lectores Ficha 360, `LocalDocumentStorage` / `S3DocumentStorage`, `FakeIdentityVerifier`, `HmacOtpHasher`, `OutboxOtpSender`, `AesGcmFieldProtector` y casos de uso; registra además los 9 `IntegrationEvent` en `IntegrationEventTypeRegistry`.
- **`CustomersOptionsValidator.cs`**: valida rangos al arranque (`ValidateOnStart`): `CoolingPeriodHours` (24–48), `ValidityMonths` / `MaxFileSizeMb` / `Otp.*`, bucket S3 y rechaza proveedor `Fake` o secretos ausentes fuera de `Development` (junto con `SecretManagerExtensions.ValidateStartupSecrets`).

### Adaptadores de Entrada (`Inbound/`)
- **`Inbound/Http/CustomersEndpoints.cs`**: alta (`Idempotency-Key`), búsqueda paginada con `unaccent ILIKE`, consulta individual, edición parcial `PATCH` (`If-Match` / `ETag`), historial auditado (`/history`) y Ficha 360 (`/overview`).
- **`Inbound/Http/KycAndConsentEndpoints.cs`**: carga multipart con límite de tamaño Kestrel, listado e URL temporal de documentos, `/api/customers/files/{token}` (anónimo con token HMAC), flujo KYC (`/kyc`, `/submit`, `/approve`, `/reject`, `/resubmit`, `/retry-checks`), catálogo público `/api/consents/current` y gestión LPDP (`/consents`, `DELETE .../MARKETING`).
- **`Inbound/Http/BankAccountAndOtpEndpoints.cs`**: registro de cuentas/billeteras/tarjetas tokenizadas (`Idempotency-Key`), listado seguro, verificación de titularidad, desactivación, designación de primaria (`PUT .../primary`), confirmación con OTP (`POST .../primary/confirm` + `Idempotency-Key`) y verificación OTP de email/teléfono (`/contact/verify/start|confirm`).
- **`Inbound/Http/RequireCustomerOwnershipFilter.cs`**: filtro reutilizable para aislar `ROLE_CLIENT` al `customer_id` de su token JWT (responde `404` ante intentos sobre otros clientes) y helpers `If-Match`.
- **`Inbound/Events/RunKycChecksHandler.cs`**: consumidor outbox de `CustomerKycSubmittedIntegrationEvent` que ejecuta las verificaciones automáticas vía `IRunKycChecksUseCase`.
- **`Inbound/Jobs/ExpireKycJob.cs`**: job Quartz diario (`02:00 America/Lima`, `[DisallowConcurrentExecution]`, `CustomersJobs.AddCustomersJobs`) que ejecuta `IExpireKycUseCase` en lotes de 500.

### Adaptadores de Salida (`Outbound/`)
- **`Outbound/Persistence/CustomersDbContext.cs` y `Configurations/CustomersEntityConfigurations.cs`**: mapeo EF Core de las 7 entidades (`customers`, `customer_documents`, `kyc_verifications`, `consent_texts`, `customer_consents`, `otp_verifications`, `customer_bank_accounts`), traducción de enums PG `public` y conversión de violación `23505` a `DuplicateRecordException`.
- **`Outbound/Persistence/CustomerRepository.cs` y `CustomerSubRepositories.cs`**: repositorios EF Core para agregado `Customer`, documentos, verificaciones KYC, consentimientos, OTP y cuentas bancarias.
- **`Outbound/Persistence/CustomerReadersAndBatchStore.cs` y `CatalogAdapters.cs`**: consultas Dapper de historial (`audit_logs` con diff de columnas críticas), lector paralelo Ficha 360 (`Task.WhenAll` sobre `ILoanPortfolioQuery` y `IApplicationHistoryQuery`), almacén por lotes para expiración KYC, catálogo de documentos (`document_type_rules` con caché) y catálogo estático de bancos peruanos (`IBanksCatalog`).
- **`Outbound/Providers/LocalDocumentStorage.cs` y `S3DocumentStorage.cs`**: almacenamiento local firmado por HMAC con expiración (solo dev/test) y cliente S3/MinIO/R2 con firma AWS SigV4, SSE-S3 (`AES256`) y URLs presignadas GET.
- **`Outbound/Providers/FakeIdentityVerifier.cs` y `CustomersInfraProviders.cs`**: proveedor determinista de pruebas (`0`→FAIL, `9`→PEP/REVIEW, resto→PASS), hasher OTP HMAC-SHA256 en tiempo constante, publicador outbox sensible (`OutboxOtpSender`), adaptadores de auditoría explícita (`audit_logs`), diagnóstico y `ICustomerPortalLookup`.

---

## 3. ¿Por qué está diseñado así?
1. **Protección estricta de PII y documentos:**
   Los adaptadores nunca exponen CCI completo, números de tarjeta (PAN), tokens OTP ni archivos en claro por URLs públicas permanentes; toda descarga pasa por URL firmada temporal (`VIEW_DOCUMENT` auditado).
2. **Encapsulamiento `internal sealed`:**
   Los adaptadores HTTP, DbContext y proveedores son internos al ensamblado (expuestos a pruebas solo vía `InternalsVisibleTo`), asegurando por `NetArchTest` que ningún otro módulo acople con sus tablas ni modelos internos.
3. **Concurrencia y anti-enumeración:**
   Indices únicos parciales (`ux_customer_bank_accounts_primary`, `ux_customer_documents_current`), tokens `If-Match`/`version` y respuestas `404` consistentes para aislamiento `ROLE_CLIENT` previenen carreras y fugas de existencia.
