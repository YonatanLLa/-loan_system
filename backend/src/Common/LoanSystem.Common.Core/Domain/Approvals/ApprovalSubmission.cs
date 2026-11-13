namespace LoanSystem.Common.Core.Domain.Approvals;

/// <summary>Parameters required to submit a new four-eyes approval request (BE-SEC-09).</summary>
public sealed record ApprovalSubmission(
    string OperationType,
    string Module,
    string TargetEntity,
    Guid TargetId,
    string PayloadJson,
    string TargetSnapshotJson,
    string Reason,
    string? EvidenceRef = null);
