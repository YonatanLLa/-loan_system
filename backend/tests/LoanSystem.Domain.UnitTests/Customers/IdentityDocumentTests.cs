using LoanSystem.Common.Core.Ports.Outbound;
using LoanSystem.Modules.Customers.Core.Domain;
using LoanSystem.Modules.Customers.Core.Domain.Validators;
using Shouldly;
using Xunit;

namespace LoanSystem.Domain.UnitTests.Customers;

public class IdentityDocumentTests
{
    private static DocumentTypeRule ActiveRule(DocumentType type, int? len = null, string? regex = null) =>
        new(type, type.ToString(), len, regex, IsActive: true);

    [Fact]
    public void Normalize_StripsWhitespaceHyphensAndUppercases()
    {
        IdentityDocument.Normalize("  ab-12 34-c  ").ShouldBe("AB1234C");
    }

    [Fact]
    public void Create_Dni_ValidatesExact8Digits()
    {
        var rule = ActiveRule(DocumentType.DNI, 8, @"^\d{8}$");

        var valid = IdentityDocument.Create(DocumentType.DNI, " 1234-5678 ", rule);
        valid.IsSuccess.ShouldBeTrue();
        valid.Value.Type.ShouldBe(DocumentType.DNI);
        valid.Value.Number.ShouldBe("12345678");

        var shortDni = IdentityDocument.Create(DocumentType.DNI, "1234567", rule);
        shortDni.IsFailure.ShouldBeTrue();
        shortDni.Error.Code.ShouldBe("Customers.InvalidDocumentNumber");
    }

    [Fact]
    public void Create_CeAndPassport_ValidateFormats()
    {
        var ce = IdentityDocument.Create(DocumentType.CE, "001234567", ActiveRule(DocumentType.CE, 9, @"^\d{9}$"));
        ce.IsSuccess.ShouldBeTrue();
        ce.Value.Number.ShouldBe("001234567");

        var ceInvalid = IdentityDocument.Create(DocumentType.CE, "12345678", ActiveRule(DocumentType.CE, 9, @"^\d{9}$"));
        ceInvalid.IsFailure.ShouldBeTrue();

        var passport = IdentityDocument.Create(
            DocumentType.PASAPORTE,
            "pe-123456",
            ActiveRule(DocumentType.PASAPORTE, null, @"^[A-Z0-9]{6,12}$"));
        passport.IsSuccess.ShouldBeTrue();
        passport.Value.Number.ShouldBe("PE123456");

        var shortPassport = IdentityDocument.Create(
            DocumentType.PASAPORTE,
            "AB12",
            ActiveRule(DocumentType.PASAPORTE, null, @"^[A-Z0-9]{6,12}$"));
        shortPassport.IsFailure.ShouldBeTrue();
    }

    [Theory]
    [InlineData("20123456786")]
    [InlineData("10123456781")] // remainder == 11 -> 1
    [InlineData("10444444440")] // remainder == 10 -> 0
    public void Create_Ruc_ValidatesCheckDigitModulo11(string validRuc)
    {
        var rule = ActiveRule(DocumentType.RUC, 11, @"^(10|15|16|17|20)\d{9}$");

        var result = IdentityDocument.Create(DocumentType.RUC, validRuc, rule);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Number.ShouldBe(validRuc);
    }

    [Theory]
    [InlineData("20123456785")] // wrong check digit
    [InlineData("30123456786")] // unsupported prefix
    [InlineData("2012345678")]  // too short
    public void Create_Ruc_InvalidCheckDigitOrPrefix_Fails(string invalidRuc)
    {
        var rule = ActiveRule(DocumentType.RUC, 11, @"^(10|15|16|17|20)\d{9}$");

        var result = IdentityDocument.Create(DocumentType.RUC, invalidRuc, rule);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Customers.InvalidDocumentNumber");
    }

    [Fact]
    public void Create_InactiveRule_FailsWithDocumentTypeInactive()
    {
        var rule = new DocumentTypeRule(DocumentType.PASAPORTE, "Pasaporte", null, null, IsActive: false);

        var result = IdentityDocument.Create(DocumentType.PASAPORTE, "AB123456", rule);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Customers.DocumentTypeInactive");
    }

    [Fact]
    public void PiiMasker_MasksDocumentPhoneEmailAndOtpTargets()
    {
        PiiMasker.MaskDocNumber("12345678").ShouldBe("****5678");
        PiiMasker.MaskPhone("987654321").ShouldBe("*****4321");
        PiiMasker.MaskPhone(null).ShouldBeNull();
        PiiMasker.MaskEmail("maria.perez@example.com").ShouldBe("m***@example.com");
        PiiMasker.MaskTarget(OtpChannel.EMAIL, "ana@test.pe").ShouldBe("a***@test.pe");
        PiiMasker.MaskTarget(OtpChannel.SMS, "999888777").ShouldBe("*****8777");
    }

    private sealed class FakeClock(DateTimeOffset? now = null) : IClock
    {
        public DateTimeOffset UtcNow { get; } = now ?? new DateTimeOffset(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);
        public DateTime LimaNow => UtcNow.UtcDateTime.AddHours(-5);
        public DateOnly LimaToday => DateOnly.FromDateTime(LimaNow);
    }
}
