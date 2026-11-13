using LoanSystem.Common.Core.Ports.Outbound;
using LoanSystem.Modules.Security.Core.Domain;
using LoanSystem.Modules.Security.Core.Ports.Outbound;
using Microsoft.EntityFrameworkCore;

namespace LoanSystem.Modules.Security.Adapters.Outbound.Persistence;

internal sealed class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly SecurityDbContext _db;

    public RefreshTokenRepository(SecurityDbContext db) => _db = db;

    public void Add(RefreshToken token) => _db.RefreshTokens.Add(token);

    public Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken ct = default) =>
        _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

    // Bulk UPDATE ... WHERE revoked_at IS NULL: atomic, catches tokens inserted after any earlier read
    // and never trips the revoked_at concurrency token (tracked entities are not involved).
    public Task RevokeFamilyAsync(Guid familyId, IClock clock, CancellationToken ct = default)
    {
        DateTimeOffset? now = clock.UtcNow;
        return _db.RefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);
    }

    public Task RevokeAllForUserAsync(Guid userId, IClock clock, CancellationToken ct = default)
    {
        DateTimeOffset? now = clock.UtcNow;
        return _db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);
    }
}
