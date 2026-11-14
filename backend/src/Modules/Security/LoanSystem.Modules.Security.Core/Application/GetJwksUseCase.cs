using LoanSystem.Common.Core.Application;
using LoanSystem.Modules.Security.Core.Contracts;
using LoanSystem.Modules.Security.Core.Ports.Inbound;
using LoanSystem.Modules.Security.Core.Ports.Outbound;

namespace LoanSystem.Modules.Security.Core.Application;

internal sealed class GetJwksUseCase : IGetJwksUseCase
{
    private readonly IPublicSigningKeysProvider _publicKeysProvider;

    public GetJwksUseCase(IPublicSigningKeysProvider publicKeysProvider)
    {
        _publicKeysProvider = publicKeysProvider;
    }

    public Task<Result<JwksResponse>> ExecuteAsync(CancellationToken ct = default)
    {
        var keys = _publicKeysProvider.GetValidationKeys();
        return Task.FromResult(Result.Success(new JwksResponse(keys)));
    }
}
