using LoanSystem.Common.Core.Application;
using LoanSystem.Common.Core.Ports.Outbound;
using LoanSystem.Modules.Security.Core.Contracts;
using LoanSystem.Modules.Security.Core.Ports.Inbound;
using LoanSystem.Modules.Security.Core.Ports.Outbound;

namespace LoanSystem.Modules.Security.Core.Application;

internal sealed class ActivateUserUseCase : IActivateUserUseCase
{
    private readonly IUserRepository _users;
    private readonly IUserStatusTracker _statusTracker;
    private readonly ISecurityUnitOfWork _unitOfWork;
    private readonly IAuditContext _audit;

    public ActivateUserUseCase(
        IUserRepository users,
        IUserStatusTracker statusTracker,
        ISecurityUnitOfWork unitOfWork,
        IAuditContext audit)
    {
        _users = users;
        _statusTracker = statusTracker;
        _unitOfWork = unitOfWork;
        _audit = audit;
    }

    public async Task<Result> ExecuteAsync(ActivateUserRequest request, CancellationToken ct = default)
    {
        if (request.UserId == Guid.Empty)
            return Error.Validation("Security.InvalidUserId", "A valid user identifier is required.");

        var user = await _users.FindByIdAsync(request.UserId, ct);
        if (user is null)
            return Error.NotFound("Security.UserNotFound", $"User '{request.UserId}' was not found.");

        user.Activate();
        _audit.SetAction("ACTIVATE");
        await _unitOfWork.SaveChangesAsync(ct);
        _statusTracker.ActivateUser(user.Id);

        return Result.Success();
    }
}
