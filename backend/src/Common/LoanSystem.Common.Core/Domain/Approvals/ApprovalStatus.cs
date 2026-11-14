namespace LoanSystem.Common.Core.Domain.Approvals;

/// <summary>Lifecycle status of a four-eyes approval request (BE-SEC-09).</summary>
public enum ApprovalStatus
{
    PENDING,
    APPROVED,
    REJECTED,
    CANCELLED,
    EXPIRED
}
