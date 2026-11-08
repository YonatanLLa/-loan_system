using LoanSystem.Modules.Security.Core.Domain;

namespace LoanSystem.Modules.Security.Core.Ports.Outbound;

/// <summary>Read/track access to <see cref="User"/> aggregates. Changes are flushed via <see cref="ISecurityUnitOfWork"/>.</summary>
internal interface IUserRepository
{
    Task<User?> FindByEmailAsync(string email, CancellationToken ct = default);
    Task<User?> FindByIdAsync(Guid id, CancellationToken ct = default);
}
