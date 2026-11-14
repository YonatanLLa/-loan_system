namespace LoanSystem.Modules.Security.Core.Ports.Outbound;

/// <summary>
/// Encrypts and decrypts user-bound sensitive values such as TOTP secrets with key rotation support (BE-SEC-03, BE-SEC-10).
/// </summary>
internal interface ISecretProtector
{
    string Protect(string plaintext, Guid userId);
    string Unprotect(string protectedValue, Guid userId);
    bool NeedsReprotect(string protectedValue);
}
