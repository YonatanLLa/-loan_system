# LoanSystem.Modules.Integrations.Core

## 1. ¿Para qué sirve?
`LoanSystem.Modules.Integrations.Core` es el **Núcleo de Dominio y Aplicación del Módulo de Integraciones Externas y Pasarelas (`Integrations`)**. Dentro de nuestra Arquitectura Hexagonal, este proyecto actúa como la **Capa Anticorrupción (*Anti-Corruption Layer - ACL*)** frente a proveedores externos: pasarelas de pago y recaudación, bancos para transferencias de desembolso, centrales de riesgo crediticio (bureaus/SBS), validación de identidad (RENIEC) y recepción de webhooks externos.

---

## 2. ¿Qué hace?

### Responsabilidades actuales y archivos `.cs`

- **`Ports/Outbound/IIntegrationsUnitOfWork.cs`**
  - Puerto de salida que hereda de `IUnitOfWork` para persistir de forma atómica los registros de llamadas salientes (`integration_logs`), los webhooks entrantes (`webhook_events`) y las partidas de conciliación con terceros.

- **`Ports/Outbound/IIntegrationsOutbox.cs`**
  - Puerto de salida que hereda de `IOutboxRepository` para publicar eventos de integración internos una vez que un webhook externo o una respuesta de pasarela ha sido validada y normalizada al lenguaje ubicuo de nuestro dominio.

### ¿Qué se agrega aquí conforme crece el módulo?
- **Entidades y Objetos de Valor (`Domain/`):**
  - `IntegrationLog` (proveedor, operación, payload sanitizado, código de respuesta, latencia, estado), `WebhookEvent` (identificador externo único para idempotencia de webhooks, firma criptográfica HMAC verificada, estado de procesamiento) y `ExternalReconciliationDiscrepancy`.
- **Puertos de Entrada (`Ports/Inbound/`) y Casos de Uso (`Application/`):**
  - `IProcessIncomingWebhookUseCase` (recepción idempotente de confirmaciones de pago o desembolso bancario), `IQueryCreditBureauUseCase`, `IExecuteBankTransferUseCase`.
- **Puertos de Salida hacia Terceros (`Ports/Outbound/`):**
  - Interfaces puras (`IPaymentGatewayClient`, `ICreditBureauClient`, `IBankDisbursementGateway`) cuyas implementaciones HTTP/REST o SOAP con `HttpClient` y políticas de resiliencia (Polly) viven en `Integrations.Adapters`.

---

## 3. ¿Por qué está diseñado así?

1. **Patrón Anti-Corruption Layer (ACL):**
   Los formatos JSON/XML propietarios, códigos de error cambiantes y peculiaridades de las APIs de bancos o pasarelas externas nunca deben filtrarse hacia `Loans.Core`, `Payments.Core` ni `Customers.Core`. `Integrations.Core` traduce esos contratos externos a eventos y modelos limpios de nuestro dominio.
2. **Idempotencia y Auditoría de Webhooks:**
   Las pasarelas de pago suelen enviar el mismo webhook más de una vez. Al gestionar los eventos entrantes dentro de `Integrations.Core` y persistirlos mediante `IIntegrationsUnitOfWork` + `IIntegrationsOutbox`, garantizamos que cada notificación externa se procese exactamente una vez antes de notificar a `Payments` o `Treasury`.
