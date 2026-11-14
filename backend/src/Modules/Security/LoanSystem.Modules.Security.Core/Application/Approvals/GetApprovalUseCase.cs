using LoanSystem.Common.Core.Application;
using LoanSystem.Common.Core.Contracts.Approvals;
using LoanSystem.Common.Core.Ports.Outbound;
using LoanSystem.Common.Core.Ports.Outbound.Approvals;
using LoanSystem.Modules.Security.Core.Domain;
using LoanSystem.Modules.Security.Core.Ports.Inbound;
using LoanSystem.Modules.Security.Core.Ports.Outbound;

namespace LoanSystem.Modules.Security.Core.Application.Approvals;

internal sealed class GetApprovalUseCase : IGetApprovalUseCase
{
    private readonly ICurrentUser _currentUser;
    private readonly IUserRepository _users;
    private readonly IApprovalRequestReader _reader;

    public GetApprovalUseCase(
        ICurrentUser currentUser,
        IUserRepository users,
        IApprovalRequestReader reader)
    {
        _currentUser = currentUser;
        _users = users;
        _reader = reader;
    }

    public async Task<Result<ApprovalRequestDto>> ExecuteAsync(Guid approvalRequestId, CancellationToken ct = default)
    {
        if (approvalRequestId == Guid.Empty)
            return Error.Validation("Approvals.InvalidId", "A valid approval request identifier is required.");

        if (!_currentUser.IsAuthenticated || _currentUser.UserId is not Guid userId)
            return Error.Unauthorized("Security.Unauthenticated", "User is not authenticated.");

        var user = await _users.FindByIdAsync(userId, ct);
        if (user is null)
            return Error.Unauthorized("Security.Unauthenticated", "User account could not be resolved.");

        if (!user.IsActive)
            return Error.Forbidden("Security.UserNotActive", "The user account is not active.");

        var dto = await _reader.GetByIdAsync(approvalRequestId, ct);
        if (dto is null)
            return Error.NotFound("Approvals.NotFound", $"Approval request '{approvalRequestId}' was not found.");

        if (!RolePermissions.HasPermission(user.Role, "approvals.read") && dto.RequestedBy.Id != userId)
            return Error.Forbidden("Approvals.NotAllowed", "You do not have permission to view this approval request.");

        return dto;
    }
}
