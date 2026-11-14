using LoanSystem.Modules.Security.Core.Contracts;

namespace LoanSystem.Modules.Security.Core.Ports.Outbound;

/// <summary>Provides the current active and retained public RSA keys for JWKS publication (BE-SEC-10).</summary>
internal interface IPublicSigningKeysProvider
{
    IReadOnlyList<JwkDto> GetValidationKeys();
}
