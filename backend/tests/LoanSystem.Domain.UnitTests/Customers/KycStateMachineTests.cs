using LoanSystem.Common.Core.Ports.Outbound;
using LoanSystem.Modules.Customers.Core.Domain;
using Shouldly;
using Xunit;

namespace LoanSystem.Domain.UnitTests.Customers;

public class KycStateMachineTests
{
    private static readonly DateTimeOffset BaseUtc = new(2026, 11, 14, 15, 0, 0, TimeSpan.Zero);

    private static Customer CreateNaturalCustomer(FakeClock clock, decimal income = 3000m)
    {
        var doc = IdentityDocument.Create(
            DocumentType.DNI,
            "44556677",
            new DocumentTypeRule(DocumentType.DNI, "DNI", 8, @"^\d{8}$", true)).Value;

        return Customer.RegisterNatural(
            doc,
            PersonName.Create("Carlos Mendoza").Value,
            new DateOnly(1992, 3, 20),
            ContactInfo.Create("carlos@test.pe", "987654321", null).Value,
            "Jr. Junin 456",
            "Lima",
            SocioeconomicProfile.Create(income, "Analista", "Banco", 30).Value,
            Guid.NewGuid(),
            clock).Value;
    }

    private static HashSet<DocumentCategory> BasicDocs() =>
    [
        DocumentCategory.DNI_FRONTAL,
        DocumentCategory.DNI_REVERSO,
        DocumentCategory.SELFIE
    ];

    private static KycVerification Check(Guid customerId, KycCheckType type, KycCheckResult result, FakeClock clock)
    {
        var check = KycVerification.CreatePending(customerId, type, "Fake", null, Guid.NewGuid(), clock);
        check.RecordOutcome(result, 0.99m, "ext-1", "{}", result == KycCheckResult.ERROR ? "timeout" : null, clock);
        return check;
    }

    [Fact]
    public void SubmitKyc_MissingRequiredDocument_ReturnsMissingExtension()
    {
        var clock = new FakeClock(BaseUtc);
        var customer = CreateNaturalCustomer(clock, income: 8000m);

        var res = customer.SubmitKyc(BasicDocs(), incomeProofThreshold: 5000m, clock);

        res.IsFailure.ShouldBeTrue();
        res.Error.Code.ShouldBe("Customers.MissingRequiredDocuments");
        var missing = res.Error.Extensions!["missing"].ShouldBeAssignableTo<IReadOnlyList<string>>()!;
        missing.ShouldContain(nameof(DocumentCategory.BOLETA_PAGO));
    }

    [Fact]
    public void SubmitAndApproveKyc_StandardNotes_SetsVerifiedAndExpiry()
    {
        var clock = new FakeClock(BaseUtc);
        var customer = CreateNaturalCustomer(clock);
        customer.SubmitKyc(BasicDocs(), 5000m, clock).IsSuccess.ShouldBeTrue();

        var checks = new[] { Check(customer.Id, KycCheckType.IDENTITY_REGISTRY, KycCheckResult.PASS, clock) };

        var shortNotes = customer.ApproveKyc(Guid.NewGuid(), "short", isAdmin: false, checks, clock, validityMonths: 12);
        shortNotes.IsFailure.ShouldBeTrue();
        shortNotes.Error.Code.ShouldBe("Customers.KycNotesRequired");

        clock.UtcNow = BaseUtc.AddHours(1);
        var approved = customer.ApproveKyc(Guid.NewGuid(), "Dossier verified cleanly.", isAdmin: false, checks, clock, validityMonths: 12);
        approved.IsSuccess.ShouldBeTrue();
        customer.KycStatus.ShouldBe(KycStatus.VERIFIED);
        customer.KycExpiresAt.ShouldBe(clock.UtcNow.AddMonths(12));
    }

