using LoanSystem.Common.Core.Ports.Outbound;
using LoanSystem.Modules.Security.Core.Domain;
using LoanSystem.Modules.Security.Core.Ports.Outbound;
using Microsoft.EntityFrameworkCore;

namespace LoanSystem.Modules.Security.Adapters.Outbound.Persistence;

internal sealed class MfaRecoveryCodeRepository : IMfaRecoveryCodeRepository
{
    private readonly SecurityDbContext _db;

    public MfaRecoveryCodeRepository(SecurityDbContext db) => _db = db;

    public void AddRange(IEnumerable<MfaRecoveryCode> codes) => _db.MfaRecoveryCodes.AddRange(codes);

    public async Task<IReadOnlyList<MfaRecoveryCode>> FindUnusedAsync(Guid userId, CancellationToken ct = default) =>
        await _db.MfaRecoveryCodes
            .Where(c => c.UserId == userId && c.UsedAt == null)
            .ToListAsync(ct);

    public async Task MarkAllUsedForUserAsync(Guid userId, IClock clock, CancellationToken ct = default)
    {
        var unused = await _db.MfaRecoveryCodes
            .Where(c => c.UserId == userId && c.UsedAt == null)
            .ToListAsync(ct);

        foreach (var code in unused)
        {
            code.Use(clock);
        }
    }
}
