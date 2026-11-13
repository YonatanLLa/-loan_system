using LoanSystem.Modules.Security.Core.Domain;

namespace LoanSystem.Modules.Security.Core.Ports.Outbound;

internal interface IMfaChallengeRepository
{
    void Add(MfaChallenge challenge);
    Task<MfaChallenge?> FindByHashAsync(string tokenHash, CancellationToken ct = default);
}
