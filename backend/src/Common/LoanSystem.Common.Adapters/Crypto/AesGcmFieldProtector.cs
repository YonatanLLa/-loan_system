using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;

namespace LoanSystem.Common.Adapters.Crypto;

/// <summary>
/// Authenticated encryption (AES-256-GCM) with versioned key ring rotation and deterministic
/// HMAC-SHA256 hashing for sensitive domain fields (e.g., customer bank accounts/CCI, BE-CUS-09, D4).
/// Ciphertext format: <c>v1:{keyId}:{base64(nonce12|ciphertext|tag16)}</c>.
/// </summary>
public sealed class AesGcmFieldProtector : IDisposable
{
    private const string Prefix = "v1:";
    private const string DefaultKeyId = "k1";
    private const int KeySizeBytes = 32;
    private const int NonceSizeBytes = 12;
    private const int TagSizeBytes = 16;
    private const int MinHmacKeyBytes = 32;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IConfiguration? _configuration;
    private readonly IHostEnvironment? _environment;
    private readonly string _sectionPath;
    private readonly ILogger<AesGcmFieldProtector> _logger;
    private readonly Lock _reloadLock = new();

    private volatile KeyRingSnapshot? _snapshot;
    private IDisposable? _reloadRegistration;
    private bool _disposed;

