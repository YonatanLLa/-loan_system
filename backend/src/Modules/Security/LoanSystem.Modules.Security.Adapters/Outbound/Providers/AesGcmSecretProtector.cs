using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LoanSystem.Modules.Security.Core.Ports.Outbound;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;

namespace LoanSystem.Modules.Security.Adapters.Outbound.Providers;

/// <summary>
/// Encrypts and decrypts user-bound TOTP secrets using AES-256-GCM with versioned key rings
/// (<c>v2:{keyId}:{base64(nonce|ciphertext|tag)}</c> bound to <c>userId</c> via AAD),
/// backward compatibility for legacy <c>v1:</c> ciphertexts, and hot reload (BE-SEC-03, BE-SEC-10).
/// </summary>
internal sealed class AesGcmSecretProtector : ISecretProtector, IDisposable
{
    private const string V1Prefix = "v1:";
    private const string V2Prefix = "v2:";
    private const string V1KeyId = "v1";
    private const int KeySizeBytes = 32;
    private const int NonceSizeBytes = 12;
    private const int TagSizeBytes = 16;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IConfiguration? _configuration;
    private readonly ILogger<AesGcmSecretProtector> _logger;
    private readonly Lock _reloadLock = new();

    private volatile KeyRingSnapshot? _snapshot;
    private IDisposable? _reloadRegistration;
    private bool _disposed;

