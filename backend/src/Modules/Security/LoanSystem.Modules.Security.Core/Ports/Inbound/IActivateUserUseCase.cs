using LoanSystem.Common.Core.Ports.Inbound;
using LoanSystem.Modules.Security.Core.Contracts;

namespace LoanSystem.Modules.Security.Core.Ports.Inbound;

/// <summary>Reactivates a suspended user account and resets lockout counters (BE-SEC-06).</summary>
public interface IActivateUserUseCase : IUseCase<ActivateUserRequest>;
