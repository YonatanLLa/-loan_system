using LoanSystem.Common.Core.Ports.Inbound;
using LoanSystem.Modules.Security.Core.Contracts;

namespace LoanSystem.Modules.Security.Core.Ports.Inbound;

/// <summary>Resets a user's MFA configuration and revokes their active sessions (BE-SEC-03).</summary>
public interface IResetUserMfaUseCase : IUseCase<ResetUserMfaRequest>;
