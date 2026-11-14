using LoanSystem.Modules.Security.Core.Domain;
using LoanSystem.Modules.Security.Core.Ports.Outbound;
using Microsoft.EntityFrameworkCore;

namespace LoanSystem.Modules.Security.Adapters.Outbound.Persistence;

internal sealed class MfaChallengeRepository : IMfaChallengeRepository
{
    private readonly SecurityDbContext _db;

    public MfaChallengeRepository(SecurityDbContext db) => _db = db;

    public void Add(MfaChallenge challenge) => _db.MfaChallenges.Add(challenge);

    public Task<MfaChallenge?> FindByHashAsync(string tokenHash, CancellationToken ct = default) =>
        _db.MfaChallenges.FirstOrDefaultAsync(c => c.TokenHash == tokenHash, ct);
}
