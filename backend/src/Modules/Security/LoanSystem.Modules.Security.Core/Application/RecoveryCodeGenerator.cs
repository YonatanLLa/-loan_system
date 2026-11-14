using System.Security.Cryptography;

namespace LoanSystem.Modules.Security.Core.Application;

/// <summary>Generates human-readable single-use recovery codes and normalizes user input for hashing (BE-SEC-03).</summary>
internal static class RecoveryCodeGenerator
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int CodeCount = 10;
    private const int GroupLength = 4;

    public static IReadOnlyList<string> GenerateBatch()
    {
        var codes = new string[CodeCount];
        for (var i = 0; i < CodeCount; i++)
        {
            codes[i] = GenerateSingle();
        }
        return codes;
    }

    public static string Normalize(string code) =>
        code.Trim().Replace("-", string.Empty, StringComparison.Ordinal).ToUpperInvariant();

    public static string Hash(string code) =>
        RefreshTokenHasher.Hash(Normalize(code));

    private static string GenerateSingle()
    {
        Span<char> buffer = stackalloc char[GroupLength * 2 + 1];
        for (var i = 0; i < GroupLength; i++)
        {
            buffer[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
            buffer[GroupLength + 1 + i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }
        buffer[GroupLength] = '-';
        return new string(buffer);
    }
}
