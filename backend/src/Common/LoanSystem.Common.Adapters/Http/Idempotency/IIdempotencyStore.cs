namespace LoanSystem.Common.Adapters.Http.Idempotency;

public enum IdempotencyBeginStatus
{
    /// <summary>The key was free: the caller owns it and must Complete or Release it.</summary>
    Started,
    /// <summary>Another request with the same key is still running.</summary>
    InProgress,
    /// <summary>A response is already stored for this key and request.</summary>
    Completed,
    /// <summary>The key was used with a different request body.</summary>
    HashMismatch
}

public sealed record CachedResponse(int StatusCode, string? ContentType, byte[] Body);

public sealed record IdempotencyBeginResult(IdempotencyBeginStatus Status, CachedResponse? Response = null)
{
    public static IdempotencyBeginResult Started { get; } = new(IdempotencyBeginStatus.Started);
    public static IdempotencyBeginResult InProgress { get; } = new(IdempotencyBeginStatus.InProgress);
    public static IdempotencyBeginResult HashMismatch { get; } = new(IdempotencyBeginStatus.HashMismatch);
    public static IdempotencyBeginResult Completed(CachedResponse response) => new(IdempotencyBeginStatus.Completed, response);
}

public interface IIdempotencyStore
{
    /// <summary>Atomically claims the key with an in-progress marker that expires after <paramref name="inProgressTtl"/>.</summary>
    Task<IdempotencyBeginResult> TryBeginAsync(string key, string requestHash, TimeSpan inProgressTtl, CancellationToken ct = default);

    /// <summary>Stores the final response for the key (replaces the in-progress marker).</summary>
    Task CompleteAsync(string key, string requestHash, CachedResponse response, TimeSpan ttl, CancellationToken ct = default);

    /// <summary>Frees the key so the request can be retried (used on 5xx or exceptions).</summary>
    Task ReleaseAsync(string key, CancellationToken ct = default);
}

public sealed class IdempotencyOptions
{
    public const string SectionName = "Idempotency";

    public string HeaderName { get; set; } = "Idempotency-Key";
    public int MaxKeyLength { get; set; } = 255;

    /// <summary>How long a completed response is replayed.</summary>
    public TimeSpan ResponseTtl { get; set; } = TimeSpan.FromHours(24);

    /// <summary>How long a key stays locked while its first request runs (protects against crashed requests).</summary>
    public TimeSpan InProgressTtl { get; set; } = TimeSpan.FromMinutes(2);
}
