using LoanSystem.Common.Core.Ports.Outbound;
using LoanSystem.Modules.Security.Core.Domain;

namespace LoanSystem.Modules.Security.Core.Ports.Outbound;

/// <summary>Persistence of refresh tokens. New and modified tokens are flushed via <see cref="ISecurityUnitOfWork"/>.</summary>
internal interface IRefreshTokenRepository
{
    void Add(RefreshToken token);

    /// <summary>Finds a token by its hash whether it is active, revoked or expired; <c>null</c> if none matches.</summary>
    Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken ct = default);

    /// <summary>
    /// Revokes every active token of the family <paramref name="familyId"/> (reuse detection).
    /// Applied immediately as a single atomic statement; it does not wait for <see cref="IUnitOfWork.SaveChangesAsync"/>.
    /// </summary>
    Task RevokeFamilyAsync(Guid familyId, IClock clock, CancellationToken ct = default);

    /// <summary>
    /// Revokes all active refresh tokens belonging to <paramref name="userId"/> (BE-SEC-04, BE-SEC-06).
    /// Applied immediately as a single atomic statement (inside the current transaction, if any);
    /// it does not wait for <see cref="IUnitOfWork.SaveChangesAsync"/>.
    /// </summary>
    Task RevokeAllForUserAsync(Guid userId, IClock clock, CancellationToken ct = default);
}
