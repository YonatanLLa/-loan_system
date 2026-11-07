using LoanSystem.Common.Core.Contracts;

namespace LoanSystem.Common.Adapters.Outbox;

/// <summary>
/// Maps the event type name stored in outbox_messages.event_type (CLR full name) back to its type.
/// Only event types that have at least one registered handler are known.
/// </summary>
public sealed class IntegrationEventTypeRegistry
{
    private readonly Dictionary<string, Type> _types = new(StringComparer.Ordinal);

    public IReadOnlyCollection<Type> EventTypes => _types.Values;

    public void Register(Type eventType)
    {
        ArgumentNullException.ThrowIfNull(eventType);
        if (!typeof(IIntegrationEvent).IsAssignableFrom(eventType))
            throw new ArgumentException($"Type '{eventType.FullName}' does not implement {nameof(IIntegrationEvent)}.", nameof(eventType));

        _types[NameOf(eventType)] = eventType;
    }

    public bool TryResolve(string eventTypeName, out Type eventType) =>
        _types.TryGetValue(eventTypeName, out eventType!);

    public static string NameOf(Type eventType) => eventType.FullName ?? eventType.Name;
}
