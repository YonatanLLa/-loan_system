using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Primitives;

namespace LoanSystem.Common.Adapters.Configuration;

/// <summary>
/// Configures pluggable secret sources (<c>None</c>, <c>KeyPerFile</c>) and validates mandatory
/// runtime secrets before the application starts accepting traffic outside Development (BE-SEC-10).
/// </summary>
public static class SecretManagerExtensions
{
    private static readonly TimeSpan DefaultReloadInterval = TimeSpan.FromMinutes(5);

    private static readonly string[] ForbiddenDevelopmentFragments =
    [
        "postgrespassword",
        "SecretKeyForJwtSigningShouldBeReplacedInProductionEnvironment12345",
        "REPLACE_IN_PROD",
        "changeme"
    ];

    public static IConfigurationBuilder AddSecretManager(
        this IConfigurationBuilder builder,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(environment);

        var intermediate = builder.Build();
        var provider = intermediate["Secrets:Provider"];

        if (string.IsNullOrWhiteSpace(provider) ||
            string.Equals(provider, "None", StringComparison.OrdinalIgnoreCase))
        {
            return builder;
        }

        if (!string.Equals(provider, "KeyPerFile", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Unsupported 'Secrets:Provider' value '{provider}'. Supported values are 'None' and 'KeyPerFile'.");
        }

        var rawPath = intermediate["Secrets:Path"];
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            throw new InvalidOperationException(
                "Configuration value 'Secrets:Path' is required when 'Secrets:Provider' is 'KeyPerFile'.");
        }

        var fullPath = Path.GetFullPath(rawPath);
        if (!Directory.Exists(fullPath))
        {
            if (environment.IsDevelopment())
            {
                return builder;
            }

            throw new DirectoryNotFoundException(
                $"Configured secrets directory '{fullPath}' does not exist.");
        }

        var reloadInterval = ParseReloadInterval(intermediate["Secrets:ReloadInterval"]);

        builder.AddKeyPerFile(source =>
        {
            source.Optional = environment.IsDevelopment();
            source.ReloadOnChange = true;
            source.SectionDelimiter = "__";
            source.FileProvider = reloadInterval > TimeSpan.Zero
                ? new PollingKeyPerFileProvider(fullPath, reloadInterval)
                : new PhysicalFileProvider(fullPath);
        });

        return builder;
    }

