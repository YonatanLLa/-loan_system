using LoanSystem.Common.Core.Ports.Inbound;
using LoanSystem.Modules.Security.Core.Contracts;

namespace LoanSystem.Modules.Security.Core.Ports.Inbound;

/// <summary>Suspends a user account and immediately invalidates all their active sessions (BE-SEC-06).</summary>
public interface ISuspendUserUseCase : IUseCase<SuspendUserRequest>;
