using LoanSystem.Common.Core.Ports.Inbound;
using LoanSystem.Modules.Security.Core.Contracts;

namespace LoanSystem.Modules.Security.Core.Ports.Inbound;

/// <summary>Verifies a TOTP or recovery code against an active MFA challenge and issues tokens (BE-SEC-03).</summary>
public interface IVerifyMfaUseCase : IUseCase<MfaVerifyRequest, AuthTokensResponse>;
