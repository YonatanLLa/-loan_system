using LoanSystem.Common.Core.Application;
using LoanSystem.Common.Core.Domain;
using LoanSystem.Modules.Products.Core.Domain;
using LoanSystem.Modules.Products.Core.Domain.Engine;
using Shouldly;
using Xunit;

namespace LoanSystem.Domain.UnitTests.Products;

/// <summary>BE-PRO-03 acceptance criteria.</summary>
public class OpeningFeeCalculatorTests
{
    private static readonly Money P10000 = Money.From(10000m);

    [Fact] // PRO-03.1: round(requested · rate) + fixed
    public void Calculate_AddsPercentageAndFixed()
    {
        OpeningFeeCalculator.Calculate(Money.From(1234.56m), 0.015m, Money.From(10m)).Amount.ShouldBe(28.52m); // 18.5184 → 18.52
    }

    [Fact] // CA-03.a
    public void Deductive_ReducesTheNetDisbursement()
    {
        var result = OpeningFeeCalculator.Apply(P10000, new OpeningFeeTerms(0.02m, Money.Zero(), CommissionMode.DEDUCTIVA));

        result.IsSuccess.ShouldBeTrue();
        result.Value.Fee.Amount.ShouldBe(200m);
        result.Value.FinancedPrincipal.Amount.ShouldBe(10000m);
        result.Value.NetDisbursement.Amount.ShouldBe(9800m);
    }

    [Fact] // CA-03.b
    public void Financed_IncreasesThePrincipal()
    {
        var result = OpeningFeeCalculator.Apply(P10000, new OpeningFeeTerms(0.02m, Money.Zero(), CommissionMode.FINANCIADA));

        result.IsSuccess.ShouldBeTrue();
        result.Value.FinancedPrincipal.Amount.ShouldBe(10200m);
        result.Value.NetDisbursement.Amount.ShouldBe(10000m);
    }

    [Fact] // PRO-03.2: deductive fee that eats the whole amount → 422
    public void Deductive_FeeEqualToAmount_IsUnprocessable()
    {
        var result = OpeningFeeCalculator.Apply(Money.From(50m), new OpeningFeeTerms(0m, Money.From(50m), CommissionMode.DEDUCTIVA));

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Unprocessable);
        result.Error.Code.ShouldBe("Products.OpeningFeeExceedsAmount");
    }

    [Fact] // a large fee is fine when it is financed
    public void Financed_FeeLargerThanAmount_IsAllowed()
    {
        var result = OpeningFeeCalculator.Apply(Money.From(50m), new OpeningFeeTerms(0m, Money.From(60m), CommissionMode.FINANCIADA));

        result.IsSuccess.ShouldBeTrue();
        result.Value.FinancedPrincipal.Amount.ShouldBe(110m);
    }

    [Theory] // PRO-03.4
    [InlineData(-0.01, 0)]
    [InlineData(0.2001, 0)]
    [InlineData(0.02, -1)]
    public void InvalidTerms_AreRejected(decimal rate, decimal fixedAmount)
    {
        var result = OpeningFeeCalculator.Apply(P10000, new OpeningFeeTerms(rate, Money.From(fixedAmount), CommissionMode.DEDUCTIVA));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Products.InvalidOpeningFee");
    }

    [Fact] // PRO-03.4: in DEDUCTIVA the fee on min_amount must be smaller than min_amount
    public void ValidateProductTerms_DeductiveFeeOnMinAmount()
    {
        var terms = new OpeningFeeTerms(0.05m, Money.From(100m), CommissionMode.DEDUCTIVA);

        OpeningFeeCalculator.ValidateProductTerms(terms, Money.From(105m))!.Code.ShouldBe("Products.OpeningFeeExceedsAmount"); // 5.25 + 100 ≥ 105
        OpeningFeeCalculator.ValidateProductTerms(terms, Money.From(200m)).ShouldBeNull();
        OpeningFeeCalculator.ValidateProductTerms(terms with { Mode = CommissionMode.FINANCIADA }, Money.From(105m)).ShouldBeNull();
    }

    [Fact]
    public void ValidateProductTerms_RespectsConfiguredMaxRate()
    {
        var terms = new OpeningFeeTerms(0.10m, Money.Zero(), CommissionMode.FINANCIADA);

        OpeningFeeCalculator.ValidateProductTerms(terms, Money.From(500m), maxRateFraction: 0.05m)!.Code.ShouldBe("Products.InvalidOpeningFee");
    }

    [Theory]
    [InlineData("DEDUCTIVA", "DEDUCTIVA")]
    [InlineData(" financiada ", "FINANCIADA")]
    public void Parser_AcceptsPgLabels(string value, string expected)
    {
        CommissionModeParser.TryParse(value, out var mode).ShouldBeTrue();
        mode.ToString().ShouldBe(expected);
    }

    [Fact]
    public void Parser_RejectsUnknown()
    {
        CommissionModeParser.TryParse("DEDUCTIVE", out _).ShouldBeFalse();
    }
}
