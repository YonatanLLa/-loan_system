using LoanSystem.Common.Core.Application;
using LoanSystem.Common.Core.Domain;

namespace LoanSystem.Modules.Products.Core.Domain.Engine;

/// <summary>Everything needed to quote a loan from frozen terms (no product lookup).</summary>
/// <param name="RequestedAmount">Amount the customer asks for (product limits apply to this one, plan D11).</param>
/// <param name="AnnualRateFraction">TEA as a fraction (0.30 = 30 %).</param>
internal sealed record LoanQuoteInput(
    Money RequestedAmount,
    decimal AnnualRateFraction,
    RateType RateType,
    PaymentFrequency Frequency,
    int Installments,
    AmortizationMethod Method,
    OpeningFeeTerms OpeningFee,
    Money PeriodicFee,
    DateOnly? DisbursementDate = null,
    IReadOnlyList<DueDatePair>? DueDates = null);

/// <param name="TeaFraction">
/// TEA informed to the customer: the product rate, or for FLAT the real effective rate of the flow (plan D12).
/// </param>
/// <param name="TceaFraction">Annual effective cost: net disbursement vs. every installment total (PRO-T.2).</param>
internal sealed record LoanQuote(
    OpeningFeeBreakdown OpeningFee,
    ScheduleResult Schedule,
    decimal TeaFraction,
    decimal TceaFraction);

/// <summary>
/// Pure quote (BE-PRO-03 + TCEA): applies the opening fee, builds the schedule on the financed principal
/// and computes the TCEA. Late fees and prepayment penalties are contingent and never enter the TCEA.
/// </summary>
internal static class LoanQuoteCalculator
{
    public static Result<LoanQuote> Calculate(LoanQuoteInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var fee = OpeningFeeCalculator.Apply(input.RequestedAmount, input.OpeningFee);
        if (fee.IsFailure)
            return fee.Error;

        var schedule = ScheduleEngine.Build(new ScheduleInput(
            Principal: fee.Value.FinancedPrincipal,
            AnnualRateFraction: input.AnnualRateFraction,
            RateType: input.RateType,
            Frequency: input.Frequency,
            Installments: input.Installments,
            Method: input.Method,
            DisbursementDate: input.DisbursementDate,
            DueDates: input.DueDates,
            PeriodicFee: input.PeriodicFee));
        if (schedule.IsFailure)
            return schedule.Error;

        var tcea = TceaCalculator.Calculate(fee.Value.NetDisbursement, ToCashFlows(schedule.Value.Lines));
        var tea = schedule.Value.EffectiveAnnualRate ?? EffectiveTea(input);

        return new LoanQuote(fee.Value, schedule.Value, tea, tcea);
    }

    /// <summary>D_k = days from the disbursement to the theoretical due date k (k·d in NOMINAL_PERIODS).</summary>
    internal static IReadOnlyList<CashFlow> ToCashFlows(IReadOnlyList<ScheduleLine> lines)
    {
        var flows = new CashFlow[lines.Count];
        var elapsed = 0;
        for (var k = 0; k < lines.Count; k++)
        {
            elapsed += lines[k].Days;
            flows[k] = new CashFlow(elapsed, lines[k].Total);
        }

        return flows;
    }

    /// <summary>For a NOMINAL rate (quick simulator only) the TEA informed is (1 + TNA·d/360)^(360/d) − 1.</summary>
    private static decimal EffectiveTea(LoanQuoteInput input)
    {
        if (input.RateType == RateType.Efectiva)
            return input.AnnualRateFraction;

        var days = input.Frequency.NominalDays();
        var periodRate = InterestRate.ToPeriodRate(input.AnnualRateFraction, RateType.Nominal, days);
        if (periodRate == 0m)
            return 0m;

        // (1+i)^(360/d) = NthRoot((1+i)^(360/g), d/g) with g = gcd(360, d): integer powers and roots only.
        var g = Gcd(InterestRate.DaysPerYear, days);
        return InterestRate.NthRoot(InterestRate.Pow(1m + periodRate, InterestRate.DaysPerYear / g), days / g) - 1m;
    }

    private static int Gcd(int a, int b)
    {
        while (b != 0)
            (a, b) = (b, a % b);
        return a;
    }
}
