using LoanSystem.Common.Core.Domain;
using LoanSystem.Modules.Products.Core.Domain;
using LoanSystem.Modules.Products.Core.Domain.Engine;
using Shouldly;
using Xunit;

namespace LoanSystem.Domain.UnitTests.Products;

/// <summary>Opening fee + schedule + TCEA together (BE-PRO-03, PRO-T).</summary>
public class LoanQuoteCalculatorTests
{
    private static LoanQuoteInput Input(
        decimal feeRate = 0m,
        CommissionMode mode = CommissionMode.DEDUCTIVA,
        decimal periodicFee = 0m,
        AmortizationMethod method = AmortizationMethod.FRANCES,
        RateType rateType = RateType.Efectiva,
        decimal rate = 0.30m) =>
        new(Money.From(10000m), rate, rateType, PaymentFrequency.MENSUAL, 12, method,
            new OpeningFeeTerms(feeRate, Money.Zero(), mode), Money.From(periodicFee));

    [Fact] // CA-T.a / V5 end to end
    public void V5_DeductiveFeeAndPeriodicCharge_Tcea3654()
    {
        var quote = LoanQuoteCalculator.Calculate(Input(feeRate: 0.02m, periodicFee: 5m)).Value;

        quote.OpeningFee.Fee.Amount.ShouldBe(200m);
        quote.OpeningFee.NetDisbursement.Amount.ShouldBe(9800m);
        quote.Schedule.Principal.Amount.ShouldBe(10000m);
        quote.Schedule.InstallmentAmount.Amount.ShouldBe(962.86m);
        quote.Schedule.TotalFees.Amount.ShouldBe(60m); // CA-03.c: 5 · n
        quote.TeaFraction.ShouldBe(0.30m);
        Math.Round(quote.TceaFraction * 100m, 2).ShouldBe(36.54m);
    }

    [Fact] // CA-03.b: schedule on 10 200
    public void FinancedFee_BuildsScheduleOnRequestedPlusFee()
    {
        var quote = LoanQuoteCalculator.Calculate(Input(feeRate: 0.02m, mode: CommissionMode.FINANCIADA)).Value;

        quote.Schedule.Principal.Amount.ShouldBe(10200m);
        quote.Schedule.Lines.Sum(l => l.Principal.Amount).ShouldBe(10200m);
        quote.OpeningFee.NetDisbursement.Amount.ShouldBe(10000m);
        quote.TceaFraction.ShouldBeGreaterThan(0.30m);
    }

    [Fact] // CA-T.b
    public void NoFees_TceaEqualsTea()
    {
        var quote = LoanQuoteCalculator.Calculate(Input()).Value;

        Math.Abs(quote.TceaFraction - quote.TeaFraction).ShouldBeLessThan(0.0001m);
    }

    [Fact] // D12: FLAT informs its real effective rate as TEA
    public void Flat_InformsRealTea()
    {
        var quote = LoanQuoteCalculator.Calculate(Input(method: AmortizationMethod.FLAT)).Value;

        Math.Round(quote.TeaFraction * 100m, 2).ShouldBe(56.80m);
    }

    [Fact] // TNA 12 % monthly → TEA (1.01)^12 − 1 = 12.68 %
    public void NominalRate_InformsEquivalentTea()
    {
        var quote = LoanQuoteCalculator.Calculate(Input(rateType: RateType.Nominal, rate: 0.12m)).Value;

        Math.Round(quote.TeaFraction * 100m, 2).ShouldBe(12.68m);
    }

    [Fact]
    public void DeductiveFeeTooLarge_FailsBeforeBuildingTheSchedule()
    {
        var input = Input() with { OpeningFee = new OpeningFeeTerms(0m, Money.From(10000m), CommissionMode.DEDUCTIVA) };

        LoanQuoteCalculator.Calculate(input).Error.Code.ShouldBe("Products.OpeningFeeExceedsAmount");
    }
}
