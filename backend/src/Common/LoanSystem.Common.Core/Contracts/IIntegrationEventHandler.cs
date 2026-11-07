namespace LoanSystem.Common.Core.Contracts;

/// <summary>
/// Handles an integration event published through the outbox. Delivery is at-least-once,
/// so handlers must be idempotent (use <see cref="IIntegrationEvent.EventId"/> to deduplicate).
/// </summary>
public interface IIntegrationEventHandler<in TEvent> where TEvent : IIntegrationEvent
{
    Task HandleAsync(TEvent integrationEvent, CancellationToken ct = default);
}
