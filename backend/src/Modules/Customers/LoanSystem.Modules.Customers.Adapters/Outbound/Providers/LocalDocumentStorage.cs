using System.Security.Cryptography;
using System.Text;
using LoanSystem.Common.Core.Ports.Outbound;
using LoanSystem.Modules.Customers.Core.Application;
using LoanSystem.Modules.Customers.Core.Ports.Outbound;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace LoanSystem.Modules.Customers.Adapters.Outbound.Providers;

internal sealed class LocalDocumentStorage : IDocumentStorage, ILocalSignedFileReader
{
    private const int StreamBufferSize = 81920;

    private readonly string _rootDirectory;
    private readonly long _maxFileSizeBytes;
    private readonly byte[] _urlSigningKey;
    private readonly IClock _clock;

    public LocalDocumentStorage(
        CustomersOptions options,
        IConfiguration configuration,
        IHostEnvironment environment,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(clock);

        _rootDirectory = Path.GetFullPath(options.Storage.LocalPath);
        _maxFileSizeBytes = options.Documents.MaxFileSizeBytes;
        _clock = clock;

        var configuredSecret = configuration["Customers:Storage:SigningSecret"]
            ?? configuration["Customers:FieldProtection:HmacKey"];

        if (!string.IsNullOrWhiteSpace(configuredSecret))
        {
            _urlSigningKey = SHA256.HashData(Encoding.UTF8.GetBytes($"local-url:{configuredSecret}"));
        }
        else if (environment.IsDevelopment() || string.Equals(environment.EnvironmentName, "Testing", StringComparison.OrdinalIgnoreCase))
        {
            _urlSigningKey = SHA256.HashData(
                Encoding.UTF8.GetBytes($"{environment.ApplicationName}:{environment.EnvironmentName}:local-doc-url"));
        }
        else
        {
            throw new InvalidOperationException(
                "Customers:Storage:SigningSecret (or Customers:FieldProtection:HmacKey) must be configured in non-development environments.");
        }
    }

