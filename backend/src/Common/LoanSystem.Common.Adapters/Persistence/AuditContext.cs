using LoanSystem.Common.Core.Ports.Outbound;

namespace LoanSystem.Common.Adapters.Persistence;

/// <summary>Scoped (one per request / job scope) implementation of <see cref="IAuditContext"/>.</summary>
public sealed class AuditContext : IAuditContext
{
    public string? Action { get; private set; }

    public Guid? ActorId { get; private set; }

    public void SetAction(string action)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        Action = action.Trim().ToUpperInvariant();
    }

    public void SetActor(Guid userId)
    {
        if (userId != Guid.Empty)
        {
            ActorId = userId;
        }
    }
}
