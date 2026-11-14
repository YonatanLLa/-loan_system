using LoanSystem.Common.Core.Application;
using LoanSystem.Common.Core.Domain.Approvals;
using LoanSystem.Common.Core.Ports.Outbound;
using Shouldly;
using Xunit;

namespace LoanSystem.Domain.UnitTests;

public class ApprovalRequestTests
{
    private static readonly Guid RequesterId = Guid.NewGuid();
    private static readonly Guid ApproverId = Guid.NewGuid();
    private static readonly Guid TargetId = Guid.NewGuid();

    private static ApprovalRequest CreatePending(
        string operationType = ApprovalOperationTypes.PaymentReversal,
        Guid? targetId = null,
        FakeClock? clock = null)
    {
        clock ??= new FakeClock();
        var submission = new ApprovalSubmission(
            operationType,
            Module: "Payments",
            TargetEntity: "payments",
            TargetId: targetId ?? TargetId,
            PayloadJson: "{\"reason\":\"Error de digitación\"}",
            TargetSnapshotJson: "{\"status\":\"APPLIED\"}",
            Reason: "Motivo suficientemente descriptivo");

        var result = ApprovalRequest.Submit(submission, RequesterId, clock, TimeSpan.FromHours(48));
        result.IsSuccess.ShouldBeTrue();
        return result.Value;
    }

    [Fact]
    public void Submit_WithShortReason_ReturnsValidationError()
    {
        var clock = new FakeClock();
        var submission = new ApprovalSubmission(
            ApprovalOperationTypes.PaymentReversal,
            "Payments",
            "payments",
            TargetId,
            "{}",
            "{}",
            Reason: "corto");

        var result = ApprovalRequest.Submit(submission, RequesterId, clock, TimeSpan.FromHours(24));

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Validation);
        result.Error.Code.ShouldBe("Approvals.InvalidReason");
    }

    [Fact]
    public void Approve_ByRequester_ReturnsSelfApprovalForbidden()
    {
        var clock = new FakeClock();
        var request = CreatePending(clock: clock);

        var result = request.Approve(RequesterId, clock, "Trying to self-approve");

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Forbidden);
        result.Error.Code.ShouldBe("Approvals.SelfApprovalForbidden");
        request.Status.ShouldBe(ApprovalStatus.PENDING);
    }

    [Fact]
    public void Approve_ByBeneficiaryOfRoleChange_ReturnsBeneficiaryCannotApprove()
    {
        var clock = new FakeClock();
        var beneficiaryId = Guid.NewGuid();
        var request = CreatePending(ApprovalOperationTypes.UserRoleChange, beneficiaryId, clock);

        var result = request.Approve(beneficiaryId, clock, "Self-promotion");

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Forbidden);
        result.Error.Code.ShouldBe("Approvals.BeneficiaryCannotApprove");
        request.Status.ShouldBe(ApprovalStatus.PENDING);
    }

    [Fact]
    public void Approve_Twice_ReturnsNotPending()
    {
        var clock = new FakeClock();
        var request = CreatePending(clock: clock);

        request.Approve(ApproverId, clock, "OK").IsSuccess.ShouldBeTrue();
        var second = request.Approve(Guid.NewGuid(), clock, "Second");

        second.IsFailure.ShouldBeTrue();
        second.Error.Type.ShouldBe(ErrorType.Conflict);
        second.Error.Code.ShouldBe("Approvals.NotPending");
    }

    [Fact]
    public void Approve_WhenExpired_ReturnsExpired()
    {
        var clock = new FakeClock();
        var request = CreatePending(clock: clock);
        var later = new FakeClock(clock.UtcNow.AddHours(49));

        var result = request.Approve(ApproverId, later, "Too late");

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Conflict);
        result.Error.Code.ShouldBe("Approvals.Expired");
    }

    [Fact]
    public void Reject_WithoutComment_ReturnsUnprocessable()
    {
        var clock = new FakeClock();
        var request = CreatePending(clock: clock);

        var result = request.Reject(ApproverId, clock, "   ");

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Unprocessable);
        result.Error.Code.ShouldBe("Approvals.CommentRequired");
        request.Status.ShouldBe(ApprovalStatus.PENDING);
    }

    [Fact]
    public void Reject_WithCommentByDistinctApprover_TransitionsToRejectedAndBlocksFurtherChanges()
    {
        var clock = new FakeClock();
        var request = CreatePending(clock: clock);

        var result = request.Reject(ApproverId, clock, "No adjunta sustento");

        result.IsSuccess.ShouldBeTrue();
        request.Status.ShouldBe(ApprovalStatus.REJECTED);
        request.DecidedBy.ShouldBe(ApproverId);
        request.Approve(ApproverId, clock, "Override").Error.Code.ShouldBe("Approvals.NotPending");
        request.Cancel(RequesterId, clock).Error.Code.ShouldBe("Approvals.NotPending");
    }

    [Fact]
    public void Cancel_ByNonRequester_ReturnsForbidden_ByRequester_Cancels()
    {
        var clock = new FakeClock();
        var request = CreatePending(clock: clock);

        var forbidden = request.Cancel(ApproverId, clock);
        forbidden.IsFailure.ShouldBeTrue();
        forbidden.Error.Type.ShouldBe(ErrorType.Forbidden);

        var ok = request.Cancel(RequesterId, clock);
        ok.IsSuccess.ShouldBeTrue();
        request.Status.ShouldBe(ApprovalStatus.CANCELLED);
        request.DecidedBy.ShouldBe(RequesterId);
    }

    [Fact]
    public void Expire_WhenPastExpiresAt_TransitionsToExpired()
    {
        var clock = new FakeClock();
        var request = CreatePending(clock: clock);

        request.Expire(clock).IsFailure.ShouldBeTrue();

        var afterExpiry = new FakeClock(clock.UtcNow.AddHours(48));
        var expired = request.Expire(afterExpiry);

        expired.IsSuccess.ShouldBeTrue();
        request.Status.ShouldBe(ApprovalStatus.EXPIRED);
        request.DecidedBy.ShouldBeNull();
    }

    private sealed class FakeClock(DateTimeOffset? now = null) : IClock
    {
        public DateTimeOffset UtcNow { get; } = now ?? new DateTimeOffset(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);
        public DateTime LimaNow => UtcNow.UtcDateTime.AddHours(-5);
        public DateOnly LimaToday => DateOnly.FromDateTime(LimaNow);
    }
}
