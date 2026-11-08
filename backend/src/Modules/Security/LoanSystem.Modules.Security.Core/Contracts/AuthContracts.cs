namespace LoanSystem.Modules.Security.Core.Contracts;

/// <summary>Credentials submitted to <c>POST /api/auth/login</c>.</summary>
public sealed record LoginRequest(string Email, string Password);

/// <summary>Body of <c>POST /api/auth/refresh</c>: the raw refresh token to rotate.</summary>
public sealed record RefreshRequest(string RefreshToken);

/// <summary>Body of <c>POST /api/auth/logout</c>: the raw refresh token to revoke (BE-SEC-04).</summary>
public sealed record LogoutRequest(string? RefreshToken = null);

/// <summary>Tokens returned by a successful login or refresh (BE-SEC-01).</summary>
public sealed record AuthTokensResponse(string AccessToken, string RefreshToken, int ExpiresInSeconds, string Role);

/// <summary>Authenticated user profile and effective permissions returned by <c>GET /api/me</c> (BE-SEC-04).</summary>
public sealed record MeResponse(
    Guid Id,
    Guid UserId,
    string FullName,
    string Email,
    string Role,
    IReadOnlyCollection<string> Permissions);
