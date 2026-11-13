using LoanSystem.Common.Core.Ports.Inbound;
using LoanSystem.Modules.Security.Core.Contracts;

namespace LoanSystem.Modules.Security.Core.Ports.Inbound;

/// <summary>Confirms TOTP enrollment, generates recovery codes, and issues tokens (BE-SEC-03).</summary>
public interface IConfirmMfaEnrollmentUseCase : IUseCase<MfaEnrollConfirmRequest, MfaEnrollConfirmResponse>;
