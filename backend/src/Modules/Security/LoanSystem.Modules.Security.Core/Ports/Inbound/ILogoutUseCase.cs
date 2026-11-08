using LoanSystem.Common.Core.Ports.Inbound;
using LoanSystem.Modules.Security.Core.Contracts;

namespace LoanSystem.Modules.Security.Core.Ports.Inbound;

/// <summary>Revokes an active refresh token or the authenticated user's active sessions (BE-SEC-04).</summary>
public interface ILogoutUseCase : IUseCase<LogoutRequest>;
