using LoanSystem.Common.Core.Ports.Outbound;
using LoanSystem.Modules.Security.Core.Domain;

namespace LoanSystem.Modules.Security.Core.Ports.Outbound;

/// <summary>Persistence of refresh tokens. New tokens are flushed via <see cref="ISecurityUnitOfWork"/>.</summary>
internal interface IRefreshTokenRepository
{
    void Add(RefreshToken token);

    /// <summary>Finds a non-revoked token by its hash, or <c>null</c> if none matches.</summary>
    Task<RefreshToken?> FindActiveByHashAsync(string tokenHash, CancellationToken ct = default);

    /// <summary>Revokes all active refresh tokens belonging to <paramref name="userId"/> (BE-SEC-04, BE-SEC-06).</summary>
    Task RevokeAllForUserAsync(Guid userId, IClock clock, CancellationToken ct = default);
}
