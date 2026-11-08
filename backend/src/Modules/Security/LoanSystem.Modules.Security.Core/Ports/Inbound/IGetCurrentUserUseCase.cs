using LoanSystem.Common.Core.Ports.Inbound;
using LoanSystem.Modules.Security.Core.Contracts;

namespace LoanSystem.Modules.Security.Core.Ports.Inbound;

/// <summary>Returns the currently authenticated user's profile and effective permissions (BE-SEC-04).</summary>
public interface IGetCurrentUserUseCase : IEmptyUseCase<MeResponse>;
