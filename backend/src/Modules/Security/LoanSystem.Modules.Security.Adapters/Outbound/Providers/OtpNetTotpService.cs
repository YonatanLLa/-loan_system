using OtpNet;
using LoanSystem.Modules.Security.Core.Ports.Outbound;

namespace LoanSystem.Modules.Security.Adapters.Outbound.Providers;

/// <summary>RFC 6238 TOTP provider backed by Otp.NET (SHA-1, 6 digits, 30-second step) (BE-SEC-03).</summary>
internal sealed class OtpNetTotpService : ITotpService
{
    private const int SecretByteLength = 20; // 160 bits per RFC 4226/6238
    private const int StepSeconds = 30;
    private const int Digits = 6;

    private readonly string _issuer;
    private readonly VerificationWindow _window;

    public OtpNetTotpService(string issuer = "LoanSystem", int allowedDriftSteps = 1)
    {
        _issuer = string.IsNullOrWhiteSpace(issuer) ? "LoanSystem" : issuer;
        var drift = Math.Max(0, allowedDriftSteps);
        _window = new VerificationWindow(previous: drift, future: drift);
    }

    public string GenerateSecret()
    {
        var key = KeyGeneration.GenerateRandomKey(SecretByteLength);
        return Base32Encoding.ToString(key);
    }

    public string BuildProvisioningUri(string secret, string accountName)
    {
        var encodedIssuer = Uri.EscapeDataString(_issuer);
        var encodedAccount = Uri.EscapeDataString(accountName);
        return $"otpauth://totp/{encodedIssuer}:{encodedAccount}?secret={secret}&issuer={encodedIssuer}&algorithm=SHA1&digits={Digits}&period={StepSeconds}";
    }

    public bool TryVerify(string secret, string code, DateTimeOffset now, out long matchedStep)
    {
        matchedStep = 0;
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(code))
            return false;

        byte[] keyBytes;
        try
        {
            keyBytes = Base32Encoding.ToBytes(secret);
        }
        catch (ArgumentException)
        {
            return false;
        }

        var totp = new Totp(keyBytes, step: StepSeconds, mode: OtpHashMode.Sha1, totpSize: Digits);
        return totp.VerifyTotp(now.UtcDateTime, code.Trim(), out matchedStep, _window);
    }
}
