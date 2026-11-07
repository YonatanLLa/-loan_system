namespace LoanSystem.Common.Adapters.Http.Idempotency;

/// <summary>
/// Single-process store for local development without Redis and for tests.
/// Not suitable for several API instances: each one would keep its own keys.
/// </summary>
public sealed class InMemoryIdempotencyStore : IIdempotencyStore
{
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly Lock _lock = new();
    private readonly TimeProvider _timeProvider;

    public InMemoryIdempotencyStore(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public Task<IdempotencyBeginResult> TryBeginAsync(string key, string requestHash, TimeSpan inProgressTtl, CancellationToken ct = default)
    {
        var now = _timeProvider.GetUtcNow();
        lock (_lock)
        {
            if (_entries.TryGetValue(key, out var entry) && entry.ExpiresAt > now)
            {
                if (!string.Equals(entry.RequestHash, requestHash, StringComparison.Ordinal))
                    return Task.FromResult(IdempotencyBeginResult.HashMismatch);

                return Task.FromResult(entry.Response is null
                    ? IdempotencyBeginResult.InProgress
                    : IdempotencyBeginResult.Completed(entry.Response));
            }

            _entries[key] = new Entry(requestHash, null, now + inProgressTtl);
            return Task.FromResult(IdempotencyBeginResult.Started);
        }
    }

    public Task CompleteAsync(string key, string requestHash, CachedResponse response, TimeSpan ttl, CancellationToken ct = default)
    {
        lock (_lock)
        {
            _entries[key] = new Entry(requestHash, response, _timeProvider.GetUtcNow() + ttl);
        }

        return Task.CompletedTask;
    }

    public Task ReleaseAsync(string key, CancellationToken ct = default)
    {
        lock (_lock)
        {
            _entries.Remove(key);
        }

        return Task.CompletedTask;
    }

    private sealed record Entry(string RequestHash, CachedResponse? Response, DateTimeOffset ExpiresAt);
}
