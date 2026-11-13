using LoanSystem.Common.Core.Application;
using LoanSystem.Common.Core.Ports.Outbound;
using LoanSystem.Modules.Security.Core.Contracts;
using LoanSystem.Modules.Security.Core.Domain;
using LoanSystem.Modules.Security.Core.Ports.Inbound;
using LoanSystem.Modules.Security.Core.Ports.Outbound;

namespace LoanSystem.Modules.Security.Core.Application;

internal sealed class GetCurrentUserUseCase : IGetCurrentUserUseCase
{
    private static readonly Error Unauthenticated =
        Error.Unauthorized("Security.Unauthenticated", "User is not authenticated.");

    private readonly ICurrentUser _currentUser;
    private readonly IUserRepository _users;

    public GetCurrentUserUseCase(ICurrentUser currentUser, IUserRepository users)
    {
        _currentUser = currentUser;
        _users = users;
    }

    public async Task<Result<MeResponse>> ExecuteAsync(CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is not Guid userId)
            return Unauthenticated;

        var user = await _users.FindByIdAsync(userId, ct);
        if (user is not null)
        {
            if (!user.IsActive)
                return Error.Forbidden("Security.UserNotActive", "The user account is not active.");

            var permissions = RolePermissions.GetForRole(user.Role);
            return new MeResponse(
                user.Id,
                user.Id,
                user.FullName,
                user.Email,
                user.Role.ToString(),
                permissions,
                user.MfaEnabled);
        }

        // Fallback to authenticated claims when the user is resolved purely from the principal.
        var roleClaim = _currentUser.Roles.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(_currentUser.Email) ||
            string.IsNullOrWhiteSpace(roleClaim) ||
            !Enum.TryParse<SecurityRole>(roleClaim, ignoreCase: true, out var parsedRole))
        {
            return Unauthenticated;
        }

        return new MeResponse(
            userId,
            userId,
            _currentUser.Email,
            _currentUser.Email,
            parsedRole.ToString(),
            RolePermissions.GetForRole(parsedRole));
    }
}
