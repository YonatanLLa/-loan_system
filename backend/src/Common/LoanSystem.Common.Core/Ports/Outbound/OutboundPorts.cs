namespace LoanSystem.Common.Core.Ports.Outbound;

public interface IUnitOfWork
{
    /// <summary>Persists all pending changes atomically.</summary>
    /// <exception cref="ConcurrencyConflictException">An optimistic concurrency check failed; nothing was saved.</exception>
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task<ITransactionScope> BeginTransactionAsync(CancellationToken ct = default);
}

public interface ITransactionScope : IAsyncDisposable
{
    Task CommitAsync(CancellationToken ct = default);
    Task RollbackAsync(CancellationToken ct = default);
}

public interface IClock
{
    DateTimeOffset UtcNow { get; }
    DateTime LimaNow { get; }
    DateOnly LimaToday { get; }
}

public interface ICurrentUser
{
    Guid? UserId { get; }
    string? Email { get; }
    IReadOnlyCollection<string> Roles { get; }
    Guid? CustomerId { get; }
    bool IsAuthenticated { get; }
    bool IsInRole(string role);
}

public interface IOutbox
{
    void Enqueue<TEvent>(TEvent @event) where TEvent : class;
}