    public async Task<StoredObject> PutAsync(
        string key,
        Stream content,
        string contentType,
        CancellationToken ct = default)
    {
        var normalizedKey = NormalizeSafeKey(key);
        var targetPath = ResolveSafePath(normalizedKey);
        var directory = Path.GetDirectoryName(targetPath)!;
        Directory.CreateDirectory(directory);

        var tempPath = Path.Combine(directory, $"{Guid.NewGuid():N}.tmp");
        try
        {
            long totalBytes = 0;
            using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[StreamBufferSize];

            await using (var fs = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, StreamBufferSize, useAsync: true))
            {
                while (true)
                {
                    var read = await content.ReadAsync(buffer.AsMemory(0, buffer.Length), ct);
                    if (read == 0)
                    {
                        break;
                    }

                    totalBytes += read;
                    if (totalBytes > _maxFileSizeBytes)
                    {
                        throw new InvalidOperationException($"Uploaded stream exceeded maximum allowed size of {_maxFileSizeBytes} bytes.");
                    }

                    hasher.AppendData(buffer, 0, read);
                    await fs.WriteAsync(buffer.AsMemory(0, read), ct);
                }

                await fs.FlushAsync(ct);
            }

            File.Move(tempPath, targetPath, overwrite: true);
            var hashHex = Convert.ToHexString(hasher.GetHashAndReset()).ToLowerInvariant();
            return new StoredObject(normalizedKey, hashHex, totalBytes);
        }
        catch
        {
            TryDeleteFile(tempPath);
            throw;
        }
    }

    public Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        var normalizedKey = NormalizeSafeKey(key);
        var targetPath = ResolveSafePath(normalizedKey);
        return Task.FromResult(File.Exists(targetPath));
    }

    public Task<Uri> GetSignedReadUrlAsync(string key, TimeSpan ttl, CancellationToken ct = default)
    {
        if (ttl <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(ttl));
        }

        var normalizedKey = NormalizeSafeKey(key);
        var expiresUnixMs = _clock.UtcNow.Add(ttl).ToUnixTimeMilliseconds();
        var token = CreateSignedToken(normalizedKey, expiresUnixMs);
        return Task.FromResult(new Uri($"/api/customers/files/{Uri.EscapeDataString(token)}", UriKind.Relative));
    }

    public Task<LocalSignedFile?> OpenSignedReadAsync(string token, CancellationToken ct = default)
    {
        if (!TryValidateToken(token, out var normalizedKey))
        {
            return Task.FromResult<LocalSignedFile?>(null);
        }

        var targetPath = ResolveSafePath(normalizedKey);
        if (!File.Exists(targetPath))
        {
            return Task.FromResult<LocalSignedFile?>(null);
        }

        var contentType = ResolveContentTypeFromPath(targetPath);
        Stream stream = new FileStream(targetPath, FileMode.Open, FileAccess.Read, FileShare.Read, StreamBufferSize, useAsync: true);
        return Task.FromResult<LocalSignedFile?>(new LocalSignedFile(stream, contentType, Path.GetFileName(targetPath)));
    }

    internal string CreateSignedToken(string normalizedKey, long expiresUnixMs)
    {
        var payload = $"{expiresUnixMs}|{normalizedKey}";
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var signature = HMACSHA256.HashData(_urlSigningKey, payloadBytes);
        return $"{WebEncoders.Base64UrlEncode(payloadBytes)}.{WebEncoders.Base64UrlEncode(signature)}";
    }

    internal bool TryValidateToken(string token, out string normalizedKey)
    {
        normalizedKey = string.Empty;
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var dotIndex = token.IndexOf('.');
        if (dotIndex <= 0 || dotIndex >= token.Length - 1)
        {
            return false;
        }

        try
        {
            var payloadBytes = WebEncoders.Base64UrlDecode(token[..dotIndex]);
            var providedSig = WebEncoders.Base64UrlDecode(token[(dotIndex + 1)..]);
            var expectedSig = HMACSHA256.HashData(_urlSigningKey, payloadBytes);

            if (!CryptographicOperations.FixedTimeEquals(providedSig, expectedSig))
            {
                return false;
            }

            var payloadText = Encoding.UTF8.GetString(payloadBytes);
            var sep = payloadText.IndexOf('|');
            if (sep <= 0 || sep >= payloadText.Length - 1)
            {
                return false;
            }

            if (!long.TryParse(payloadText[..sep], out var expiresUnixMs))
            {
                return false;
            }

            if (_clock.UtcNow.ToUnixTimeMilliseconds() > expiresUnixMs)
            {
                return false;
            }

            normalizedKey = NormalizeSafeKey(payloadText[(sep + 1)..]);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string NormalizeSafeKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Storage key cannot be empty.", nameof(key));
        }

        var cleaned = key.Trim().Replace('\\', '/');
        if (cleaned.StartsWith("local://", StringComparison.OrdinalIgnoreCase))
        {
            cleaned = cleaned["local://".Length..];
        }

        cleaned = cleaned.TrimStart('/');
        var segments = cleaned.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || segments.Any(s => s is "." or ".."))
        {
            throw new InvalidOperationException("Invalid storage key path.");
        }

        return string.Join('/', segments);
    }

    private string ResolveSafePath(string normalizedKey)
    {
        var relativeOs = normalizedKey.Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(_rootDirectory, relativeOs));
        var rootWithSep = _rootDirectory.EndsWith(Path.DirectorySeparatorChar)
            ? _rootDirectory
            : _rootDirectory + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(rootWithSep, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Resolved path escapes storage root.");
        }

        return fullPath;
    }

    private static string ResolveContentTypeFromPath(string path) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".pdf" => "application/pdf",
            _ => "application/octet-stream"
        };

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best-effort cleanup of temp file
        }
    }
}

internal sealed class NullLocalSignedFileReader : ILocalSignedFileReader
{
    public Task<LocalSignedFile?> OpenSignedReadAsync(string token, CancellationToken ct = default) =>
        Task.FromResult<LocalSignedFile?>(null);
}
