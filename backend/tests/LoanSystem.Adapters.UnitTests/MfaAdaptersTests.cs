using System.Security.Cryptography;
using LoanSystem.Modules.Security.Adapters.Outbound.Providers;
using Shouldly;
using Xunit;

namespace LoanSystem.Adapters.UnitTests;

public class MfaAdaptersTests
{
    // RFC 6238 Appendix B test secret: ASCII "12345678901234567890" -> Base32
    private const string Rfc6238SecretBase32 = "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ";

    [Theory]
    [InlineData(59L, "287082")]
    [InlineData(1111111109L, "081804")]
    [InlineData(1111111111L, "050471")]
    [InlineData(1234567890L, "005924")]
    public void OtpNetTotpService_MatchesRfc6238Sha1Vectors(long unixSeconds, string expectedCode)
    {
        var sut = new OtpNetTotpService("LoanSystem", allowedDriftSteps: 0);
        var now = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);

        var ok = sut.TryVerify(Rfc6238SecretBase32, expectedCode, now, out var step);

        ok.ShouldBeTrue();
        step.ShouldBe(unixSeconds / 30);
    }

    [Fact]
    public void OtpNetTotpService_BuildProvisioningUri_FormatsOtpAuthUri()
    {
        var sut = new OtpNetTotpService("LoanSystem", allowedDriftSteps: 1);

        var uri = sut.BuildProvisioningUri("JBSWY3DPEHPK3PXP", "admin@loan.test");

        uri.ShouldStartWith("otpauth://totp/LoanSystem:admin%40loan.test?");
        uri.ShouldContain("secret=JBSWY3DPEHPK3PXP");
        uri.ShouldContain("issuer=LoanSystem");
    }

    [Fact]
    public void AesGcmSecretProtector_RoundTripsAndRejectsTampering()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var sut = new AesGcmSecretProtector(key);
        var userId = Guid.NewGuid();

        var protectedValue = sut.Protect("JBSWY3DPEHPK3PXP", userId);
        protectedValue.ShouldStartWith("v2:v1:");
        sut.NeedsReprotect(protectedValue).ShouldBeFalse();
        sut.Unprotect(protectedValue, userId).ShouldBe("JBSWY3DPEHPK3PXP");

        var tampered = protectedValue[..^2] + (protectedValue[^1] == 'A' ? "BB" : "AA");
        Should.Throw<CryptographicException>(() => sut.Unprotect(tampered, userId));
    }

    [Fact]
    public void AesGcmSecretProtector_DifferentKey_ThrowsCryptographicException()
    {
        var userId = Guid.NewGuid();
        var sut1 = new AesGcmSecretProtector(RandomNumberGenerator.GetBytes(32));
        var sut2 = new AesGcmSecretProtector(RandomNumberGenerator.GetBytes(32));

        var protectedValue = sut1.Protect("JBSWY3DPEHPK3PXP", userId);

        Should.Throw<CryptographicException>(() => sut2.Unprotect(protectedValue, userId));
    }

    [Fact]
    public void AesGcmSecretProtector_CrossUserAadMismatch_ThrowsCryptographicException()
    {
        var sut = new AesGcmSecretProtector(RandomNumberGenerator.GetBytes(32));
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        var protectedForA = sut.Protect("JBSWY3DPEHPK3PXP", userA);

        Should.Throw<CryptographicException>(() => sut.Unprotect(protectedForA, userB));
    }

    [Fact]
    public void AesGcmSecretProtector_KeyRotation_DecryptsPreviousKeyAndFlagsReprotect()
    {
        var userId = Guid.NewGuid();
        var keyV1 = RandomNumberGenerator.GetBytes(32);
        var keyV2 = RandomNumberGenerator.GetBytes(32);

        var ringBeforeRotation = new AesGcmSecretProtector(
            "k1",
            new Dictionary<string, byte[]> { ["k1"] = keyV1 });

        var oldCiphertext = ringBeforeRotation.Protect("SECRET_TOTP_VALUE", userId);
        oldCiphertext.ShouldStartWith("v2:k1:");

        var ringAfterRotation = new AesGcmSecretProtector(
            "k2",
            new Dictionary<string, byte[]> { ["k1"] = keyV1, ["k2"] = keyV2 });

        ringAfterRotation.NeedsReprotect(oldCiphertext).ShouldBeTrue();
        ringAfterRotation.Unprotect(oldCiphertext, userId).ShouldBe("SECRET_TOTP_VALUE");

        var reprotected = ringAfterRotation.Protect("SECRET_TOTP_VALUE", userId);
        reprotected.ShouldStartWith("v2:k2:");
        ringAfterRotation.NeedsReprotect(reprotected).ShouldBeFalse();
        ringAfterRotation.Unprotect(reprotected, userId).ShouldBe("SECRET_TOTP_VALUE");

        var withoutOldKey = new AesGcmSecretProtector(
            "k2",
            new Dictionary<string, byte[]> { ["k2"] = keyV2 });
        Should.Throw<CryptographicException>(() => withoutOldKey.Unprotect(oldCiphertext, userId));
    }

    [Fact]
    public void AesGcmSecretProtector_LegacyV1Ciphertext_DecryptsWithoutAadAndFlagsReprotect()
    {
        var userId = Guid.NewGuid();
        var keyV1 = RandomNumberGenerator.GetBytes(32);
        var keyActive = RandomNumberGenerator.GetBytes(32);

        // Encrypt using legacy v1 format (no AAD)
        var plaintextBytes = System.Text.Encoding.UTF8.GetBytes("LEGACY_TOTP_SECRET");
        var payload = new byte[12 + plaintextBytes.Length + 16];
        RandomNumberGenerator.Fill(payload.AsSpan(0, 12));
        using (var aes = new AesGcm(keyV1, 16))
        {
            aes.Encrypt(
                payload.AsSpan(0, 12),
                plaintextBytes,
                payload.AsSpan(12, plaintextBytes.Length),
                payload.AsSpan(12 + plaintextBytes.Length, 16));
        }
        var legacyCiphertext = "v1:" + Convert.ToBase64String(payload);

        var ring = new AesGcmSecretProtector(
            "2026-11",
            new Dictionary<string, byte[]> { ["v1"] = keyV1, ["2026-11"] = keyActive });

        ring.NeedsReprotect(legacyCiphertext).ShouldBeTrue();
        ring.Unprotect(legacyCiphertext, userId).ShouldBe("LEGACY_TOTP_SECRET");
    }
}
