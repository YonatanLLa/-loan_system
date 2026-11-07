using System.Text.Json;
using StackExchange.Redis;

namespace LoanSystem.Common.Adapters.Http.Idempotency;

/// <summary>
/// Redis-backed store shared by every API instance. The in-progress marker is claimed with
/// SET NX, so only one concurrent request per key can run.
/// </summary>
public sealed class RedisIdempotencyStore : IIdempotencyStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly IConnectionMultiplexer _redis;

    public RedisIdempotencyStore(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task<IdempotencyBeginResult> TryBeginAsync(string key, string requestHash, TimeSpan inProgressTtl, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var marker = Serialize(new StoredEntry(requestHash, null, null, null));

        // Two attempts: the existing entry may expire or be released between SET NX and GET
        for (var attempt = 0; attempt < 2; attempt++)
        {
            if (await db.StringSetAsync(key, marker, inProgressTtl, When.NotExists))
                return IdempotencyBeginResult.Started;

            var existing = await db.StringGetAsync(key);
            if (!existing.HasValue)
                continue;

            var entry = JsonSerializer.Deserialize<StoredEntry>(existing.ToString(), SerializerOptions);
            if (entry is null)
                continue;

            if (!string.Equals(entry.RequestHash, requestHash, StringComparison.Ordinal))
                return IdempotencyBeginResult.HashMismatch;

            if (entry.StatusCode is null)
                return IdempotencyBeginResult.InProgress;

            return IdempotencyBeginResult.Completed(new CachedResponse(
                entry.StatusCode.Value,
                entry.ContentType,
                entry.Body is null ? [] : Convert.FromBase64String(entry.Body)));
        }

        return IdempotencyBeginResult.InProgress;
    }

    public async Task CompleteAsync(string key, string requestHash, CachedResponse response, TimeSpan ttl, CancellationToken ct = default)
    {
        var entry = new StoredEntry(requestHash, response.StatusCode, response.ContentType, Convert.ToBase64String(response.Body));
        await _redis.GetDatabase().StringSetAsync(key, Serialize(entry), ttl);
    }

    public async Task ReleaseAsync(string key, CancellationToken ct = default)
    {
        await _redis.GetDatabase().KeyDeleteAsync(key);
    }

    private static string Serialize(StoredEntry entry) => JsonSerializer.Serialize(entry, SerializerOptions);

    // StatusCode == null means "in progress"
    private sealed record StoredEntry(string RequestHash, int? StatusCode, string? ContentType, string? Body);
}
