namespace LoanSystem.Modules.Security.Core.Ports.Outbound;

/// <summary>Hashes and verifies user passwords (Argon2id/BCrypt). Implemented in the Adapters layer (BE-SEC-01).</summary>
internal interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string passwordHash);
}
