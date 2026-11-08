using System.Security.Cryptography;
using System.Text;

namespace LoanSystem.Modules.Security.Core.Application;

/// <summary>
/// Generates cryptographically-random refresh tokens and their storage hash.
/// Only the hash is persisted; the raw value is returned to the client once.
/// </summary>
internal static class RefreshTokenHasher
{
    /// <summary>Creates a new raw token and its SHA-256 hash.</summary>
    public static (string RawToken, string Hash) Create()
    {
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        return (raw, Hash(raw));
    }

    /// <summary>Hashes a raw token for lookup/storage (SHA-256, hex-encoded).</summary>
    public static string Hash(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes);
    }
}
