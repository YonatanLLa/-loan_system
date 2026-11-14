using LoanSystem.Common.Core.Application;
using LoanSystem.Common.Core.Contracts.Approvals;

namespace LoanSystem.Common.Core.Ports.Outbound.Approvals;

/// <summary>
/// Implemented by the owning module of each four-eyes operation.
/// Revalidates target state, executes the operation, and marks the approval request APPROVED
/// in a single local database transaction (BE-SEC-09, R7).
/// </summary>
public interface IApprovalHandler
{
    string OperationType { get; }
    Task<Result<ApprovalOutcome>> ApproveAsync(ApprovalDecision decision, CancellationToken ct = default);
}
