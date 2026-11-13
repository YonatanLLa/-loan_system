using LoanSystem.Common.Core.Ports.Inbound;
using LoanSystem.Modules.Security.Core.Contracts;

namespace LoanSystem.Modules.Security.Core.Ports.Inbound;

/// <summary>Generates a pending TOTP secret and provisioning URI for first-login MFA enrollment (BE-SEC-03).</summary>
public interface IBeginMfaEnrollmentUseCase : IUseCase<MfaEnrollRequest, MfaEnrollResponse>;
