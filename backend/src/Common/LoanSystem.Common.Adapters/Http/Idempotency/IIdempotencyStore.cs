namespace LoanSystem.Common.Adapters.Http.Idempotency;

public enum IdempotencyBeginStatus
{
    /// <summary>La clave estaba libre: quien llama pasa a ser su dueño y debe completarla (Complete) o liberarla (Release).</summary>
    Started,
    /// <summary>Otra petición con la misma clave todavía se está ejecutando.</summary>
    InProgress,
    /// <summary>Ya hay una respuesta guardada para esta clave y esta petición.</summary>
    Completed,
    /// <summary>La clave ya se usó con un cuerpo de petición distinto.</summary>
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
    /// <summary>Reserva la clave de forma atómica con una marca de "en curso" que expira tras <paramref name="inProgressTtl"/>.</summary>
    Task<IdempotencyBeginResult> TryBeginAsync(string key, string requestHash, TimeSpan inProgressTtl, CancellationToken ct = default);

    /// <summary>Guarda la respuesta final de la clave (reemplaza la marca de "en curso").</summary>
    Task CompleteAsync(string key, string requestHash, CachedResponse response, TimeSpan ttl, CancellationToken ct = default);

    /// <summary>Libera la clave para que la petición se pueda reintentar (se usa ante errores 5xx o excepciones).</summary>
    Task ReleaseAsync(string key, CancellationToken ct = default);
}

public sealed class IdempotencyOptions
{
    public const string SectionName = "Idempotency";

    public string HeaderName { get; set; } = "Idempotency-Key";
    public int MaxKeyLength { get; set; } = 255;

    /// <summary>Tiempo durante el cual se reenvía una respuesta ya completada.</summary>
    public TimeSpan ResponseTtl { get; set; } = TimeSpan.FromHours(24);

    /// <summary>Tiempo que la clave queda bloqueada mientras se ejecuta su primera petición (protege ante peticiones que se cayeron).</summary>
    public TimeSpan InProgressTtl { get; set; } = TimeSpan.FromMinutes(2);
}
