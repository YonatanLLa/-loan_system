namespace LoanSystem.Modules.Security.Core.Ports.Outbound;

/// <summary>Tracks active/inactive user session states and role invalidations in real time (BE-SEC-06, BE-SEC-09).</summary>
public interface IUserStatusTracker
{
    void InvalidateUser(Guid userId);
    void ActivateUser(Guid userId);
    void EvictUser(Guid userId);
}
