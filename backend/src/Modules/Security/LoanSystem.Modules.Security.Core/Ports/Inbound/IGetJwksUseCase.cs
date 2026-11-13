using LoanSystem.Common.Core.Ports.Inbound;
using LoanSystem.Modules.Security.Core.Contracts;

namespace LoanSystem.Modules.Security.Core.Ports.Inbound;

/// <summary>Returns the currently valid public signing keys as a JWKS document (BE-SEC-10).</summary>
public interface IGetJwksUseCase : IEmptyUseCase<JwksResponse>;
