using LoanSystem.Common.Core.Ports.Outbound;
using LoanSystem.Modules.Customers.Core.Domain;
using Shouldly;
using Xunit;

namespace LoanSystem.Domain.UnitTests.Customers;

public class CustomerTests
{
    private static readonly DateTimeOffset FixedUtc = new(2026, 11, 14, 15, 0, 0, TimeSpan.Zero);

    private static IdentityDocument CreateDni(string num = "12345678") =>
        IdentityDocument.Create(
            DocumentType.DNI,
            num,
            new DocumentTypeRule(DocumentType.DNI, "DNI", 8, @"^\d{8}$", true)).Value;

    private static IdentityDocument CreateRuc20() =>
        IdentityDocument.Create(
            DocumentType.RUC,
            "20123456786",
            new DocumentTypeRule(DocumentType.RUC, "RUC", 11, @"^(10|15|16|17|20)\d{9}$", true)).Value;

    private static Customer CreateValidNaturalCustomer(FakeClock? clock = null, decimal income = 3000m)
    {
        clock ??= new FakeClock(FixedUtc);
        var name = PersonName.Create("  María   Pérez  ").Value;
        var contact = ContactInfo.Create("Maria@Example.COM", "+51 987-654-321", null).Value;
        var profile = SocioeconomicProfile.Create(income, "Ingeniera", "Tech SAC", 24).Value;

        return Customer.RegisterNatural(
            CreateDni(),
            name,
            new DateOnly(1995, 5, 10),
            contact,
            "Av. Arequipa 123",
            "Lima",
            profile,
            Guid.NewGuid(),
            clock).Value;
    }

    [Fact]
    public void RegisterNatural_NormalizesFieldsAndStartsInPendingKyc()
    {
        var clock = new FakeClock(FixedUtc);
        var customer = CreateValidNaturalCustomer(clock);

        customer.PersonType.ShouldBe(PersonType.NATURAL);
        customer.FullName.ShouldBe("MARÍA PÉREZ");
        customer.Email.ShouldBe("maria@example.com");
        customer.PhonePrimary.ShouldBe("987654321");
        customer.KycStatus.ShouldBe(KycStatus.PENDING);
        customer.AmlRiskLevel.ShouldBe(AmlRiskLevel.LOW);
        customer.IsPep.ShouldBeFalse();
        customer.Version.ShouldBe(0);
    }

    [Fact]
    public void RegisterNatural_Exact18thBirthdayInLimaToday_Succeeds_WhileOneDayYounger_Fails()
    {
        var clock = new FakeClock(FixedUtc); // LimaToday = 2026-11-14
        var name = PersonName.Create("Ana Torres").Value;
        var contact = ContactInfo.Create("ana@test.pe", "911222333", null).Value;
        var profile = SocioeconomicProfile.Create(2000m, null, null, 0).Value;

        var exactly18 = Customer.RegisterNatural(
            CreateDni(), name, new DateOnly(2008, 11, 14), contact, "Jr. Sol 10", "Cusco", profile, Guid.NewGuid(), clock);
        exactly18.IsSuccess.ShouldBeTrue();

        var underage = Customer.RegisterNatural(
            CreateDni(), name, new DateOnly(2008, 11, 15), contact, "Jr. Sol 10", "Cusco", profile, Guid.NewGuid(), clock);
        underage.IsFailure.ShouldBeTrue();
        underage.Error.Code.ShouldBe("Customers.Underage");
    }

    [Fact]
    public void RegisterNatural_RejectsRucDocumentAndMissingBirthDate()
    {
        var clock = new FakeClock(FixedUtc);
        var name = PersonName.Create("Juan Gomez").Value;
        var contact = ContactInfo.Create("juan@test.pe", "911222333", null).Value;
        var profile = SocioeconomicProfile.Create(2000m, null, null, 0).Value;

        var rucOnNatural = Customer.RegisterNatural(
            CreateRuc20(), name, new DateOnly(1990, 1, 1), contact, "Av. Uno", "Lima", profile, Guid.NewGuid(), clock);
        rucOnNatural.IsFailure.ShouldBeTrue();
        rucOnNatural.Error.Code.ShouldBe("Customers.InvalidPersonDocumentCombination");

        var missingDob = Customer.RegisterNatural(
            CreateDni(), name, null, contact, "Av. Uno", "Lima", profile, Guid.NewGuid(), clock);
        missingDob.IsFailure.ShouldBeTrue();
        missingDob.Error.Code.ShouldBe("Customers.Validation");
    }

