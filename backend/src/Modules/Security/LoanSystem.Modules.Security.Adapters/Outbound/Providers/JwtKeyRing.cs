using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LoanSystem.Common.Core.Ports.Outbound;
using LoanSystem.Modules.Security.Core.Contracts;
using LoanSystem.Modules.Security.Core.Ports.Outbound;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using Microsoft.IdentityModel.Tokens;

namespace LoanSystem.Modules.Security.Adapters.Outbound.Providers;

internal enum JwtHealthState
{
    Healthy,
    Degraded,
    Unhealthy
}

internal sealed record JwtKeyRingHealthStatus(
    JwtHealthState State,
    string Description,
    string? ActiveKid,
    DateTimeOffset? ActiveKeyExpiresAt,
    DateTimeOffset? NextKeyStartsAt,
    int ValidationKeyCount);

/// <summary>
/// Manages RS256 signing and validation keys with hot reload, retention windows for rotation,
/// and deterministic RFC 7638 JWK SHA-256 thumbprint key IDs (BE-SEC-10).
/// </summary>
internal interface IJwtKeyRing
{
    void EnsureInitialized();
    SigningCredentials GetSigningCredentials();
    IReadOnlyCollection<SecurityKey> ResolveValidationKeys(string? kid);
    JwtKeyRingHealthStatus GetStatus();
}

internal sealed class JwtKeyRing : IJwtKeyRing, IPublicSigningKeysProvider, IDisposable
{
    private const int DefaultAccessTokenMinutes = 30;
    private const int DefaultClockSkewSeconds = 60;
    private const int DefaultMinRsaKeySizeBits = 2048;
    private const int DefaultRotationWarningDays = 14;
    private const int EphemeralDevKeySizeBits = 3072;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IConfiguration _configuration;
    private readonly IClock _clock;
    private readonly IHostEnvironment? _environment;
    private readonly ILogger<JwtKeyRing> _logger;
    private readonly Lock _reloadLock = new();

    private volatile KeyRingSnapshot? _snapshot;
    private volatile string? _lastReloadError;
    private IDisposable? _reloadRegistration;
    private bool _disposed;

    public JwtKeyRing(
        IConfiguration configuration,
        IClock clock,
        IHostEnvironment? environment = null,
        ILogger<JwtKeyRing>? logger = null)
    {
        _configuration = configuration;
        _clock = clock;
        _environment = environment;
        _logger = logger ?? NullLogger<JwtKeyRing>.Instance;
    }

    public void EnsureInitialized()
    {
        if (_snapshot is not null)
        {
            return;
        }

        lock (_reloadLock)
        {
            if (_snapshot is not null)
            {
                return;
            }

            _snapshot = BuildInitialSnapshot();
            _lastReloadError = null;
            _reloadRegistration ??= ChangeToken.OnChange(
                () => _configuration.GetReloadToken(),
                ReloadOnConfigurationChange);
        }
    }

    public SigningCredentials GetSigningCredentials()
    {
        EnsureInitialized();
        var snapshot = _snapshot!;
        var now = _clock.UtcNow;

        var active = SelectActiveKey(snapshot.Entries, now);
        if (active is null)
        {
            throw new InvalidOperationException(
                "No active RS256 signing key is currently valid for the present UTC time.");
        }

        return active.SigningCredentials;
    }

    public IReadOnlyCollection<SecurityKey> ResolveValidationKeys(string? kid)
    {
        if (string.IsNullOrWhiteSpace(kid))
        {
            return Array.Empty<SecurityKey>();
        }

        EnsureInitialized();
        var snapshot = _snapshot!;
        var now = _clock.UtcNow;

        foreach (var entry in snapshot.Entries)
        {
            if (IsWithinValidationWindow(entry, snapshot, now) &&
                string.Equals(entry.Kid, kid, StringComparison.Ordinal))
            {
                return [entry.ValidationKey];
            }
        }

        return Array.Empty<SecurityKey>();
    }

    public IReadOnlyList<JwkDto> GetValidationKeys()
    {
        EnsureInitialized();
        var snapshot = _snapshot!;
        var now = _clock.UtcNow;

        return snapshot.Entries
            .Where(entry => IsWithinValidationWindow(entry, snapshot, now))
            .Select(entry => entry.PublicJwk)
            .ToArray();
    }

