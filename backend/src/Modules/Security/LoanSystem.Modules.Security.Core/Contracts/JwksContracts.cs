namespace LoanSystem.Modules.Security.Core.Contracts;

/// <summary>Public RSA JSON Web Key (RFC 7517 / RFC 7518) exposed by the JWKS endpoint (BE-SEC-10).</summary>
public sealed record JwkDto(
    string Kty,
    string Use,
    string Alg,
    string Kid,
    string N,
    string E);

/// <summary>Response envelope for <c>GET /.well-known/jwks.json</c> (BE-SEC-10).</summary>
public sealed record JwksResponse(IReadOnlyList<JwkDto> Keys);
