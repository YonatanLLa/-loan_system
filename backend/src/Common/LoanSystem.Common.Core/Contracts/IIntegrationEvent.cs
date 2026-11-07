namespace LoanSystem.Common.Core.Contracts;

public interface IIntegrationEvent
{
    Guid EventId { get; }
    DateTimeOffset OccurredOn { get; }
    string EventType { get; }
}

public abstract record IntegrationEvent(Guid EventId, DateTimeOffset OccurredOn, string EventType) : IIntegrationEvent
{
    protected IntegrationEvent() : this(Guid.NewGuid(), DateTimeOffset.UtcNow, string.Empty)
    {
        EventType = GetType().Name;
    }
}
