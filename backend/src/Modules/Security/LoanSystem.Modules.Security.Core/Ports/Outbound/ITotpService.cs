namespace LoanSystem.Modules.Security.Core.Ports.Outbound;

/// <summary>Generates TOTP secrets, provisioning URIs, and verifies 6-digit TOTP codes (BE-SEC-03).</summary>
internal interface ITotpService
{
    string GenerateSecret();
    string BuildProvisioningUri(string secret, string accountName);
    bool TryVerify(string secret, string code, DateTimeOffset now, out long matchedStep);
}
