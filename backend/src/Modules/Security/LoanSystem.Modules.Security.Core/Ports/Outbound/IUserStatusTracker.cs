namespace LoanSystem.Modules.Security.Core.Ports.Outbound;

/// <summary>Tracks active/inactive user session states for real-time token invalidation and reactivation (BE-SEC-06).</summary>
public interface IUserStatusTracker
{
    void InvalidateUser(Guid userId);
    void ActivateUser(Guid userId);
}