    public JwtKeyRingHealthStatus GetStatus()
    {
        EnsureInitialized();
        var snapshot = _snapshot!;
        var now = _clock.UtcNow;

        var validationCount = snapshot.Entries.Count(entry => IsWithinValidationWindow(entry, snapshot, now));
        var active = SelectActiveKey(snapshot.Entries, now);
        var nextKeyStartsAt = snapshot.Entries
            .Where(entry => entry.NotBefore > now)
            .OrderBy(entry => entry.NotBefore)
            .Select(entry => (DateTimeOffset?)entry.NotBefore)
            .FirstOrDefault();

        if (active is null)
        {
            return new JwtKeyRingHealthStatus(
                JwtHealthState.Unhealthy,
                "No active RS256 signing key is available.",
                ActiveKid: null,
                ActiveKeyExpiresAt: null,
                NextKeyStartsAt: nextKeyStartsAt,
                ValidationKeyCount: validationCount);
        }

        var reloadError = _lastReloadError;
        if (reloadError is not null)
        {
            return new JwtKeyRingHealthStatus(
                JwtHealthState.Degraded,
                "Last JWT key ring reload failed; retaining previous valid key snapshot.",
                ActiveKid: active.Kid,
                ActiveKeyExpiresAt: active.NotAfter,
                NextKeyStartsAt: nextKeyStartsAt,
                ValidationKeyCount: validationCount);
        }

        if (snapshot.RotationWarningWindow > TimeSpan.Zero &&
            active.NotAfter - now < snapshot.RotationWarningWindow)
        {
            return new JwtKeyRingHealthStatus(
                JwtHealthState.Degraded,
                $"Active signing key expires in less than {snapshot.RotationWarningWindow.TotalDays:0} days.",
                ActiveKid: active.Kid,
                ActiveKeyExpiresAt: active.NotAfter,
                NextKeyStartsAt: nextKeyStartsAt,
                ValidationKeyCount: validationCount);
        }

        return new JwtKeyRingHealthStatus(
            JwtHealthState.Healthy,
            "Active RS256 signing key is healthy.",
            ActiveKid: active.Kid,
            ActiveKeyExpiresAt: active.NotAfter,
            NextKeyStartsAt: nextKeyStartsAt,
            ValidationKeyCount: validationCount);
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

    private KeyRingSnapshot BuildInitialSnapshot()
    {
        var rawJson = _configuration["Jwt:SigningKeys"];
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            if (_environment?.IsDevelopment() == true)
            {
                return BuildEphemeralDevelopmentSnapshot();
            }

            throw new InvalidOperationException(
                "Configuration value 'Jwt:SigningKeys' is required outside the Development environment.");
        }

        return ParseSnapshot(rawJson);
    }

    private void ReloadOnConfigurationChange()
    {
        if (_disposed)
        {
            return;
        }

        lock (_reloadLock)
        {
            if (_disposed)
            {
                return;
            }

            var rawJson = _configuration["Jwt:SigningKeys"];
            if (string.IsNullOrWhiteSpace(rawJson))
            {
                if (_environment?.IsDevelopment() == true && _snapshot is { IsEphemeralDevelopment: true })
                {
                    return;
                }

                _lastReloadError = "Configuration value 'Jwt:SigningKeys' is missing after reload.";
                _logger.LogError(
                    "JWT key ring reload failed: 'Jwt:SigningKeys' is missing or empty. Keeping previous snapshot.");
                return;
            }

            try
            {
                var next = ParseSnapshot(rawJson);
                // Previous RSA instances are intentionally NOT disposed to avoid ObjectDisposedException
                // for concurrent signing/validation operations in flight during reload.
                _snapshot = next;
                _lastReloadError = null;
                _logger.LogInformation(
                    "Reloaded JWT key ring with {KeyCount} key(s).",
                    next.Entries.Count);
            }
            catch (Exception ex)
            {
                _lastReloadError = ex.Message;
                _logger.LogError(
                    ex,
                    "JWT key ring reload failed; retaining previous valid key snapshot.");
            }
        }
    }

