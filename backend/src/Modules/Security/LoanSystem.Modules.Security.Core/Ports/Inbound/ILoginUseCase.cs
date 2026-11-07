using LoanSystem.Common.Core.Ports.Inbound;
using LoanSystem.Modules.Security.Core.Contracts;

namespace LoanSystem.Modules.Security.Core.Ports.Inbound;

/// <summary>Authenticates a user and issues a short-lived JWT plus a rotating refresh token (BE-SEC-01).</summary>
public interface ILoginUseCase : IUseCase<LoginRequest, AuthTokensResponse>;
