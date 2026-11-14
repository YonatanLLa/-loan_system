using LoanSystem.Common.Core.Application;
using LoanSystem.Common.Core.Contracts.Approvals;
using LoanSystem.Common.Core.Domain.Approvals;
using LoanSystem.Common.Core.Ports.Outbound;
using LoanSystem.Common.Core.Ports.Outbound.Approvals;
using LoanSystem.Modules.Security.Core.Contracts;
using LoanSystem.Modules.Security.Core.Domain;
using LoanSystem.Modules.Security.Core.Ports.Inbound;
using LoanSystem.Modules.Security.Core.Ports.Outbound;

namespace LoanSystem.Modules.Security.Core.Application.Approvals;

internal sealed class ApproveRequestUseCase : IApproveRequestUseCase
{
    private static readonly Error InvalidStepUpCode =
        Error.Forbidden("Approvals.InvalidMfaCode", "A valid TOTP verification code is required to approve this request.");

    private readonly ICurrentUser _currentUser;
    private readonly IUserRepository _users;
    private readonly IApprovalRequestReader _reader;
    private readonly ITotpService _totp;
    private readonly ISecretProtector _protector;
    private readonly ISecurityUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly IAuditContext _audit;
    private readonly ApprovalsOptions _approvalsOptions;
    private readonly IEnumerable<IApprovalHandler> _handlers;

    public ApproveRequestUseCase(
        ICurrentUser currentUser,
        IUserRepository users,
        IApprovalRequestReader reader,
        ITotpService totp,
        ISecretProtector protector,
        ISecurityUnitOfWork unitOfWork,
        IClock clock,
        IAuditContext audit,
        ApprovalsOptions approvalsOptions,
        IEnumerable<IApprovalHandler> handlers)
    {
        _currentUser = currentUser;
        _users = users;
        _reader = reader;
        _totp = totp;
        _protector = protector;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _audit = audit;
        _approvalsOptions = approvalsOptions;
        _handlers = handlers;
    }

    public async Task<Result<ApprovalDecisionResponse>> ExecuteAsync(
        ApproveApprovalCommand command, CancellationToken ct = default)
    {
        if (command.ApprovalRequestId == Guid.Empty)
            return Error.Validation("Approvals.InvalidId", "A valid approval request identifier is required.");

        if (!_currentUser.IsAuthenticated || _currentUser.UserId is not Guid approverId)
            return Error.Unauthorized("Security.Unauthenticated", "User is not authenticated.");

        var header = await _reader.GetHeaderAsync(command.ApprovalRequestId, ct);
        if (header is null)
            return Error.NotFound("Approvals.NotFound", $"Approval request '{command.ApprovalRequestId}' was not found.");

        if (header.Status != ApprovalStatus.PENDING)
            return Error.Conflict("Approvals.NotPending", $"Approval request is already {header.Status}.");

        if (header.ExpiresAt <= _clock.UtcNow)
            return Error.Conflict("Approvals.Expired", "The approval request has expired.");

        var approver = await _users.FindByIdAsync(approverId, ct);
        if (approver is null)
            return Error.Unauthorized("Security.Unauthenticated", "Approver account could not be resolved.");

        if (!approver.IsActive)
            return Error.Forbidden("Security.UserNotActive", "The user account is not active.");

        // Check operation-specific approval permission before any write (BE-SEC-05, R3)
        var requiredPermission = ApprovalOperationPolicy.GetApprovalPermission(header.OperationType);
        if (requiredPermission is null || !RolePermissions.HasPermission(approver.Role, requiredPermission))
            return Error.Forbidden("Approvals.NotAllowed", "You do not have permission to approve this operation.");

        // Fast fail R1 & R2 (the handler also enforces these inside the transaction)
        if (approver.Id == header.RequestedBy)
            return Error.Forbidden("Approvals.SelfApprovalForbidden", "The requester cannot approve their own request.");

        if (string.Equals(header.OperationType, ApprovalOperationTypes.UserRoleChange, StringComparison.Ordinal)
            && approver.Id == header.TargetId)
        {
            return Error.Forbidden("Approvals.BeneficiaryCannotApprove", "The affected user cannot approve their own role change.");
        }

        // Step-up MFA check (D8): returns 403 (never 401) so the client does not treat it as an expired session
        if (_approvalsOptions.RequiresMfaStepUp(header.OperationType))
        {
            if (string.IsNullOrWhiteSpace(command.MfaCode)
                || !approver.MfaEnabled
                || string.IsNullOrWhiteSpace(approver.MfaSecretEncrypted))
            {
                return InvalidStepUpCode;
            }

            var secret = _protector.Unprotect(approver.MfaSecretEncrypted, approver.Id);
            if (!_totp.TryVerify(secret, command.MfaCode.Trim(), _clock.UtcNow, out var matchedStep)
                || !approver.TryAcceptMfaStep(matchedStep))
            {
                return InvalidStepUpCode;
            }

            if (_protector.NeedsReprotect(approver.MfaSecretEncrypted))
            {
                approver.ReprotectMfaSecret(_protector.Protect(secret, approver.Id));
            }

            _audit.SetApproval(header.Id);
            _audit.SetAction("MFA_STEP_UP", "users");
            await _unitOfWork.SaveChangesAsync(ct);
        }

        var handler = _handlers.FirstOrDefault(h =>
            string.Equals(h.OperationType, header.OperationType, StringComparison.Ordinal));
        if (handler is null)
            return Error.Failure("Approvals.MissingHandler", $"No approval handler registered for '{header.OperationType}'.");

        var outcome = await handler.ApproveAsync(
            new ApprovalDecision(header.Id, approver.Id, command.Comment), ct);

        if (outcome.IsFailure)
            return outcome.Error;

        return new ApprovalDecisionResponse(header.Id, nameof(ApprovalStatus.APPROVED), outcome.Value);
    }
}
