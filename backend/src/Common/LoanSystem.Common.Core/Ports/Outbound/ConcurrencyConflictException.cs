namespace LoanSystem.Common.Core.Ports.Outbound;

/// <summary>
/// Thrown by <see cref="IUnitOfWork.SaveChangesAsync"/> when an optimistic concurrency check fails:
/// a row the use case loaded was changed (or removed) by someone else before the save. Nothing from
/// that save is persisted. Lets use cases react to lost races without depending on EF Core.
/// </summary>
public sealed class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException()
        : base("The data was modified by another operation before the changes could be saved.") { }

    public ConcurrencyConflictException(string message) : base(message) { }

    public ConcurrencyConflictException(string message, Exception innerException) : base(message, innerException) { }
}
