using LoanSystem.Common.Core.Application;
using LoanSystem.Common.Core.Ports.Outbound;

namespace LoanSystem.Common.Core.Domain.Approvals;

/// <summary>
/// Four-eyes approval request shared across modules via <c>approval_requests</c> (BE-SEC-09, DB-66).
/// Enforces requester ≠ approver (R1), beneficiary ≠ approver (R2), and immutability of decisions.
/// </summary>
public sealed class ApprovalRequest : AggregateRoot<Guid>
{
    public const int MinReasonLength = 10;

    private ApprovalRequest() { }

    public string OperationType { get; private set; } = null!;
    public string Module { get; private set; } = null!;
    public string TargetEntity { get; private set; } = null!;
    public Guid TargetId { get; private set; }
    public string PayloadJson { get; private set; } = null!;
    public string TargetSnapshotJson { get; private set; } = null!;
    public string Reason { get; private set; } = null!;
    public string? EvidenceRef { get; private set; }
    public ApprovalStatus Status { get; private set; }
    public Guid RequestedBy { get; private set; }
    public DateTimeOffset RequestedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public Guid? DecidedBy { get; private set; }
    public DateTimeOffset? DecidedAt { get; private set; }
    public string? DecisionComment { get; private set; }

    public bool IsExpired(IClock clock) => ExpiresAt <= clock.UtcNow;

    public static Result<ApprovalRequest> Submit(
        ApprovalSubmission submission,
        Guid requestedBy,
        IClock clock,
        TimeSpan ttl)
    {
        if (!ApprovalOperationTypes.All.Contains(submission.OperationType, StringComparer.Ordinal))
            return Error.Validation("Approvals.InvalidOperationType", $"Unknown approval operation '{submission.OperationType}'.");

        if (string.IsNullOrWhiteSpace(submission.Module) || string.IsNullOrWhiteSpace(submission.TargetEntity))
            return Error.Validation("Approvals.MissingTarget", "Module and target entity are required.");

        if (submission.TargetId == Guid.Empty)
            return Error.Validation("Approvals.InvalidTargetId", "A valid target identifier is required.");

        if (requestedBy == Guid.Empty)
            return Error.Validation("Approvals.InvalidRequester", "A valid requester identifier is required.");

        var trimmedReason = submission.Reason?.Trim() ?? string.Empty;
        if (trimmedReason.Length < MinReasonLength)
            return Error.Validation("Approvals.InvalidReason", $"Reason must be at least {MinReasonLength} characters long.");

        if (string.IsNullOrWhiteSpace(submission.PayloadJson) || string.IsNullOrWhiteSpace(submission.TargetSnapshotJson))
            return Error.Validation("Approvals.MissingPayload", "Payload and target snapshot are required.");

        if (ttl <= TimeSpan.Zero)
            return Error.Validation("Approvals.InvalidExpiration", "Expiration lifetime must be positive.");

        var now = clock.UtcNow;
        var evidence = string.IsNullOrWhiteSpace(submission.EvidenceRef) ? null : submission.EvidenceRef.Trim();

        return new ApprovalRequest
        {
            Id = Guid.NewGuid(),
            OperationType = submission.OperationType,
            Module = submission.Module.Trim(),
            TargetEntity = submission.TargetEntity.Trim(),
            TargetId = submission.TargetId,
            PayloadJson = submission.PayloadJson,
            TargetSnapshotJson = submission.TargetSnapshotJson,
            Reason = trimmedReason,
            EvidenceRef = evidence,
            Status = ApprovalStatus.PENDING,
            RequestedBy = requestedBy,
            RequestedAt = now,
            ExpiresAt = now.Add(ttl)
        };
    }

    public Result Approve(Guid approverId, IClock clock, string? comment, Guid? beneficiaryId = null)
    {
        var check = ValidateDecisionEligibility(approverId, clock, beneficiaryId);
        if (check.IsFailure)
            return check.Error;

        Status = ApprovalStatus.APPROVED;
        DecidedBy = approverId;
        DecidedAt = clock.UtcNow;
        DecisionComment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        return Result.Success();
    }

    public Result Reject(Guid approverId, IClock clock, string comment)
    {
        var check = ValidateDecisionEligibility(approverId, clock);
        if (check.IsFailure)
            return check.Error;

        if (string.IsNullOrWhiteSpace(comment))
            return Error.Unprocessable("Approvals.CommentRequired", "A rejection comment is required.");

        Status = ApprovalStatus.REJECTED;
        DecidedBy = approverId;
        DecidedAt = clock.UtcNow;
        DecisionComment = comment.Trim();
        return Result.Success();
    }

    public Result Cancel(Guid userId, IClock clock)
    {
        if (Status != ApprovalStatus.PENDING)
            return Error.Conflict("Approvals.NotPending", $"Approval request is already {Status}.");

        if (userId != RequestedBy)
            return Error.Forbidden("Approvals.OnlyRequesterCanCancel", "Only the original requester can cancel this approval request.");

        Status = ApprovalStatus.CANCELLED;
        DecidedBy = userId;
        DecidedAt = clock.UtcNow;
        return Result.Success();
    }

    public Result CancelBySystem(IClock clock, string reason)
    {
        if (Status != ApprovalStatus.PENDING)
            return Error.Conflict("Approvals.NotPending", $"Approval request is already {Status}.");

        Status = ApprovalStatus.CANCELLED;
        DecidedBy = null;
        DecidedAt = clock.UtcNow;
        DecisionComment = string.IsNullOrWhiteSpace(reason) ? "Cancelled by system." : reason.Trim();
        return Result.Success();
    }

    public Result Expire(IClock clock)
    {
        if (Status != ApprovalStatus.PENDING)
            return Error.Conflict("Approvals.NotPending", $"Approval request is already {Status}.");

        if (!IsExpired(clock))
            return Error.Conflict("Approvals.NotExpired", "Approval request has not reached its expiration time.");

        Status = ApprovalStatus.EXPIRED;
        DecidedBy = null;
        DecidedAt = clock.UtcNow;
        return Result.Success();
    }

    private Result ValidateDecisionEligibility(Guid approverId, IClock clock, Guid? beneficiaryId = null)
    {
        if (Status != ApprovalStatus.PENDING)
            return Error.Conflict("Approvals.NotPending", $"Approval request is already {Status}.");

        if (IsExpired(clock))
            return Error.Conflict("Approvals.Expired", "The approval request has expired.");

        if (approverId == Guid.Empty)
            return Error.Validation("Approvals.InvalidApprover", "A valid approver identifier is required.");

        if (approverId == RequestedBy)
            return Error.Forbidden("Approvals.SelfApprovalForbidden", "The requester cannot approve or reject their own request.");

        var effectiveBeneficiary = beneficiaryId
            ?? (OperationType == ApprovalOperationTypes.UserRoleChange ? TargetId : null);

        if (effectiveBeneficiary.HasValue && approverId == effectiveBeneficiary.Value)
            return Error.Forbidden("Approvals.BeneficiaryCannotApprove", "The affected user cannot approve or reject this request.");

        return Result.Success();
    }
}