    private KeyRingSnapshot BuildEphemeralDevelopmentSnapshot()
    {
        var now = _clock.UtcNow;
        var rsa = RSA.Create(EphemeralDevKeySizeBits);
        var entry = CreateKeyEntry(rsa, now.AddMinutes(-1), now.AddYears(1));

        _logger.LogWarning(
            "Configuration 'Jwt:SigningKeys' is not configured. Generated ephemeral RS256 signing key '{Kid}' for Development use only.",
            entry.Kid);

        return new KeyRingSnapshot(
            [entry],
            ReadOptions(),
            IsEphemeralDevelopment: true);
    }

    private KeyRingSnapshot ParseSnapshot(string rawJson)
    {
        var options = ReadOptions();

        SigningKeysDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<SigningKeysDocument>(rawJson, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Configuration value 'Jwt:SigningKeys' is not valid JSON.", ex);
        }

        if (document?.Keys is null || document.Keys.Count == 0)
        {
            throw new InvalidOperationException("Configuration value 'Jwt:SigningKeys' must contain at least one key.");
        }

        var entries = new List<KeyEntry>(document.Keys.Count);
        var seenKids = new HashSet<string>(StringComparer.Ordinal);

        foreach (var item in document.Keys)
        {
            if (string.IsNullOrWhiteSpace(item.PrivateKeyPem))
            {
                throw new InvalidOperationException("Each entry in 'Jwt:SigningKeys' must define a non-empty 'privateKeyPem'.");
            }

            if (item.NotBefore is null || item.NotAfter is null)
            {
                throw new InvalidOperationException("Each entry in 'Jwt:SigningKeys' must define both 'notBefore' and 'notAfter'.");
            }

            var notBefore = item.NotBefore.Value.ToUniversalTime();
            var notAfter = item.NotAfter.Value.ToUniversalTime();
            if (notAfter <= notBefore)
            {
                throw new InvalidOperationException("Each entry in 'Jwt:SigningKeys' must satisfy notAfter > notBefore.");
            }

            var rsa = ParsePkcs8RsaPrivateKey(item.PrivateKeyPem, options.MinRsaKeySizeBits);
            KeyEntry entry;
            try
            {
                entry = CreateKeyEntry(rsa, notBefore, notAfter);
            }
            catch
            {
                rsa.Dispose();
                throw;
            }

            if (!seenKids.Add(entry.Kid))
            {
                rsa.Dispose();
                throw new InvalidOperationException($"Duplicate RSA key id '{entry.Kid}' found in 'Jwt:SigningKeys'.");
            }

            entries.Add(entry);
        }

        var ordered = entries
            .OrderByDescending(e => e.NotBefore)
            .ThenBy(e => e.Kid, StringComparer.Ordinal)
            .ToArray();

        if (SelectActiveKey(ordered, _clock.UtcNow) is null)
        {
            throw new InvalidOperationException(
                "No key in 'Jwt:SigningKeys' is currently active (notBefore <= UtcNow < notAfter).");
        }

        return new KeyRingSnapshot(ordered, options, IsEphemeralDevelopment: false);
    }

    private static RSA ParsePkcs8RsaPrivateKey(string pem, int minKeySizeBits)
    {
        var pemSpan = pem.AsSpan();
        if (!PemEncoding.TryFind(pemSpan, out var fields) ||
            !pemSpan[fields.Label].SequenceEqual("PRIVATE KEY"))
        {
            throw new InvalidOperationException(
                "JWT signing key PEM must be a PKCS#8 private key ('BEGIN PRIVATE KEY').");
        }

        var rsa = RSA.Create();
        try
        {
            rsa.ImportFromPem(pem);
            if (rsa.KeySize < minKeySizeBits)
            {
                throw new InvalidOperationException(
                    $"RSA signing key size ({rsa.KeySize} bits) is below the configured minimum of {minKeySizeBits} bits.");
            }

            // Ensure private exponent/CRT parameters are present and valid.
            _ = rsa.ExportParameters(includePrivateParameters: true);
            return rsa;
        }
        catch
        {
            rsa.Dispose();
            throw;
        }
    }

