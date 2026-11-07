using LoanSystem.Common.Core.Ports.Inbound;
using LoanSystem.Modules.Security.Core.Contracts;

namespace LoanSystem.Modules.Security.Core.Ports.Inbound;

/// <summary>Rotates an active refresh token: revokes the one presented and issues a new token pair (BE-SEC-01).</summary>
public interface IRefreshTokenUseCase : IUseCase<RefreshRequest, AuthTokensResponse>;
