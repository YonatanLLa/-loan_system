using LoanSystem.Common.Core.Ports.Inbound;
using LoanSystem.Modules.Security.Core.Contracts;

namespace LoanSystem.Modules.Security.Core.Ports.Inbound;

/// <summary>Authenticates a user and issues either tokens or an MFA challenge (BE-SEC-01, BE-SEC-03).</summary>
public interface ILoginUseCase : IUseCase<LoginRequest, LoginResponse>;