    public static void ValidateStartupSecrets(
        this IConfiguration configuration,
        IHostEnvironment environment,
        bool requireHttpSecuritySecrets = true,
        bool requireDatabaseConnection = true)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        if (environment.IsDevelopment())
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(configuration["Jwt:Key"]))
        {
            throw new InvalidOperationException(
                "Legacy symmetric 'Jwt:Key' is forbidden outside Development. Configure RS256 'Jwt:SigningKeys' instead.");
        }

        var dbConnection = configuration["ConnectionStrings:Database"];
        if (requireDatabaseConnection && string.IsNullOrWhiteSpace(dbConnection))
        {
            throw new InvalidOperationException(
                "Configuration value 'ConnectionStrings:Database' is required outside Development.");
        }

        EnsureNoForbiddenPlaceholder(dbConnection, "ConnectionStrings:Database");
        EnsureNoForbiddenPlaceholder(configuration["ConnectionStrings:Redis"], "ConnectionStrings:Redis");

        if (!requireHttpSecuritySecrets)
        {
            return;
        }

        var signingKeys = configuration["Jwt:SigningKeys"];
        if (string.IsNullOrWhiteSpace(signingKeys))
        {
            throw new InvalidOperationException(
                "Configuration value 'Jwt:SigningKeys' is required outside Development.");
        }

        EnsureNoForbiddenPlaceholder(signingKeys, "Jwt:SigningKeys");

        var mfaKeys = configuration["Security:Mfa:EncryptionKeys"];
        if (string.IsNullOrWhiteSpace(mfaKeys))
        {
            throw new InvalidOperationException(
                "Configuration value 'Security:Mfa:EncryptionKeys' is required outside Development.");
        }

        EnsureNoForbiddenPlaceholder(mfaKeys, "Security:Mfa:EncryptionKeys");
        ValidateMfaKeyRingFormat(mfaKeys);
    }

    private static TimeSpan ParseReloadInterval(string? rawInterval)
    {
        if (string.IsNullOrWhiteSpace(rawInterval))
        {
            return DefaultReloadInterval;
        }

        if (!TimeSpan.TryParse(rawInterval, CultureInfo.InvariantCulture, out var interval) ||
            interval < TimeSpan.Zero)
        {
            throw new InvalidOperationException(
                "Configuration value 'Secrets:ReloadInterval' must be a non-negative TimeSpan (e.g. '00:05:00').");
        }

        return interval;
    }

    private static void EnsureNoForbiddenPlaceholder(string? value, string keyName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        foreach (var forbidden in ForbiddenDevelopmentFragments)
        {
            if (value.Contains(forbidden, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Configuration value '{keyName}' contains a forbidden development placeholder outside Development.");
            }
        }
    }

    private static void ValidateMfaKeyRingFormat(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("active", out var activeProp) ||
                activeProp.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(activeProp.GetString()))
            {
                throw new InvalidOperationException(
                    "Configuration value 'Security:Mfa:EncryptionKeys' must define a non-empty 'active' key id.");
            }

            var activeId = activeProp.GetString()!;
            if (!doc.RootElement.TryGetProperty("keys", out var keysProp) ||
                keysProp.ValueKind != JsonValueKind.Object ||
                !keysProp.TryGetProperty(activeId, out var activeKeyProp) ||
                activeKeyProp.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(activeKeyProp.GetString()))
            {
                throw new InvalidOperationException(
                    $"Configuration value 'Security:Mfa:EncryptionKeys' must include key material for active key '{activeId}'.");
            }
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                "Configuration value 'Security:Mfa:EncryptionKeys' is not valid JSON.", ex);
        }
    }

    /// <summary>
    /// Wraps <see cref="PhysicalFileProvider"/> with periodic content hashing so secret rotation works
    /// both immediately via filesystem watchers and on symlink-swapped volumes (e.g. Kubernetes Secrets).
    /// </summary>
    private sealed class PollingKeyPerFileProvider : IFileProvider, IDisposable
    {
        private readonly string _rootPath;
        private readonly PhysicalFileProvider _inner;
        private readonly Timer _timer;
        private readonly Lock _gate = new();

        private CancellationTokenSource _pollCts = new();
        private string _lastFingerprint;
        private bool _disposed;

        public PollingKeyPerFileProvider(string rootPath, TimeSpan reloadInterval)
        {
            _rootPath = rootPath;
            _inner = new PhysicalFileProvider(rootPath);
            _lastFingerprint = ComputeFingerprint(rootPath);
            _timer = new Timer(PollForChanges, state: null, reloadInterval, reloadInterval);
        }

        public IDirectoryContents GetDirectoryContents(string subpath) => _inner.GetDirectoryContents(subpath);

        public IFileInfo GetFileInfo(string subpath) => _inner.GetFileInfo(subpath);

        public IChangeToken Watch(string filter)
        {
            var innerToken = _inner.Watch(filter);
            CancellationChangeToken pollToken;
            lock (_gate)
            {
                pollToken = new CancellationChangeToken(_pollCts.Token);
            }

            return new CompositeChangeToken([innerToken, pollToken]);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _timer.Dispose();
            _inner.Dispose();

            lock (_gate)
            {
                _pollCts.Dispose();
            }
        }

        private void PollForChanges(object? state)
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                var currentFingerprint = ComputeFingerprint(_rootPath);
                CancellationTokenSource? previousCts = null;

                lock (_gate)
                {
                    if (_disposed || string.Equals(currentFingerprint, _lastFingerprint, StringComparison.Ordinal))
                    {
                        return;
                    }

                    _lastFingerprint = currentFingerprint;
                    previousCts = _pollCts;
                    _pollCts = new CancellationTokenSource();
                }

                previousCts.Cancel();
                previousCts.Dispose();
            }
            catch
            {
                // Ignore transient I/O errors during concurrent atomic symlink swaps; next poll retries cleanly.
            }
        }

        private static string ComputeFingerprint(string rootPath)
        {
            if (!Directory.Exists(rootPath))
            {
                return string.Empty;
            }

            var files = Directory.EnumerateFiles(rootPath)
                .Where(path =>
                {
                    var name = Path.GetFileName(path);
                    return !name.StartsWith(".", StringComparison.Ordinal) &&
                           !name.StartsWith("ignore.", StringComparison.OrdinalIgnoreCase);
                })
                .OrderBy(path => path, StringComparer.Ordinal);

            using var incrementalHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            foreach (var file in files)
            {
                var nameBytes = Encoding.UTF8.GetBytes(Path.GetFileName(file));
                incrementalHash.AppendData(nameBytes);
                incrementalHash.AppendData([0]);

                var content = File.ReadAllBytes(file);
                incrementalHash.AppendData(content);
                incrementalHash.AppendData([0]);
            }

            return Convert.ToHexString(incrementalHash.GetHashAndReset());
        }
    }
}
