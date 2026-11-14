namespace LoanSystem.Modules.Security.Core.Contracts;

/// <summary>Credentials submitted to <c>POST /api/auth/login</c>.</summary>
public sealed record LoginRequest(string Email, string Password);

/// <summary>Body of <c>POST /api/auth/refresh</c>: the raw refresh token to rotate.</summary>
public sealed record RefreshRequest(string RefreshToken);

/// <summary>Body of <c>POST /api/auth/logout</c>: the raw refresh token to revoke (BE-SEC-04).</summary>
public sealed record LogoutRequest(string? RefreshToken = null);

/// <summary>Tokens returned by a successful login, MFA verification, or refresh (BE-SEC-01, BE-SEC-03).</summary>
public sealed record AuthTokensResponse(string AccessToken, string RefreshToken, int ExpiresInSeconds, string Role);

/// <summary>
/// Response from <c>POST /api/auth/login</c>: either full tokens (<c>AUTHENTICATED</c>)
/// or a short-lived MFA challenge (<c>MFA_REQUIRED</c> / <c>MFA_ENROLLMENT_REQUIRED</c>) (BE-SEC-03).
/// </summary>
public sealed record LoginResponse(
    string Status,
    string? AccessToken,
    string? RefreshToken,
    int? ExpiresInSeconds,
    string? Role,
    string? MfaToken,
    int? MfaExpiresInSeconds)
{
    public static LoginResponse Authenticated(AuthTokensResponse tokens) =>
        new("AUTHENTICATED", tokens.AccessToken, tokens.RefreshToken, tokens.ExpiresInSeconds, tokens.Role, null, null);

    public static LoginResponse Challenge(string status, string mfaToken, int mfaExpiresInSeconds) =>
        new(status, null, null, null, null, mfaToken, mfaExpiresInSeconds);
}

/// <summary>Request to complete the second factor using a TOTP code or a single-use recovery code (BE-SEC-03).</summary>
public sealed record MfaVerifyRequest(string MfaToken, string? Code = null, string? RecoveryCode = null);

/// <summary>Request to begin mandatory TOTP enrollment using an <c>ENROLL</c> challenge token (BE-SEC-03).</summary>
public sealed record MfaEnrollRequest(string MfaToken);

/// <summary>TOTP secret and <c>otpauth://</c> URI for QR rendering on the client (BE-SEC-03).</summary>
public sealed record MfaEnrollResponse(string Secret, string OtpauthUri);

/// <summary>Request to confirm TOTP enrollment with the first 6-digit code from the authenticator app (BE-SEC-03).</summary>
public sealed record MfaEnrollConfirmRequest(string MfaToken, string Code);

/// <summary>Tokens and one-time recovery codes returned upon completing MFA enrollment (BE-SEC-03).</summary>
public sealed record MfaEnrollConfirmResponse(AuthTokensResponse Tokens, IReadOnlyList<string> RecoveryCodes);

/// <summary>Request to reset a user's MFA configuration so they must re-enroll (BE-SEC-03).</summary>
public sealed record ResetUserMfaRequest(Guid UserId);

/// <summary>Authenticated user profile and effective permissions returned by <c>GET /api/me</c> (BE-SEC-04).</summary>
public sealed record MeResponse(
    Guid Id,
    Guid UserId,
    string FullName,
    string Email,
    string Role,
    IReadOnlyCollection<string> Permissions,
    bool MfaEnabled = false);

/// <summary>Request to suspend a user and invalidate all their sessions (BE-SEC-06).</summary>
public sealed record SuspendUserRequest(Guid UserId);

/// <summary>Request to reactivate a suspended user account (BE-SEC-06).</summary>
public sealed record ActivateUserRequest(Guid UserId);

/// <summary>HTTP body of <c>PUT /api/users/{id}/role</c> to request a four-eyes role change (BE-SEC-09).</summary>
public sealed record ChangeUserRoleRequest(string NewRole, string Reason);

/// <summary>Command to submit a user role-change approval request (BE-SEC-09).</summary>
public sealed record RequestUserRoleChangeCommand(Guid TargetUserId, string NewRole, string Reason);

/// <summary>Command to approve a pending four-eyes request (BE-SEC-09).</summary>
public sealed record ApproveApprovalCommand(Guid ApprovalRequestId, string? Comment = null, string? MfaCode = null);

/// <summary>Command to reject a pending four-eyes request (BE-SEC-09).</summary>
public sealed record RejectApprovalCommand(Guid ApprovalRequestId, string Comment);
