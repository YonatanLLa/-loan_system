using LoanSystem.Modules.Security.Core.Domain;

namespace LoanSystem.Modules.Security.Core.Ports.Outbound;

/// <summary>Issues a signed, short-lived access token (JWT) for a user. Implemented in the Adapters layer (BE-SEC-01).</summary>
internal interface IJwtTokenGenerator
{
    (string AccessToken, DateTimeOffset ExpiresAt) Generate(User user);
}
