using System.Security.Cryptography;
using LoanSystem.Common.Adapters.Crypto;
using LoanSystem.Common.Core.Ports.Outbound;
using LoanSystem.Modules.Customers.Adapters.Outbound.Providers;
using LoanSystem.Modules.Customers.Core.Domain;
using LoanSystem.Modules.Customers.Core.Domain.Validators;
using LoanSystem.Modules.Customers.Core.Ports.Outbound;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace LoanSystem.Adapters.UnitTests.Customers;

public class FileSignatureSnifferAndAdaptersTests
{
    [Fact]
    public void FileSignatureSniffer_DetectsMagicBytesAndRejectsSpoofedOrUnsupported()
    {
        var allowed = new[] { "image/jpeg", "image/png", "image/webp", "application/pdf" };

        FileSignatureSniffer.DetectAllowedMimeType([0xFF, 0xD8, 0xFF, 0xE0], allowed).ShouldBe("image/jpeg");
        FileSignatureSniffer.DetectAllowedMimeType([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00], allowed).ShouldBe("image/png");
        FileSignatureSniffer.DetectAllowedMimeType("RIFF\0\0\0\0WEBP"u8, allowed).ShouldBe("image/webp");
        FileSignatureSniffer.DetectAllowedMimeType("%PDF-1.7\n"u8, allowed).ShouldBe("application/pdf");

        // Spoofed DOS MZ / plain text / too short / disallowed MIME
        FileSignatureSniffer.DetectAllowedMimeType([0x4D, 0x5A, 0x90, 0x00], allowed).ShouldBeNull();
        FileSignatureSniffer.DetectAllowedMimeType("<html>bad</html>"u8, allowed).ShouldBeNull();
        FileSignatureSniffer.DetectAllowedMimeType([0xFF, 0xD8], allowed).ShouldBeNull();
        FileSignatureSniffer.DetectAllowedMimeType([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], ["application/pdf"]).ShouldBeNull();
    }

    [Fact]
    public void AesGcmFieldProtector_EncryptsWithRandomNonce_RotatesKeys_AndComputesDeterministicHmac()
    {
        var key1 = RandomNumberGenerator.GetBytes(32);
        var key2 = RandomNumberGenerator.GetBytes(32);
        var hmac = RandomNumberGenerator.GetBytes(32);

        using var v1Protector = new AesGcmFieldProtector("k1", new Dictionary<string, byte[]> { ["k1"] = key1 }, hmac);
        var c1 = v1Protector.Protect("00219100000000001234", context: "cci");
        var c2 = v1Protector.Protect("00219100000000001234", context: "cci");

        c1.ShouldStartWith("v1:k1:");
        c1.ShouldNotBe(c2); // random 96-bit nonce
        c1.ShouldNotContain("00219100000000001234");
        v1Protector.Unprotect(c1, context: "cci").ShouldBe("00219100000000001234");

        // Wrong AAD fails GCM authentication tag verification
        Should.Throw<CryptographicException>(() => v1Protector.Unprotect(c1, context: "other"));

        // Deterministic 64-hex HMAC-SHA256
        var h1 = v1Protector.Hash("00219100000000001234");
        h1.Length.ShouldBe(64);
        v1Protector.Hash("00219100000000001234").ShouldBe(h1);

        // Key rotation: k2 active, k1 kept for decrypt
        using var rotated = new AesGcmFieldProtector(
            "k2",
            new Dictionary<string, byte[]> { ["k1"] = key1, ["k2"] = key2 },
            hmac);
        rotated.Unprotect(c1, context: "cci").ShouldBe("00219100000000001234");
        var underK2 = rotated.Protect("00219100000000001234", context: "cci");
        underK2.ShouldStartWith("v1:k2:");
    }

    [Fact]
    public void HmacOtpHasher_HashesAndVerifiesConstTime()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Customers:Otp:Pepper"] = "super-secret-pepper-value-123456" })
            .Build();
        var env = new TestEnv { EnvironmentName = Environments.Production };

        var hasher = new HmacOtpHasher(config, env);
        var digest = hasher.Hash("123456");

        digest.Length.ShouldBe(64);
        hasher.Verify("123456", digest).ShouldBeTrue();
        hasher.Verify("654321", digest).ShouldBeFalse();
        hasher.Verify("", digest).ShouldBeFalse();
    }

    [Fact]
    public async Task FakeIdentityVerifier_FollowsDeterministicDocumentSuffixRules()
    {
        var verifier = new FakeIdentityVerifier();
        verifier.ProviderName.ShouldBe("FAKE");

        var failReg = await verifier.VerifyRegistryAsync(
            new IdentityRegistryQuery(Guid.NewGuid(), PersonType.NATURAL, DocumentType.DNI, "12345670", "Test", new DateOnly(1990, 1, 1)));
        failReg.Result.ShouldBe(KycCheckResult.FAIL);

        var pepHit = await verifier.ScreenWatchlistsAsync(
            new WatchlistQuery(Guid.NewGuid(), PersonType.NATURAL, DocumentType.DNI, "12345679", "Test"));
        pepHit.Result.ShouldBe(KycCheckResult.REVIEW);
        pepHit.IsPepFlag.ShouldBeTrue();

        var passReg = await verifier.VerifyRegistryAsync(
            new IdentityRegistryQuery(Guid.NewGuid(), PersonType.NATURAL, DocumentType.DNI, "12345678", "Test", new DateOnly(1990, 1, 1)));
        passReg.Result.ShouldBe(KycCheckResult.PASS);
    }

    private sealed class TestEnv : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "LoanSystem.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
