# LoanSystem.Adapters.UnitTests

## 1. ¿Para qué sirve?
`LoanSystem.Adapters.UnitTests` es la **Suite de Pruebas Unitarias de la Capa de Adaptadores e Infraestructura Compartida**. En nuestra Arquitectura Hexagonal, este proyecto valida que los mecanismos técnicos transversales de `LoanSystem.Common.Adapters` y de los proyectos `*.Adapters` de los módulos —como el **Middleware de Idempotencia HTTP**, el mapeo a **`ProblemDetails`** y el **Registro de Eventos de Integración del Outbox**— funcionen correctamente frente a escenarios de concurrencia, fallos y reintentos.

---

## 2. ¿Qué hace?

### Responsabilidades actuales y archivos `.cs`

- **`IdempotencyMiddlewareTests.cs`**
  - Verifica el comportamiento de seguridad financiera de `IdempotencyMiddleware` (`LoanSystem.Common.Adapters/Http/Idempotency/`):
    - **Peticiones `GET` o sin cabecera:** Pasan directamente por el pipeline sin bloqueo.
    - **Primera petición mutadora (`POST`) con `Idempotency-Key`:** Ejecuta el endpoint, captura el código de estado y el payload de respuesta y los guarda en `IIdempotencyStore` asociados al hash SHA-256 del cuerpo de la petición.
    - **Reintento idéntico (misma llave + mismo payload):** Devuelve inmediatamente la respuesta cacheada (`X-Idempotency-Replayed: true`) **sin volver a ejecutar el caso de uso**, previniendo pagos o desembolsos duplicados.
    - **Colisión de payload (misma `Idempotency-Key` pero cuerpo diferente):** Rechaza la petición con estado de error (`422 Unprocessable Entity` / `409 Conflict`) en formato `ProblemDetails`.
    - **Petición en vuelo concurrente:** Si llega una segunda petición con la misma llave mientras la primera aún no termina, devuelve `409 Conflict` inmediatamente.

- **`IntegrationEventRegistrationTests.cs`**
  - Verifica `IntegrationEventTypeRegistry` (`LoanSystem.Common.Adapters/Outbox/`):
    - Comprueba el registro y resolución bidireccional entre el nombre lógico del evento (`event_type`) y su tipo CLR (`IIntegrationEvent`).
    - Asegura que no se puedan registrar dos eventos distintos con el mismo nombre lógico, evitando fallos de deserialización JSON cuando `OutboxProcessor` despacha mensajes desde `public.outbox_messages`.

### ¿Qué se agrega aquí conforme crece el sistema?
- Pruebas unitarias de `ProblemDetailsResultMapper`, `CurrentUserProvider`, `LimaTimeZone`, convertidores JSON y adaptadores específicos de cada módulo.

---

## 3. ¿Por qué está diseñado así?

1. **Pruebas Rápidas en Memoria para Infraestructura Crítica:**
   Usando `InMemoryIdempotencyStore` y `DefaultHttpContext` en memoria, `IdempotencyMiddlewareTests` prueba todas las condiciones de carrera y casos borde del protocolo de idempotencia en pocos milisegundos sin requerir levantar un servidor web completo ni Redis.
2. **Prevención de Errores de Serialización en el Outbox:**
   Validar `IntegrationEventTypeRegistry` con pruebas unitarias garantiza que todos los eventos de integración registrados por los 12 módulos puedan serializarse al guardar la transacción y deserializarse cuando `OutboxDispatcherJob` los procesa asíncronamente.
