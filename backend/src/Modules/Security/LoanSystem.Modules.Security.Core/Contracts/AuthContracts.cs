namespace LoanSystem.Modules.Security.Core.Contracts;

/// <summary>Credentials submitted to <c>POST /api/auth/login</c>.</summary>
public sealed record LoginRequest(string Email, string Password);

/// <summary>Body of <c>POST /api/auth/refresh</c>: the raw refresh token to rotate.</summary>
public sealed record RefreshRequest(string RefreshToken);

/// <summary>Tokens returned by a successful login or refresh (BE-SEC-01).</summary>
public sealed record AuthTokensResponse(string AccessToken, string RefreshToken, int ExpiresInSeconds, string Role);
