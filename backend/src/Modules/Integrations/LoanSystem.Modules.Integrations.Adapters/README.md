# LoanSystem.Modules.Integrations.Adapters

## 1. ¿Para qué sirve?
`LoanSystem.Modules.Integrations.Adapters` es la **Capa de Adaptadores (Entrada y Salida)** del módulo de Integraciones Externas (`Integrations`). Conecta el núcleo `LoanSystem.Modules.Integrations.Core` con los endpoints HTTP (recepción de webhooks y monitoreo en `/api/v1/integrations`), los clientes HTTP salientes hacia APIs de terceros y la persistencia en PostgreSQL bajo el esquema `integrations`.

---

## 2. ¿Qué hace?

### Responsabilidades actuales y archivos `.cs`

- **`IntegrationsModule.cs`**
  - Clase pública de composición del módulo que expone `AddIntegrationsModule` (registrando `IntegrationsDbContext`, `IIntegrationsUnitOfWork` e `IIntegrationsOutbox`) y `MapIntegrationsEndpoints`.

- **`Inbound/Http/IntegrationsEndpoints.cs`**
  - Adaptador primario HTTP que define el grupo `/api/v1/integrations` con tag OpenAPI `"Integrations"`, donde se exponen los endpoints receptores de webhooks de pasarelas de pago/bancos y las consultas administrativas de trazas de integración.

- **`Outbound/Persistence/IntegrationsDbContext.cs`**
  - Adaptador secundario de persistencia que hereda de `ModuleDbContext`.
  - Configura el esquema por defecto **`integrations`** (`modelBuilder.HasDefaultSchema("integrations")`), mapeando las tablas definidas en `database/migrations/10_integrations_reconciliation.sql` (`integration_logs`, `webhook_events`, `reconciliation_discrepancies`).

### ¿Qué se agrega aquí conforme crece el módulo?
- En `Outbound/Gateways/`: Implementaciones de `HttpClient` tipados con políticas de reintento exponencial y *Circuit Breaker* hacia bancos, pasarelas de recaudación y centrales de riesgo.
- En `Inbound/Http/`: Validadores de firmas HMAC-SHA256 para webhooks entrantes y endpoints protegidos con `IdempotencyMiddleware`.
- En `Inbound/Events/`: Manejadores `IIntegrationEventHandler` que escuchan solicitudes de desembolso o consultas de bureau emitidas por otros módulos.

---

## 3. ¿Por qué está diseñado así?

1. **Aislamiento de Fallos Externos:**
   Las llamadas de red a terceros son inherentemente lentas y propensas a fallos. Al confinar todos los `HttpClient` externos y sus bitácoras (`integration_logs`) en `Integrations.Adapters` y comunicarnos por eventos asíncronos (Outbox), una caída temporal de un proveedor externo jamás corrompe las transacciones locales de los módulos core.
2. **Esquema Dedicado (`integrations`):**
   Las bitácoras de payloads de entrada/salida y webhooks crecen a gran velocidad; mantenerlas aisladas en el esquema `integrations` permite gestionar su retención e indexación sin impactar las tablas transaccionales de préstamos o pagos.
