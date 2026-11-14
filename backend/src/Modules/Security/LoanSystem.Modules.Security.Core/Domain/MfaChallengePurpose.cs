namespace LoanSystem.Modules.Security.Core.Domain;

/// <summary>Purpose of a short-lived MFA challenge issued during login (BE-SEC-03).</summary>
internal enum MfaChallengePurpose
{
    VERIFY,
    ENROLL
}
