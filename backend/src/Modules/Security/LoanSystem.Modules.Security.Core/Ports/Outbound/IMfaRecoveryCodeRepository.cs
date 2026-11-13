using LoanSystem.Common.Core.Ports.Outbound;
using LoanSystem.Modules.Security.Core.Domain;

namespace LoanSystem.Modules.Security.Core.Ports.Outbound;

internal interface IMfaRecoveryCodeRepository
{
    void AddRange(IEnumerable<MfaRecoveryCode> codes);
    Task<IReadOnlyList<MfaRecoveryCode>> FindUnusedAsync(Guid userId, CancellationToken ct = default);
    Task MarkAllUsedForUserAsync(Guid userId, IClock clock, CancellationToken ct = default);
}