    public AesGcmSecretProtector(
        IConfiguration configuration,
        ILogger<AesGcmSecretProtector>? logger = null)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? NullLogger<AesGcmSecretProtector>.Instance;
    }

    internal AesGcmSecretProtector(byte[] key)
        : this(V1KeyId, new Dictionary<string, byte[]>(StringComparer.Ordinal) { [V1KeyId] = key })
    {
    }

    internal AesGcmSecretProtector(string activeKeyId, IReadOnlyDictionary<string, byte[]> keys)
    {
        _logger = NullLogger<AesGcmSecretProtector>.Instance;
        _snapshot = BuildValidatedSnapshot(activeKeyId, keys);
    }

    public string Protect(string plaintext, Guid userId)
    {
        ArgumentException.ThrowIfNullOrEmpty(plaintext);
        ValidateUserId(userId);

        var snapshot = EnsureInitialized();
        var key = snapshot.Keys[snapshot.ActiveKeyId];

        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var payload = new byte[NonceSizeBytes + plaintextBytes.Length + TagSizeBytes];

        var nonce = payload.AsSpan(0, NonceSizeBytes);
        var ciphertext = payload.AsSpan(NonceSizeBytes, plaintextBytes.Length);
        var tag = payload.AsSpan(NonceSizeBytes + plaintextBytes.Length, TagSizeBytes);

        RandomNumberGenerator.Fill(nonce);

        Span<byte> aad = stackalloc byte[16];
        userId.TryWriteBytes(aad);

        using var aes = new AesGcm(key, TagSizeBytes);
        aes.Encrypt(nonce, plaintextBytes, ciphertext, tag, aad);

        return $"{V2Prefix}{snapshot.ActiveKeyId}:{Convert.ToBase64String(payload)}";
    }

    public string Unprotect(string protectedValue, Guid userId)
    {
        ValidateUserId(userId);
        if (string.IsNullOrWhiteSpace(protectedValue))
        {
            throw new CryptographicException("Unsupported protected secret format.");
        }

        var snapshot = EnsureInitialized();

        if (protectedValue.StartsWith(V2Prefix, StringComparison.Ordinal))
        {
            var rest = protectedValue.AsSpan(V2Prefix.Length);
            var separatorIndex = rest.IndexOf(':');
            if (separatorIndex <= 0 || separatorIndex == rest.Length - 1)
            {
                throw new CryptographicException("Malformed v2 protected secret format.");
            }

            var keyId = rest[..separatorIndex].ToString();
            if (!snapshot.Keys.TryGetValue(keyId, out var key))
            {
                throw new CryptographicException($"MFA encryption key '{keyId}' is not available.");
            }

            var payload = DecodePayload(rest[(separatorIndex + 1)..]);
            Span<byte> aad = stackalloc byte[16];
            userId.TryWriteBytes(aad);

            return DecryptWithKey(payload, key, aad);
        }

        if (protectedValue.StartsWith(V1Prefix, StringComparison.Ordinal))
        {
            if (!snapshot.Keys.TryGetValue(V1KeyId, out var v1Key))
            {
                throw new CryptographicException("Legacy MFA encryption key 'v1' is not available in the key ring.");
            }

            var payload = DecodePayload(protectedValue.AsSpan(V1Prefix.Length));
            return DecryptWithKey(payload, v1Key, associatedData: ReadOnlySpan<byte>.Empty);
        }

        throw new CryptographicException("Unsupported protected secret format.");
    }

    public bool NeedsReprotect(string protectedValue)
    {
        if (string.IsNullOrWhiteSpace(protectedValue))
        {
            throw new CryptographicException("Unsupported protected secret format.");
        }

        var snapshot = EnsureInitialized();

        if (protectedValue.StartsWith(V1Prefix, StringComparison.Ordinal))
        {
            return true;
        }

        if (protectedValue.StartsWith(V2Prefix, StringComparison.Ordinal))
        {
            var rest = protectedValue.AsSpan(V2Prefix.Length);
            var separatorIndex = rest.IndexOf(':');
            if (separatorIndex <= 0 || separatorIndex == rest.Length - 1)
            {
                throw new CryptographicException("Malformed v2 protected secret format.");
            }

            var keyId = rest[..separatorIndex];
            return !keyId.SequenceEqual(snapshot.ActiveKeyId);
        }

        throw new CryptographicException("Unsupported protected secret format.");
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _reloadRegistration?.Dispose();
    }

    private KeyRingSnapshot EnsureInitialized()
    {
        var existing = _snapshot;
        if (existing is not null)
        {
            return existing;
        }

        lock (_reloadLock)
        {
            if (_snapshot is not null)
            {
                return _snapshot;
            }

            _snapshot = ParseFromConfiguration();
            if (_configuration is not null)
            {
                _reloadRegistration ??= ChangeToken.OnChange(
                    () => _configuration.GetReloadToken(),
                    ReloadOnConfigurationChange);
            }

            return _snapshot;
        }
    }

    private void ReloadOnConfigurationChange()
    {
        if (_disposed || _configuration is null)
        {
            return;
        }

        lock (_reloadLock)
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                _snapshot = ParseFromConfiguration();
                _logger.LogInformation("Reloaded MFA encryption key ring.");
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to reload 'Security:Mfa:EncryptionKeys'; retaining previous MFA encryption key ring.");
            }
        }
    }

    private KeyRingSnapshot ParseFromConfiguration()
    {
        var rawJson = _configuration?["Security:Mfa:EncryptionKeys"];
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            throw new InvalidOperationException(
                "Configuration value 'Security:Mfa:EncryptionKeys' is required. " +
                "In Development, configure it via 'dotnet user-secrets set \"Security:Mfa:EncryptionKeys\" ...'.");
        }

        EncryptionKeysDocument? doc;
        try
        {
            doc = JsonSerializer.Deserialize<EncryptionKeysDocument>(rawJson, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Configuration value 'Security:Mfa:EncryptionKeys' is not valid JSON.", ex);
        }

        if (doc is null || string.IsNullOrWhiteSpace(doc.Active) || doc.Keys is null || doc.Keys.Count == 0)
        {
            throw new InvalidOperationException(
                "Configuration value 'Security:Mfa:EncryptionKeys' must define 'active' and a non-empty 'keys' map.");
        }

        var decoded = new Dictionary<string, byte[]>(doc.Keys.Count, StringComparer.Ordinal);
        foreach (var (keyId, base64) in doc.Keys)
        {
            if (string.IsNullOrWhiteSpace(base64))
            {
                throw new InvalidOperationException($"MFA encryption key '{keyId}' cannot be empty.");
            }

            decoded[keyId] = DecodeKey(base64, keyId);
        }

        return BuildValidatedSnapshot(doc.Active, decoded);
    }

    private static KeyRingSnapshot BuildValidatedSnapshot(
        string activeKeyId,
        IReadOnlyDictionary<string, byte[]> keys)
    {
        if (string.IsNullOrWhiteSpace(activeKeyId) || activeKeyId.Contains(':', StringComparison.Ordinal))
        {
            throw new ArgumentException("Active MFA key id must be non-empty and must not contain ':'.", nameof(activeKeyId));
        }

        ArgumentNullException.ThrowIfNull(keys);
        if (keys.Count == 0)
        {
            throw new ArgumentException("At least one MFA encryption key is required.", nameof(keys));
        }

        var copy = new Dictionary<string, byte[]>(keys.Count, StringComparer.Ordinal);
        foreach (var (id, keyBytes) in keys)
        {
            if (string.IsNullOrWhiteSpace(id) || id.Contains(':', StringComparison.Ordinal))
            {
                throw new ArgumentException($"Invalid MFA encryption key id '{id}'.", nameof(keys));
            }

            if (keyBytes is null || keyBytes.Length != KeySizeBytes)
            {
                throw new ArgumentException($"MFA encryption key '{id}' must be {KeySizeBytes} bytes (256 bits).", nameof(keys));
            }

            copy[id] = keyBytes.ToArray();
        }

        if (!copy.ContainsKey(activeKeyId))
        {
            throw new InvalidOperationException(
                $"Active MFA encryption key '{activeKeyId}' was not found in 'keys'.");
        }

        return new KeyRingSnapshot(activeKeyId, copy);
    }

    private static byte[] DecodeKey(string base64Key, string keyId)
    {
        try
        {
            return Convert.FromBase64String(base64Key);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException(
                $"MFA encryption key '{keyId}' must be a valid Base64-encoded 256-bit key.", ex);
        }
    }

    private static byte[] DecodePayload(ReadOnlySpan<char> base64Span)
    {
        byte[] payload;
        try
        {
            payload = Convert.FromBase64String(base64Span.ToString());
        }
        catch (FormatException ex)
        {
            throw new CryptographicException("Protected secret payload is not valid Base64.", ex);
        }

        if (payload.Length < NonceSizeBytes + TagSizeBytes)
        {
            throw new CryptographicException("Protected secret payload is too short.");
        }

        return payload;
    }

    private static string DecryptWithKey(byte[] payload, byte[] key, ReadOnlySpan<byte> associatedData)
    {
        var cipherLength = payload.Length - NonceSizeBytes - TagSizeBytes;
        var nonce = payload.AsSpan(0, NonceSizeBytes);
        var ciphertext = payload.AsSpan(NonceSizeBytes, cipherLength);
        var tag = payload.AsSpan(NonceSizeBytes + cipherLength, TagSizeBytes);

        var plaintextBytes = new byte[cipherLength];
        using var aes = new AesGcm(key, TagSizeBytes);
        aes.Decrypt(nonce, ciphertext, tag, plaintextBytes, associatedData);

        return Encoding.UTF8.GetString(plaintextBytes);
    }

    private static void ValidateUserId(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id cannot be empty when protecting or unprotecting user-bound secrets.", nameof(userId));
        }
    }

    private sealed record EncryptionKeysDocument(
        [property: JsonPropertyName("active")] string? Active,
        [property: JsonPropertyName("keys")] Dictionary<string, string>? Keys);

    private sealed record KeyRingSnapshot(
        string ActiveKeyId,
        IReadOnlyDictionary<string, byte[]> Keys);
}
