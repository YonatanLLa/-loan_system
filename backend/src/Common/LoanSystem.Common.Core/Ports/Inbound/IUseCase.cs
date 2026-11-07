using LoanSystem.Common.Core.Application;

namespace LoanSystem.Common.Core.Ports.Inbound;

public interface IUseCase<in TRequest, TResponse>
{
    Task<Result<TResponse>> ExecuteAsync(TRequest request, CancellationToken ct = default);
}

public interface IUseCase<in TRequest>
{
    Task<Result> ExecuteAsync(TRequest request, CancellationToken ct = default);
}

public interface IEmptyUseCase<TResponse>
{
    Task<Result<TResponse>> ExecuteAsync(CancellationToken ct = default);
}

public interface IEmptyUseCase
{
    Task<Result> ExecuteAsync(CancellationToken ct = default);
}