    private static KeyEntry CreateKeyEntry(RSA rsa, DateTimeOffset notBefore, DateTimeOffset notAfter)
    {
        var publicParams = rsa.ExportParameters(includePrivateParameters: false);
        var e = Base64UrlEncoder.Encode(publicParams.Exponent!);
        var n = Base64UrlEncoder.Encode(publicParams.Modulus!);

        // RFC 7638 JWK SHA-256 thumbprint for RSA: lexicographically ordered members {"e","kty","n"}.
        var canonicalJwk = $"{{\"e\":\"{e}\",\"kty\":\"RSA\",\"n\":\"{n}\"}}";
        var thumbprint = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalJwk));
        var kid = Base64UrlEncoder.Encode(thumbprint);

        var signingKey = new RsaSecurityKey(rsa) { KeyId = kid };
        var signingCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.RsaSha256);

        // Validation key uses public-only parameters so verification paths never touch private material.
        var validationKey = new RsaSecurityKey(publicParams) { KeyId = kid };
        var jwkDto = new JwkDto("RSA", "sig", SecurityAlgorithms.RsaSha256, kid, n, e);

        return new KeyEntry(kid, notBefore, notAfter, signingCredentials, validationKey, jwkDto);
    }

    private JwtKeyOptions ReadOptions()
    {
        var ttlMinutes = ReadPositiveInt("Jwt:AccessTokenMinutes", DefaultAccessTokenMinutes, min: 1);
        var clockSkewSeconds = ReadPositiveInt("Jwt:ClockSkewSeconds", DefaultClockSkewSeconds, min: 0);
        var minRsaBits = ReadPositiveInt("Jwt:MinRsaKeySizeBits", DefaultMinRsaKeySizeBits, min: DefaultMinRsaKeySizeBits);
        var rotationWarningDays = ReadPositiveInt("Jwt:RotationWarningDays", DefaultRotationWarningDays, min: 0);

        return new JwtKeyOptions(
            TimeSpan.FromMinutes(ttlMinutes),
            TimeSpan.FromSeconds(clockSkewSeconds),
            minRsaBits,
            TimeSpan.FromDays(rotationWarningDays));
    }

    private int ReadPositiveInt(string key, int defaultValue, int min)
    {
        var raw = _configuration[key];
        if (string.IsNullOrWhiteSpace(raw))
        {
            return defaultValue;
        }

        if (!int.TryParse(raw, out var value) || value < min)
        {
            throw new InvalidOperationException(
                $"Configuration value '{key}' must be an integer >= {min}.");
        }

        return value;
    }

    private static KeyEntry? SelectActiveKey(IReadOnlyList<KeyEntry> entries, DateTimeOffset now)
    {
        KeyEntry? selected = null;
        foreach (var entry in entries)
        {
            if (entry.NotBefore <= now && now < entry.NotAfter)
            {
                if (selected is null || entry.NotBefore > selected.NotBefore)
                {
                    selected = entry;
                }
            }
        }

        return selected;
    }

    private static bool IsWithinValidationWindow(KeyEntry entry, KeyRingSnapshot snapshot, DateTimeOffset now)
    {
        var retentionEnd = entry.NotAfter + snapshot.AccessTokenTtl + snapshot.ClockSkew;
        return now <= retentionEnd;
    }

    private sealed record SigningKeysDocument(
        [property: JsonPropertyName("keys")] IReadOnlyList<SigningKeyJsonItem>? Keys);

    private sealed record SigningKeyJsonItem(
        [property: JsonPropertyName("privateKeyPem")] string? PrivateKeyPem,
        [property: JsonPropertyName("notBefore")] DateTimeOffset? NotBefore,
        [property: JsonPropertyName("notAfter")] DateTimeOffset? NotAfter);

    private sealed record JwtKeyOptions(
        TimeSpan AccessTokenTtl,
        TimeSpan ClockSkew,
        int MinRsaKeySizeBits,
        TimeSpan RotationWarningWindow);

    private sealed record KeyEntry(
        string Kid,
        DateTimeOffset NotBefore,
        DateTimeOffset NotAfter,
        SigningCredentials SigningCredentials,
        SecurityKey ValidationKey,
        JwkDto PublicJwk);

    private sealed record KeyRingSnapshot(
        IReadOnlyList<KeyEntry> Entries,
        JwtKeyOptions Options,
        bool IsEphemeralDevelopment)
    {
        public TimeSpan AccessTokenTtl => Options.AccessTokenTtl;
        public TimeSpan ClockSkew => Options.ClockSkew;
        public TimeSpan RotationWarningWindow => Options.RotationWarningWindow;
    }
}
