using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using LoanSystem.Common.Core.Ports.Outbound;
using LoanSystem.Modules.Customers.Core.Application;
using LoanSystem.Modules.Customers.Core.Ports.Outbound;
using Microsoft.Extensions.Configuration;

namespace LoanSystem.Modules.Customers.Adapters.Outbound.Providers;

/// <summary>
/// S3-compatible object storage adapter using AWS Signature Version 4 over HTTP
/// (supports AWS S3, MinIO, and Cloudflare R2 without third-party SDK dependencies).
/// </summary>
internal sealed class S3DocumentStorage : IDocumentStorage
{
    private const int BufferChunkSize = 81920;

    private readonly HttpClient _http;
    private readonly StorageOptions _storage;
    private readonly long _maxFileSizeBytes;
    private readonly IClock _clock;
    private readonly string _accessKey;
    private readonly string _secretKey;
    private readonly string? _serverSideEncryption;

    public S3DocumentStorage(
        HttpClient http,
        CustomersOptions options,
        IConfiguration configuration,
        IClock clock)
        : this(
            http,
            options.Storage,
            options.Documents.MaxFileSizeBytes,
            clock,
            configuration["Customers:Storage:AccessKey"] ?? string.Empty,
            configuration["Customers:Storage:SecretKey"] ?? string.Empty,
            configuration["Customers:Storage:ServerSideEncryption"])
    {
    }

