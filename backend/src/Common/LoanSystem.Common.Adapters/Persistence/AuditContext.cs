using LoanSystem.Common.Core.Ports.Outbound;

namespace LoanSystem.Common.Adapters.Persistence;

/// <summary>Scoped (one per request / job scope) implementation of <see cref="IAuditContext"/>.</summary>
public sealed class AuditContext : IAuditContext
{
    private readonly Dictionary<string, string> _entityActions = new(StringComparer.OrdinalIgnoreCase);

    public string? Action { get; private set; }

    public Guid? ActorId { get; private set; }

    public Guid? ApprovalRequestId { get; private set; }

    public void SetAction(string action)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        Action = action.Trim().ToUpperInvariant();
    }

    public void SetAction(string action, string entityName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityName);
        _entityActions[entityName.Trim()] = action.Trim().ToUpperInvariant();
    }

    public string? GetActionForEntity(string entityName) =>
        _entityActions.TryGetValue(entityName, out var action) ? action : null;

    public void SetActor(Guid userId)
    {
        if (userId != Guid.Empty)
        {
            ActorId = userId;
        }
    }

    public void SetApproval(Guid approvalRequestId)
    {
        if (approvalRequestId != Guid.Empty)
        {
            ApprovalRequestId = approvalRequestId;
        }
    }
}
