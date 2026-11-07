using LoanSystem.Modules.Security.Core.Ports.Outbound;

namespace LoanSystem.Modules.Security.Adapters.Outbound.Providers;

/// <summary>BCrypt implementation of <see cref="IPasswordHasher"/> (BE-SEC-01).</summary>
internal sealed class BcryptPasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 12;

    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);

    public bool Verify(string password, string passwordHash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, passwordHash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            // Stored hash is malformed or in an unsupported format: treat as a failed verification.
            return false;
        }
    }
}
