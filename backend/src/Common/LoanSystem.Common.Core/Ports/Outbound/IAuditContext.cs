namespace LoanSystem.Common.Core.Ports.Outbound;

/// <summary>
/// Per-request audit context (BE-SEC-07). Lets a use case describe what it is doing so the audit
/// interceptor records a business action ("SUSPEND", "LOGIN", ...) instead of a generic CREATE/UPDATE,
/// and identifies the actor when the request is still anonymous (e.g. a successful login).
/// </summary>
public interface IAuditContext
{
    /// <summary>Business action set by the current use case, or <see langword="null"/> to use CREATE/UPDATE/DELETE.</summary>
    string? Action { get; }

    /// <summary>User the change is attributed to when there is no authenticated user in the request.</summary>
    Guid? ActorId { get; }

    void SetAction(string action);

    void SetActor(Guid userId);
}
