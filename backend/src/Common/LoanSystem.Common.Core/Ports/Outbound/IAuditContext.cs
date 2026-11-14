namespace LoanSystem.Common.Core.Ports.Outbound;

/// <summary>
/// Per-request audit context (BE-SEC-07, BE-SEC-09). Lets a use case describe what it is doing so the audit
/// interceptor records a business action ("SUSPEND", "LOGIN", "APPROVE", ...) instead of a generic CREATE/UPDATE,
/// link changes to an <see cref="ApprovalRequestId"/>, and identify the actor when the request is still anonymous.
/// </summary>
public interface IAuditContext
{
    /// <summary>Default business action set by the current use case, or <see langword="null"/> to use CREATE/UPDATE/DELETE.</summary>
    string? Action { get; }

    /// <summary>User the change is attributed to when there is no authenticated user in the request.</summary>
    Guid? ActorId { get; }

    /// <summary>Four-eyes approval request governing the changes in this save batch (BE-SEC-09, R9).</summary>
    Guid? ApprovalRequestId { get; }

    void SetAction(string action);

    void SetAction(string action, string entityName);

    string? GetActionForEntity(string entityName);

    void SetActor(Guid userId);

    void SetApproval(Guid approvalRequestId);
}
