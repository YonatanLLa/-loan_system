using LoanSystem.Common.Core.Application;
using LoanSystem.Common.Core.Contracts;
using LoanSystem.Common.Core.Contracts.Approvals;
using LoanSystem.Common.Core.Ports.Outbound;
using LoanSystem.Common.Core.Ports.Outbound.Approvals;
using LoanSystem.Modules.Security.Core.Domain;
using LoanSystem.Modules.Security.Core.Ports.Inbound;
using LoanSystem.Modules.Security.Core.Ports.Outbound;

namespace LoanSystem.Modules.Security.Core.Application.Approvals;

internal sealed class QueryApprovalsUseCase : IQueryApprovalsUseCase
{
    private readonly ICurrentUser _currentUser;
    private readonly IUserRepository _users;
    private readonly IApprovalRequestReader _reader;

    public QueryApprovalsUseCase(
        ICurrentUser currentUser,
        IUserRepository users,
        IApprovalRequestReader reader)
    {
        _currentUser = currentUser;
        _users = users;
        _reader = reader;
    }

    public async Task<Result<PagedResult<ApprovalRequestSummary>>> ExecuteAsync(
        ApprovalQuery request, CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is not Guid userId)
            return Error.Unauthorized("Security.Unauthenticated", "User is not authenticated.");

        var user = await _users.FindByIdAsync(userId, ct);
        if (user is null)
            return Error.Unauthorized("Security.Unauthenticated", "User account could not be resolved.");

        if (!user.IsActive)
            return Error.Forbidden("Security.UserNotActive", "The user account is not active.");

        if (!RolePermissions.HasPermission(user.Role, "approvals.read"))
            return Error.Forbidden("Approvals.NotAllowed", "You do not have permission to view approval requests.");

        var normalized = request with
        {
            Page = request.Page < 1 ? 1 : request.Page,
            PageSize = request.PageSize is < 1 or > 100 ? 20 : request.PageSize
        };

        return await _reader.QueryAsync(normalized, userId, ct);
    }
}