    [Fact]
    public void ApproveKyc_BlocksFailCheck_RequiresAdminForPepOrHighAml_AndRequiresLongerNotesOnReview()
    {
        var clock = new FakeClock(BaseUtc);
        var customer = CreateNaturalCustomer(clock);
        customer.SubmitKyc(BasicDocs(), 5000m, clock).IsSuccess.ShouldBeTrue();

        var failChecks = new[] { Check(customer.Id, KycCheckType.IDENTITY_REGISTRY, KycCheckResult.FAIL, clock) };
        var failRes = customer.ApproveKyc(Guid.NewGuid(), new string('A', 40), isAdmin: true, failChecks, clock, 12);
        failRes.IsFailure.ShouldBeTrue();
        failRes.Error.Code.ShouldBe("Customers.KycCheckFailed");

        customer.MarkAsPep(clock);
        var reviewChecks = new[] { Check(customer.Id, KycCheckType.PEP_SCREENING, KycCheckResult.REVIEW, clock) };

        var nonAdmin = customer.ApproveKyc(Guid.NewGuid(), new string('A', 35), isAdmin: false, reviewChecks, clock, 12);
        nonAdmin.IsFailure.ShouldBeTrue();
        nonAdmin.Error.Code.ShouldBe("Customers.KycRequiresAdmin");

        var shortAdminNotes = customer.ApproveKyc(Guid.NewGuid(), "Only twenty chars!!!", isAdmin: true, reviewChecks, clock, 12);
        shortAdminNotes.IsFailure.ShouldBeTrue();
        shortAdminNotes.Error.Code.ShouldBe("Customers.KycNotesRequired");

        var okAdmin = customer.ApproveKyc(Guid.NewGuid(), "PEP match reviewed against official registry and cleared.", isAdmin: true, reviewChecks, clock, 12);
        okAdmin.IsSuccess.ShouldBeTrue();
        customer.KycStatus.ShouldBe(KycStatus.VERIFIED);
    }

    [Fact]
    public void Reject_Resubmit_And_ExpireLifecycle_TransitionsCorrectly()
    {
        var clock = new FakeClock(BaseUtc);
        var customer = CreateNaturalCustomer(clock);
        customer.SubmitKyc(BasicDocs(), 5000m, clock).IsSuccess.ShouldBeTrue();
        customer.ApproveKyc(Guid.NewGuid(), "Approved initial verification.", isAdmin: false, [], clock, 12).IsSuccess.ShouldBeTrue();

        // Rejecting an already VERIFIED dossier requires Admin
        var analystReject = customer.RejectKyc(Guid.NewGuid(), "Post-approval discrepancy detected", isAdmin: false, clock);
        analystReject.IsFailure.ShouldBeTrue();
        analystReject.Error.Code.ShouldBe("Customers.KycRequiresAdmin");

        // Too early to expire
        customer.ExpireKyc(clock).IsFailure.ShouldBeTrue();
        customer.IsKycEffectivelyExpired(clock.UtcNow).ShouldBeFalse();

        // Past expiry
        clock.UtcNow = BaseUtc.AddMonths(12).AddSeconds(1);
        customer.IsKycEffectivelyExpired(clock.UtcNow).ShouldBeTrue();
        customer.ExpireKyc(clock).IsSuccess.ShouldBeTrue();
        customer.KycStatus.ShouldBe(KycStatus.EXPIRED);

        // Resubmit resets to PENDING
        customer.ResubmitKyc(clock).IsSuccess.ShouldBeTrue();
        customer.KycStatus.ShouldBe(KycStatus.PENDING);
        customer.KycSubmittedAt.ShouldBeNull();
        customer.KycExpiresAt.ShouldBeNull();
    }

    private sealed class FakeClock(DateTimeOffset? now = null) : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = now ?? BaseUtc;
        public DateTime LimaNow => UtcNow.UtcDateTime.AddHours(-5);
        public DateOnly LimaToday => DateOnly.FromDateTime(LimaNow);
    }
}
