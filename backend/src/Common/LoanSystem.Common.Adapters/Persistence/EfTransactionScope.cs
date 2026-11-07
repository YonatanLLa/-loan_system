using LoanSystem.Common.Core.Ports.Outbound;
using Microsoft.EntityFrameworkCore.Storage;

namespace LoanSystem.Common.Adapters.Persistence;

public sealed class EfTransactionScope : ITransactionScope
{
    private readonly IDbContextTransaction _transaction;

    public EfTransactionScope(IDbContextTransaction transaction)
    {
        _transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
    }

    public Task CommitAsync(CancellationToken ct = default) => _transaction.CommitAsync(ct);

    public Task RollbackAsync(CancellationToken ct = default) => _transaction.RollbackAsync(ct);

    public ValueTask DisposeAsync() => _transaction.DisposeAsync();
}
