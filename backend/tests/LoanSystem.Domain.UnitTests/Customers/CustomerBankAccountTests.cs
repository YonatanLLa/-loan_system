using LoanSystem.Common.Core.Ports.Outbound;
using LoanSystem.Modules.Customers.Core.Domain;
using LoanSystem.Modules.Customers.Core.Domain.Validators;
using LoanSystem.Modules.Customers.Core.Ports.Outbound;
using Shouldly;
using Xunit;

namespace LoanSystem.Domain.UnitTests.Customers;

public class CustomerBankAccountTests
{
    private static readonly DateTimeOffset BaseUtc = new(2026, 11, 14, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CciValidator_Normalizes20DigitsAndResolvesBankName()
    {
        var validator = new CciValidator(new StubBanksCatalog());

        var valid = validator.Validate("002-1910000000000-1234");
        valid.IsSuccess.ShouldBeTrue();
        valid.Value.NormalizedCci.ShouldBe("00219100000000001234");
        valid.Value.BankCode.ShouldBe("002");
        valid.Value.BankName.ShouldBe("Banco de Crédito del Perú");
        valid.Value.Last4.ShouldBe("1234");

        var unknownBank = validator.Validate("99919100000000001234");
        unknownBank.IsFailure.ShouldBeTrue();
        unknownBank.Error.Code.ShouldBe("Customers.InvalidCci");

        var wrongLen = validator.Validate("0021910000");
        wrongLen.IsFailure.ShouldBeTrue();
        wrongLen.Error.Code.ShouldBe("Customers.InvalidCci");
    }

    [Fact]
    public void Register_SavingsAccount_EncryptsHashesAndMasksWithoutExposingPlaintext()
    {
        var clock = new FakeClock(BaseUtc);
        var validator = new CciValidator(new StubBanksCatalog());
        var protector = new StubProtector();

        var res = CustomerBankAccount.Register(
            Guid.NewGuid(),
            BankAccountType.CUENTA_AHORRO,
            null,
            "002 1910000000000 5678",
            null,
            null,
            "  José María Quispe  ",
            validator,
            protector,
            clock);

        res.IsSuccess.ShouldBeTrue();
        var acct = res.Value;
        acct.BankName.ShouldBe("Banco de Crédito del Perú");
        acct.BankCode.ShouldBe("002");
        acct.AccountNumberMasked.ShouldBe("****5678");
        acct.AccountNumberEncrypted.ShouldBe("ENC(00219100000000005678)");
        acct.AccountNumberHash.ShouldBe("HASH(00219100000000005678)");
        acct.Status.ShouldBe(BankAccountStatus.PENDING_VERIFICATION);
        acct.IsPrimary.ShouldBeFalse();
        acct.IsVerified.ShouldBeFalse();
    }

    [Fact]
    public void Register_DebitCard_RejectsRawPanAndAcceptsToken()
    {
        var clock = new FakeClock(BaseUtc);
        var validator = new CciValidator(new StubBanksCatalog());
        var protector = new StubProtector();

        var rawPan = CustomerBankAccount.Register(
            Guid.NewGuid(),
            BankAccountType.TARJETA_DEBITO,
            "BBVA",
            null,
            null,
            "4111-1111-1111-1111",
            "Ana",
            validator,
            protector,
            clock);
        rawPan.IsFailure.ShouldBeTrue();
        rawPan.Error.Code.ShouldBe("Customers.RawCardNotAllowed");

        var tokenized = CustomerBankAccount.Register(
            Guid.NewGuid(),
            BankAccountType.TARJETA_DEBITO,
            "BBVA",
            null,
            null,
            "tok_visa_99887766",
            "Ana",
            validator,
            protector,
            clock);
        tokenized.IsSuccess.ShouldBeTrue();
        tokenized.Value.GatewayToken.ShouldBe("tok_visa_99887766");
        tokenized.Value.AccountNumberEncrypted.ShouldBeNull();
        tokenized.Value.Last4.ShouldBe("7766");
    }

    [Fact]
    public void VerifyOwnershipAndPrimaryPromotion_EnforceTitularityAndCoolingPeriod()
    {
        var clock = new FakeClock(BaseUtc);
        var validator = new CciValidator(new StubBanksCatalog());
        var protector = new StubProtector();
        var acct = CustomerBankAccount.Register(
            Guid.NewGuid(),
            BankAccountType.BILLETERA_DIGITAL,
            "Yape",
            null,
            "+51 987 654 321",
            null,
            "José Quispe",
            validator,
            protector,
            clock).Value;

        acct.SetAsFirstPrimary(clock).IsFailure.ShouldBeTrue();

        var mismatch = acct.VerifyOwnership(Guid.NewGuid(), "Pedro Quispe", "JOSE QUISPE", "MANUAL", null, clock);
        mismatch.IsFailure.ShouldBeTrue();
        mismatch.Error.Code.ShouldBe("Customers.AccountHolderMismatch");

        var verified = acct.VerifyOwnership(Guid.NewGuid(), "  José   Quispe ", "JOSE QUISPE", "MANUAL", "ok", clock);
        verified.IsSuccess.ShouldBeTrue();
        acct.Status.ShouldBe(BankAccountStatus.ACTIVE);

        var promoted = acct.PromoteToPrimaryWithCooling(TimeSpan.FromHours(24), clock);
        promoted.IsSuccess.ShouldBeTrue();
        acct.IsPrimary.ShouldBeTrue();
        acct.DisbursementEnabledFrom.ShouldBe(clock.UtcNow.AddHours(24));

        acct.Deactivate(clock);
        acct.Status.ShouldBe(BankAccountStatus.INACTIVE);
        acct.IsPrimary.ShouldBeFalse();
        acct.DeactivatedAt.ShouldBe(clock.UtcNow);
    }

    private sealed class StubBanksCatalog : IBanksCatalog
    {
        private static readonly Dictionary<string, string> Banks = new()
        {
            ["002"] = "Banco de Crédito del Perú",
            ["011"] = "BBVA Perú"
        };

        public bool TryGetBankName(string bankCode, out string bankName) =>
            Banks.TryGetValue(bankCode, out bankName!);

        public IReadOnlyDictionary<string, string> GetAll() => Banks;
    }

    private sealed class StubProtector : IFieldProtector
    {
        public string Protect(string plaintext) => $"ENC({plaintext})";
        public string Unprotect(string protectedValue) => protectedValue[4..^1];
        public string Hash(string plaintext) => $"HASH({plaintext})";
    }

    private sealed class FakeClock(DateTimeOffset? now = null) : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = now ?? BaseUtc;
        public DateTime LimaNow => UtcNow.UtcDateTime.AddHours(-5);
        public DateOnly LimaToday => DateOnly.FromDateTime(LimaNow);
    }
}