    [Fact]
    public void RegisterLegalEntity_RequiresRuc20AndNullBirthDate()
    {
        var clock = new FakeClock(FixedUtc);
        var name = PersonName.Create("Inversiones Andinas S.A.C.").Value;
        var contact = ContactInfo.Create("corp@andinas.pe", "999111222", null).Value;
        var profile = SocioeconomicProfile.Create(50000m, "Comercio", null, 36).Value;

        var ok = Customer.RegisterLegalEntity(
            CreateRuc20(), name, null, contact, "Av. Pardo 500", "Miraflores", profile, Guid.NewGuid(), clock);
        ok.IsSuccess.ShouldBeTrue();
        ok.Value.PersonType.ShouldBe(PersonType.JURIDICA);

        var withDob = Customer.RegisterLegalEntity(
            CreateRuc20(), name, new DateOnly(2000, 1, 1), contact, "Av. Pardo 500", "Miraflores", profile, Guid.NewGuid(), clock);
        withDob.IsFailure.ShouldBeTrue();
        withDob.Error.Code.ShouldBe("Customers.InvalidPersonDocumentCombination");
    }

    [Fact]
    public void UpdateContact_ResetsVerificationOnlyForChangedChannelAndIncrementsVersion()
    {
        var clock = new FakeClock(FixedUtc);
        var customer = CreateValidNaturalCustomer(clock);
        customer.MarkEmailVerified(clock);
        customer.MarkPhoneVerified(clock);
        var versionAfterVerify = customer.Version;

        clock.UtcNow = FixedUtc.AddMinutes(5);
        var sameContact = ContactInfo.Create("maria@example.com", "987654321", null).Value;
        customer.UpdateContact(sameContact, clock).ShouldBeEmpty();
        customer.Version.ShouldBe(versionAfterVerify);

        clock.UtcNow = FixedUtc.AddMinutes(10);
        var updatedEmail = ContactInfo.Create("new@example.com", "987654321", "955666777").Value;
        var changed = customer.UpdateContact(updatedEmail, clock);

        changed.ShouldBe(["email", "phone_secondary"]);
        customer.EmailVerifiedAt.ShouldBeNull();
        customer.PhoneVerifiedAt.ShouldNotBeNull();
        customer.Version.ShouldBe(versionAfterVerify + 1);
    }

    [Fact]
    public void GetRequiredDocuments_VariesByPersonTypeAndIncomeThreshold()
    {
        var clock = new FakeClock(FixedUtc);
        var belowThreshold = CreateValidNaturalCustomer(clock, income: 5000m);
        belowThreshold.GetRequiredDocuments(5000m).ShouldBe(
            [DocumentCategory.DNI_FRONTAL, DocumentCategory.DNI_REVERSO, DocumentCategory.SELFIE]);

        var aboveThreshold = CreateValidNaturalCustomer(clock, income: 5000.01m);
        aboveThreshold.GetRequiredDocuments(5000m).ShouldBe(
        [
            DocumentCategory.DNI_FRONTAL,
            DocumentCategory.DNI_REVERSO,
            DocumentCategory.SELFIE,
            DocumentCategory.BOLETA_PAGO
        ]);

        var legal = Customer.RegisterLegalEntity(
            CreateRuc20(),
            PersonName.Create("Empresa Test SAC").Value,
            null,
            ContactInfo.Create("c@e.pe", "999111222", null).Value,
            "Av 1",
            "Lima",
            SocioeconomicProfile.Create(20000m, null, null, 12).Value,
            Guid.NewGuid(),
            clock).Value;
        legal.GetRequiredDocuments(5000m).ShouldBe([DocumentCategory.FICHA_RUC]);
    }

    private sealed class FakeClock(DateTimeOffset? now = null) : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = now ?? new DateTimeOffset(2026, 11, 14, 15, 0, 0, TimeSpan.Zero);
        public DateTime LimaNow => UtcNow.UtcDateTime.AddHours(-5);
        public DateOnly LimaToday => DateOnly.FromDateTime(LimaNow);
    }
}
