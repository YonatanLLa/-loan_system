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

    public Task<RefreshToken?> FindActiveByHashAsync(string tokenHash, CancellationToken ct = default) =>
        _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash && t.RevokedAt == null, ct);

    public async Task RevokeAllForUserAsync(Guid userId, IClock clock, CancellationToken ct = default)
    {
        var activeTokens = await _db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ToListAsync(ct);

        foreach (var token in activeTokens)
        {
            token.Revoke(clock);
        }
    }
}