    public AesGcmFieldProtector(
        IConfiguration configuration,
        IHostEnvironment environment,
        string sectionPath = "Customers:FieldProtection",
        ILogger<AesGcmFieldProtector>? logger = null)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        _sectionPath = string.IsNullOrWhiteSpace(sectionPath)
            ? throw new ArgumentException("Section path is required.", nameof(sectionPath))
            : sectionPath;
        _logger = logger ?? NullLogger<AesGcmFieldProtector>.Instance;
    }

    public AesGcmFieldProtector(
        byte[] cipherKey,
        byte[] hmacKey,
        string keyId = DefaultKeyId)
        : this(keyId, new Dictionary<string, byte[]>(StringComparer.Ordinal) { [keyId] = cipherKey }, hmacKey)
    {
    }

    public AesGcmFieldProtector(
        string activeKeyId,
        IReadOnlyDictionary<string, byte[]> cipherKeys,
        byte[] hmacKey)
    {
        _sectionPath = string.Empty;
        _logger = NullLogger<AesGcmFieldProtector>.Instance;
        _snapshot = BuildValidatedSnapshot(activeKeyId, cipherKeys, hmacKey);
    }

    public string Protect(string plaintext, string? context = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(plaintext);

        var snapshot = EnsureInitialized();
        var key = snapshot.CipherKeys[snapshot.ActiveKeyId];

        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var payload = new byte[NonceSizeBytes + plaintextBytes.Length + TagSizeBytes];

        var nonce = payload.AsSpan(0, NonceSizeBytes);
        var ciphertext = payload.AsSpan(NonceSizeBytes, plaintextBytes.Length);
        var tag = payload.AsSpan(NonceSizeBytes + plaintextBytes.Length, TagSizeBytes);

        RandomNumberGenerator.Fill(nonce);

        var aad = string.IsNullOrEmpty(context)
            ? Array.Empty<byte>()
            : Encoding.UTF8.GetBytes(context);

        using var aes = new AesGcm(key, TagSizeBytes);
        aes.Encrypt(nonce, plaintextBytes, ciphertext, tag, aad);

        return $"{Prefix}{snapshot.ActiveKeyId}:{Convert.ToBase64String(payload)}";
    }

    public string Unprotect(string protectedValue, string? context = null)
    {
        if (string.IsNullOrWhiteSpace(protectedValue) || !protectedValue.StartsWith(Prefix, StringComparison.Ordinal))
        {
            throw new CryptographicException("Unsupported protected field format.");
        }

        var snapshot = EnsureInitialized();

        var remainder = protectedValue.AsSpan(Prefix.Length);
        var separator = remainder.IndexOf(':');
        if (separator <= 0 || separator == remainder.Length - 1)
        {
            throw new CryptographicException("Malformed protected field envelope.");
        }

        var keyId = remainder[..separator].ToString();
        if (!snapshot.CipherKeys.TryGetValue(keyId, out var key))
        {
            throw new CryptographicException($"Unknown field protection key id '{keyId}'.");
        }

        byte[] payload;
        try
        {
            payload = Convert.FromBase64String(remainder[(separator + 1)..].ToString());
        }
        catch (FormatException ex)
        {
            throw new CryptographicException("Protected field payload is not valid base64.", ex);
        }

        if (payload.Length < NonceSizeBytes + TagSizeBytes + 1)
        {
            throw new CryptographicException("Protected field payload is truncated.");
        }

        var cipherLen = payload.Length - NonceSizeBytes - TagSizeBytes;
        var nonce = payload.AsSpan(0, NonceSizeBytes);
        var ciphertext = payload.AsSpan(NonceSizeBytes, cipherLen);
        var tag = payload.AsSpan(NonceSizeBytes + cipherLen, TagSizeBytes);
        var plaintextBytes = new byte[cipherLen];

        var aad = string.IsNullOrEmpty(context)
            ? Array.Empty<byte>()
            : Encoding.UTF8.GetBytes(context);

        using var aes = new AesGcm(key, TagSizeBytes);
        aes.Decrypt(nonce, ciphertext, tag, plaintextBytes, aad);

        return Encoding.UTF8.GetString(plaintextBytes);
    }

    public string Hash(string plaintext)
    {
        ArgumentException.ThrowIfNullOrEmpty(plaintext);

        var snapshot = EnsureInitialized();
        var inputBytes = Encoding.UTF8.GetBytes(plaintext);
        var mac = HMACSHA256.HashData(snapshot.HmacKey, inputBytes);
        return Convert.ToHexString(mac).ToLowerInvariant();
    }

    private KeyRingSnapshot EnsureInitialized()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var current = _snapshot;
        if (current is not null)
        {
            return current;
        }

        lock (_reloadLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_snapshot is not null)
            {
                return _snapshot;
            }

            _snapshot = LoadFromConfiguration();
            if (_configuration is not null)
            {
                _reloadRegistration = ChangeToken.OnChange(
                    _configuration.GetReloadToken,
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
                _snapshot = LoadFromConfiguration();
                _logger.LogInformation("Reloaded field protection keys for section '{SectionPath}'.", _sectionPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reload field protection keys for '{SectionPath}'; keeping active keys.", _sectionPath);
            }
        }
    }

    private KeyRingSnapshot LoadFromConfiguration()
    {
        var section = _configuration!.GetSection(_sectionPath);
        var rawRing = section["Keys"];
        var rawSingleKey = section["Key"];
        var rawHmacKey = section["HmacKey"];

        string activeKeyId;
        IReadOnlyDictionary<string, byte[]> cipherKeys;

        if (!string.IsNullOrWhiteSpace(rawRing))
        {
            var doc = JsonSerializer.Deserialize<KeyRingDocument>(rawRing, JsonOptions)
                ?? throw new InvalidOperationException($"Configuration value '{_sectionPath}:Keys' is not valid JSON.");

            if (string.IsNullOrWhiteSpace(doc.Active) || doc.Keys is null || doc.Keys.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Configuration value '{_sectionPath}:Keys' must define 'active' and a non-empty 'keys' map.");
            }

            activeKeyId = doc.Active;
            var map = new Dictionary<string, byte[]>(doc.Keys.Count, StringComparer.Ordinal);
            foreach (var (id, encoded) in doc.Keys)
            {
                map[id] = DecodeCipherKey(encoded, id);
            }

            cipherKeys = map;
        }
        else if (!string.IsNullOrWhiteSpace(rawSingleKey))
        {
            activeKeyId = DefaultKeyId;
            cipherKeys = new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                [DefaultKeyId] = DecodeCipherKey(rawSingleKey, DefaultKeyId)
            };
        }
        else if (_environment is not null && _environment.IsDevelopment())
        {
            // Development-only deterministic fallback derived from runtime workspace path (never used outside Development)
            activeKeyId = "dev-local";
            var devSeed = Encoding.UTF8.GetBytes($"{_environment.ApplicationName}|{_environment.ContentRootPath}|{_sectionPath}");
            cipherKeys = new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                [activeKeyId] = SHA256.HashData(devSeed)
            };
            _logger.LogWarning(
                "Using Development-only derived cipher key for '{SectionPath}'. Configure '{SectionPath}:Key' in production.",
                _sectionPath,
                _sectionPath);
        }
        else
        {
            throw new InvalidOperationException(
                $"Configuration value '{_sectionPath}:Key' (or '{_sectionPath}:Keys') is required outside Development.");
        }

        byte[] hmacKeyBytes;
        if (!string.IsNullOrWhiteSpace(rawHmacKey))
        {
            hmacKeyBytes = DecodeHmacKey(rawHmacKey);
        }
        else if (_environment is not null && _environment.IsDevelopment())
        {
            var devHmacSeed = Encoding.UTF8.GetBytes($"{_environment.ApplicationName}|{_environment.ContentRootPath}|{_sectionPath}|hmac");
            hmacKeyBytes = SHA256.HashData(devHmacSeed);
        }
        else
        {
            throw new InvalidOperationException(
                $"Configuration value '{_sectionPath}:HmacKey' is required outside Development.");
        }

        return BuildValidatedSnapshot(activeKeyId, cipherKeys, hmacKeyBytes);
    }

    private static byte[] DecodeCipherKey(string base64, string keyId)
    {
        byte[] decoded;
        try
        {
            decoded = Convert.FromBase64String(base64.Trim());
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException($"Field protection cipher key '{keyId}' must be valid base64.", ex);
        }

        if (decoded.Length != KeySizeBytes)
        {
            CryptographicOperations.ZeroMemory(decoded);
            throw new InvalidOperationException($"Field protection cipher key '{keyId}' must decode to {KeySizeBytes} bytes.");
        }

        return decoded;
    }

    private static byte[] DecodeHmacKey(string rawHmacKey)
    {
        var trimmed = rawHmacKey.Trim();
        try
        {
            var b64 = Convert.FromBase64String(trimmed);
            if (b64.Length >= MinHmacKeyBytes)
            {
                return b64;
            }
        }
        catch (FormatException)
        {
            // Fall through to UTF-8 raw key check
        }

        var utf8 = Encoding.UTF8.GetBytes(trimmed);
        if (utf8.Length < MinHmacKeyBytes)
        {
            throw new InvalidOperationException(
                $"Field protection HMAC key must be at least {MinHmacKeyBytes} bytes (256 bits).");
        }

        return utf8;
    }

    private static KeyRingSnapshot BuildValidatedSnapshot(
        string activeKeyId,
        IReadOnlyDictionary<string, byte[]> cipherKeys,
        byte[] hmacKey)
    {
        if (string.IsNullOrWhiteSpace(activeKeyId) || activeKeyId.Contains(':', StringComparison.Ordinal))
        {
            throw new ArgumentException("Active key id must be non-empty and not contain ':'.", nameof(activeKeyId));
        }

        ArgumentNullException.ThrowIfNull(cipherKeys);
        if (!cipherKeys.ContainsKey(activeKeyId))
        {
            throw new ArgumentException($"Active key id '{activeKeyId}' was not found in key ring.", nameof(activeKeyId));
        }

        ArgumentNullException.ThrowIfNull(hmacKey);
        if (hmacKey.Length < MinHmacKeyBytes)
        {
            throw new ArgumentException($"HMAC key must be at least {MinHmacKeyBytes} bytes.", nameof(hmacKey));
        }

        var copy = new Dictionary<string, byte[]>(cipherKeys.Count, StringComparer.Ordinal);
        foreach (var (id, key) in cipherKeys)
        {
            if (string.IsNullOrWhiteSpace(id) || id.Contains(':', StringComparison.Ordinal))
            {
                throw new ArgumentException($"Invalid key id '{id}'.", nameof(cipherKeys));
            }

            if (key is null || key.Length != KeySizeBytes)
            {
                throw new ArgumentException($"Cipher key '{id}' must be {KeySizeBytes} bytes.", nameof(cipherKeys));
            }

            copy[id] = key.ToArray();
        }

        return new KeyRingSnapshot(activeKeyId, copy, hmacKey.ToArray());
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _reloadRegistration?.Dispose();
        _reloadRegistration = null;

        if (_snapshot is not null)
        {
            foreach (var key in _snapshot.CipherKeys.Values)
            {
                CryptographicOperations.ZeroMemory(key);
            }

            CryptographicOperations.ZeroMemory(_snapshot.HmacKey);
            _snapshot = null;
        }
    }

    private sealed class KeyRingSnapshot(
        string activeKeyId,
        Dictionary<string, byte[]> cipherKeys,
        byte[] hmacKey)
    {
        public string ActiveKeyId { get; } = activeKeyId;
        public Dictionary<string, byte[]> CipherKeys { get; } = cipherKeys;
        public byte[] HmacKey { get; } = hmacKey;
    }

    private sealed class KeyRingDocument
    {
        [JsonPropertyName("active")]
        public string? Active { get; set; }

        [JsonPropertyName("keys")]
        public Dictionary<string, string>? Keys { get; set; }
    }
}