    internal S3DocumentStorage(
        HttpClient http,
        StorageOptions storage,
        long maxFileSizeBytes,
        IClock clock,
        string accessKey,
        string secretKey,
        string? serverSideEncryption = null)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        _maxFileSizeBytes = maxFileSizeBytes;
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _accessKey = accessKey;
        _secretKey = secretKey;
        _serverSideEncryption = string.IsNullOrWhiteSpace(serverSideEncryption) ? null : serverSideEncryption.Trim();
    }

    public async Task<StoredObject> PutAsync(
        string key,
        Stream content,
        string contentType,
        CancellationToken ct = default)
    {
        var objectKey = NormalizeObjectKey(key);
        using var bufferStream = new MemoryStream();
        var chunk = new byte[BufferChunkSize];
        long totalBytes = 0;

        while (true)
        {
            var read = await content.ReadAsync(chunk.AsMemory(0, chunk.Length), ct);
            if (read == 0)
            {
                break;
            }

            totalBytes += read;
            if (totalBytes > _maxFileSizeBytes)
            {
                throw new InvalidOperationException($"Uploaded stream exceeded maximum allowed size of {_maxFileSizeBytes} bytes.");
            }

            bufferStream.Write(chunk, 0, read);
        }

        var payloadBytes = bufferStream.ToArray();
        var payloadSha256Hex = ToHexLower(SHA256.HashData(payloadBytes));
        var endpoint = BuildObjectUri(objectKey);
        var now = _clock.UtcNow;

        using var request = new HttpRequestMessage(HttpMethod.Put, endpoint)
        {
            Content = new ByteArrayContent(payloadBytes)
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        SignRequest(request, endpoint, payloadSha256Hex, now);
        if (_serverSideEncryption is not null)
        {
            request.Headers.TryAddWithoutValidation("x-amz-server-side-encryption", _serverSideEncryption);
            // Re-sign including SSE header when enabled
            request.Headers.Remove("Authorization");
            SignRequest(request, endpoint, payloadSha256Hex, now);
        }

        using var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        return new StoredObject($"s3://{_storage.Bucket}/{objectKey}", payloadSha256Hex, totalBytes);
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        var objectKey = NormalizeObjectKey(key);
        var endpoint = BuildObjectUri(objectKey);
        var now = _clock.UtcNow;
        var emptyHash = ToHexLower(SHA256.HashData([]));

        using var request = new HttpRequestMessage(HttpMethod.Head, endpoint);
        SignRequest(request, endpoint, emptyHash, now);

        using var response = await _http.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();
        return true;
    }

    public Task<Uri> GetSignedReadUrlAsync(string key, TimeSpan ttl, CancellationToken ct = default)
    {
        if (ttl <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(ttl));
        }

        var objectKey = NormalizeObjectKey(key);
        var endpoint = BuildObjectUri(objectKey);
        var now = _clock.UtcNow;
        var amzDate = now.ToUniversalTime().ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture);
        var dateStamp = now.ToUniversalTime().ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var region = string.IsNullOrWhiteSpace(_storage.Region) ? "us-east-1" : _storage.Region;
        var credentialScope = $"{dateStamp}/{region}/s3/aws4_request";
        var expiresSeconds = Math.Clamp((long)Math.Ceiling(ttl.TotalSeconds), 1L, 604800L);

        var hostHeader = endpoint.IsDefaultPort ? endpoint.Host : $"{endpoint.Host}:{endpoint.Port}";
        var queryParams = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["X-Amz-Algorithm"] = "AWS4-HMAC-SHA256",
            ["X-Amz-Credential"] = $"{_accessKey}/{credentialScope}",
            ["X-Amz-Date"] = amzDate,
            ["X-Amz-Expires"] = expiresSeconds.ToString(CultureInfo.InvariantCulture),
            ["X-Amz-SignedHeaders"] = "host"
        };

        var canonicalQuery = string.Join("&", queryParams.Select(kvp =>
            $"{AwsUrlEncode(kvp.Key)}={AwsUrlEncode(kvp.Value)}"));

        var canonicalUri = EncodePathSegments(endpoint.AbsolutePath);
        var canonicalHeaders = $"host:{hostHeader}\n";
        var canonicalRequest = $"GET\n{canonicalUri}\n{canonicalQuery}\n{canonicalHeaders}\nhost\nUNSIGNED-PAYLOAD";

        var stringToSign = $"AWS4-HMAC-SHA256\n{amzDate}\n{credentialScope}\n{ToHexLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalRequest)))}";
        var signingKey = DeriveSigningKey(_secretKey, dateStamp, region, "s3");
        var signature = ToHexLower(HmacSha256(signingKey, stringToSign));

        var presigned = $"{endpoint.GetLeftPart(UriPartial.Authority)}{endpoint.AbsolutePath}?{canonicalQuery}&X-Amz-Signature={signature}";
        return Task.FromResult(new Uri(presigned, UriKind.Absolute));
    }

    internal Uri BuildObjectUri(string objectKey)
    {
        var baseUrl = string.IsNullOrWhiteSpace(_storage.ServiceUrl)
            ? $"https://s3.{(_storage.Region is { Length: > 0 } r ? r : "us-east-1")}.amazonaws.com"
            : _storage.ServiceUrl.TrimEnd('/');

        var encodedKey = EncodePathSegments("/" + objectKey).TrimStart('/');
        if (_storage.ForcePathStyle)
        {
            return new Uri($"{baseUrl}/{Uri.EscapeDataString(_storage.Bucket)}/{encodedKey}", UriKind.Absolute);
        }

        var uri = new Uri(baseUrl, UriKind.Absolute);
        var portPart = uri.IsDefaultPort ? string.Empty : $":{uri.Port}";
        return new Uri($"{uri.Scheme}://{_storage.Bucket}.{uri.Host}{portPart}/{encodedKey}", UriKind.Absolute);
    }

    private string NormalizeObjectKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Object key cannot be empty.", nameof(key));
        }

        var cleaned = key.Trim().Replace('\\', '/');
        var s3Prefix = $"s3://{_storage.Bucket}/";
        if (cleaned.StartsWith(s3Prefix, StringComparison.OrdinalIgnoreCase))
        {
            cleaned = cleaned[s3Prefix.Length..];
        }
        else if (cleaned.StartsWith("s3://", StringComparison.OrdinalIgnoreCase))
        {
            var slashAfterBucket = cleaned.IndexOf('/', "s3://".Length);
            if (slashAfterBucket > 0)
            {
                cleaned = cleaned[(slashAfterBucket + 1)..];
            }
        }

        cleaned = cleaned.TrimStart('/');
        var segments = cleaned.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || segments.Any(s => s is "." or ".."))
        {
            throw new InvalidOperationException("Invalid S3 object key.");
        }

        return string.Join('/', segments);
    }

    private void SignRequest(HttpRequestMessage request, Uri endpoint, string payloadSha256Hex, DateTimeOffset now)
    {
        var utc = now.ToUniversalTime();
        var amzDate = utc.ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture);
        var dateStamp = utc.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var region = string.IsNullOrWhiteSpace(_storage.Region) ? "us-east-1" : _storage.Region;
        var hostHeader = endpoint.IsDefaultPort ? endpoint.Host : $"{endpoint.Host}:{endpoint.Port}";

        request.Headers.Host = hostHeader;
        request.Headers.Remove("x-amz-date");
        request.Headers.Remove("x-amz-content-sha256");
        request.Headers.TryAddWithoutValidation("x-amz-date", amzDate);
        request.Headers.TryAddWithoutValidation("x-amz-content-sha256", payloadSha256Hex);

        var headersToSign = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["host"] = hostHeader,
            ["x-amz-content-sha256"] = payloadSha256Hex,
            ["x-amz-date"] = amzDate
        };

        if (request.Headers.TryGetValues("x-amz-server-side-encryption", out var sseVals))
        {
            headersToSign["x-amz-server-side-encryption"] = string.Join(",", sseVals).Trim();
        }

        var canonicalHeaders = string.Concat(headersToSign.Select(kvp => $"{kvp.Key}:{kvp.Value}\n"));
        var signedHeaders = string.Join(";", headersToSign.Keys);
        var canonicalUri = EncodePathSegments(endpoint.AbsolutePath);

        var canonicalRequest = $"{request.Method.Method}\n{canonicalUri}\n\n{canonicalHeaders}\n{signedHeaders}\n{payloadSha256Hex}";
        var credentialScope = $"{dateStamp}/{region}/s3/aws4_request";
        var stringToSign = $"AWS4-HMAC-SHA256\n{amzDate}\n{credentialScope}\n{ToHexLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalRequest)))}";

        var signingKey = DeriveSigningKey(_secretKey, dateStamp, region, "s3");
        var signature = ToHexLower(HmacSha256(signingKey, stringToSign));

        request.Headers.TryAddWithoutValidation(
            "Authorization",
            $"AWS4-HMAC-SHA256 Credential={_accessKey}/{credentialScope}, SignedHeaders={signedHeaders}, Signature={signature}");
    }

    private static byte[] DeriveSigningKey(string secretKey, string dateStamp, string region, string service)
    {
        var kSecret = Encoding.UTF8.GetBytes("AWS4" + secretKey);
        var kDate = HmacSha256(kSecret, dateStamp);
        var kRegion = HmacSha256(kDate, region);
        var kService = HmacSha256(kRegion, service);
        return HmacSha256(kService, "aws4_request");
    }

    private static byte[] HmacSha256(byte[] key, string data) =>
        HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(data));

    private static string ToHexLower(byte[] bytes) =>
        Convert.ToHexString(bytes).ToLowerInvariant();

    private static string EncodePathSegments(string path)
    {
        var segments = path.Split('/');
        return string.Join('/', segments.Select(AwsUrlEncode));
    }

    private static string AwsUrlEncode(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var sb = new StringBuilder(value.Length);
        foreach (var b in Encoding.UTF8.GetBytes(value))
        {
            var c = (char)b;
            if ((c >= 'A' && c <= 'Z') ||
                (c >= 'a' && c <= 'z') ||
                (c >= '0' && c <= '9') ||
                c is '_' or '-' or '~' or '.')
            {
                sb.Append(c);
            }
            else
            {
                sb.Append('%').Append(b.ToString("X2", CultureInfo.InvariantCulture));
            }
        }

        return sb.ToString();
    }
}
