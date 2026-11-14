using LoanSystem.Common.Core.Application;
using LoanSystem.Common.Core.Domain;

namespace LoanSystem.Modules.Products.Core.Domain.Engine;

/// <summary>
/// Pure amortization engine (BE-PRO-02): no database, clock or I/O (plan D14).
/// Builds a schedule that balances to the centavo: Σ principal = financed principal and final balance = 0.
/// </summary>
internal static class ScheduleEngine
{
    private static readonly IReadOnlyDictionary<AmortizationMethod, IAmortizationStrategy> Strategies =
        new IAmortizationStrategy[] { new FrenchStrategy(), new GermanStrategy(), new AmericanStrategy(), new FlatStrategy() }
            .ToDictionary(s => s.Method);

    public static Result<ScheduleResult> Build(ScheduleInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var currency = input.Principal.Currency;
        var periodicFee = input.PeriodicFee ?? Money.Zero(currency);

        var validation = Validate(input, periodicFee);
        if (validation is not null)
            return validation;

        var mode = input.DisbursementDate is null ? DayCountMode.NOMINAL_PERIODS : DayCountMode.ACTUAL_360;
        var nominalDays = input.Frequency.NominalDays();
        var nominalRate = InterestRate.ToPeriodRate(input.AnnualRateFraction, input.RateType, nominalDays);

        var days = new int[input.Installments];
        var rates = new decimal[input.Installments];

        if (mode == DayCountMode.NOMINAL_PERIODS)
        {
            Array.Fill(days, nominalDays);
            Array.Fill(rates, nominalRate);
        }
        else
        {
            // Interest runs up to the THEORETICAL due date (plan D4): d_k = theoretical_k − theoretical_{k−1}.
            var dailyFactor = input.RateType == RateType.Efectiva
                ? InterestRate.DailyGrowthFactor(input.AnnualRateFraction)
                : 0m;
            var previous = input.DisbursementDate!.Value;

            for (var k = 0; k < input.Installments; k++)
            {
                var theoretical = input.DueDates![k].TheoreticalDueDate;
                days[k] = theoretical.DayNumber - previous.DayNumber;
                rates[k] = input.RateType == RateType.Efectiva
                    ? InterestRate.Pow(dailyFactor, days[k]) - 1m
                    : InterestRate.ToPeriodRate(input.AnnualRateFraction, input.RateType, days[k]);
                previous = theoretical;
            }
        }

        var context = new AmortizationContext(input.Principal, input.Installments, mode, rates, nominalRate);
        var amortization = Strategies[input.Method].Amortize(context);
        if (amortization.IsFailure)
            return amortization.Error;

        var lines = new List<ScheduleLine>(input.Installments);
        var balance = input.Principal;
        var totalInterest = Money.Zero(currency);

        for (var k = 0; k < input.Installments; k++)
        {
            var row = amortization.Value[k];
            balance -= row.Principal;
            totalInterest += row.Interest;

            lines.Add(new ScheduleLine(
                Number: k + 1,
                TheoreticalDueDate: input.DueDates?[k].TheoreticalDueDate,
                DueDate: input.DueDates?[k].DueDate,
                Days: days[k],
                Principal: row.Principal,
                Interest: row.Interest,
                Fees: periodicFee,
                Total: row.Principal + row.Interest + periodicFee,
                RemainingBalance: balance));
        }

        var balanced = EnsureBalanced(input.Principal, lines);
        if (balanced.IsFailure)
            return balanced.Error;

        var totalFees = periodicFee * input.Installments;
        var effectiveAnnualRate = input.Method == AmortizationMethod.FLAT
            ? EffectiveAnnualRate(input.Principal, lines)
            : (decimal?)null;

        return new ScheduleResult(
            Mode: mode,
            Method: input.Method,
            Frequency: input.Frequency,
            ReferencePeriodRate: nominalRate,
            Principal: input.Principal,
            InstallmentAmount: lines[0].Total,
            TotalInterest: totalInterest,
            TotalFees: totalFees,
            TotalPayable: input.Principal + totalInterest + totalFees,
            EffectiveAnnualRate: effectiveAnnualRate,
            Lines: lines);
    }

    /// <summary>
    /// Safety net (PRO-02.5 step 4): Σ principal = financed principal, no negative principal and final balance = 0.
    /// Should never fail; if it does the schedule must not reach a customer.
    /// </summary>
    internal static Result EnsureBalanced(Money principal, IReadOnlyList<ScheduleLine> lines)
    {
        var amortized = lines.Aggregate(Money.Zero(principal.Currency), (sum, line) => sum + line.Principal);
        var balanced = lines.Count > 0
                       && amortized == principal
                       && lines[^1].RemainingBalance.Amount == 0m
                       && lines.All(l => l.Principal.Amount >= 0m && l.Interest.Amount >= 0m);

        return balanced
            ? Result.Success()
            : Error.Failure(
                "Products.ScheduleNotBalanced",
                $"Schedule does not balance: amortized {amortized.Amount} of {principal.Amount}.");
    }

    /// <summary>Real effective annual rate of the principal + interest flow (plan D12, FLAT only).</summary>
    private static decimal EffectiveAnnualRate(Money principal, IReadOnlyList<ScheduleLine> lines)
    {
        var flows = new CashFlow[lines.Count];
        var elapsed = 0;
        for (var k = 0; k < lines.Count; k++)
        {
            elapsed += lines[k].Days;
            flows[k] = new CashFlow(elapsed, lines[k].Principal + lines[k].Interest);
        }

        return TceaCalculator.Calculate(principal, flows);
    }

    private static Error? Validate(ScheduleInput input, Money periodicFee)
    {
        if (input.Principal.Amount <= 0)
            return Error.Validation("Products.InvalidAmount", "Principal must be greater than zero.");

        if (input.Installments < 1)
            return Error.Validation("Products.InvalidTerm", "Installments must be at least 1.");

        if (input.AnnualRateFraction < 0)
            return Error.Validation("Products.InvalidRate", "Annual rate cannot be negative.");

        if (periodicFee.Amount < 0)
            return Error.Validation("Products.InvalidPeriodicFee", "Periodic fee cannot be negative.");

        if (periodicFee.Currency != input.Principal.Currency)
            return Error.Validation("Products.InvalidPeriodicFee", "Periodic fee currency must match the principal currency.");

        if (!Strategies.ContainsKey(input.Method))
            return Error.Validation("Products.InvalidMethod", $"Amortization method '{input.Method}' is not supported.");

        if (input.DisbursementDate is not { } disbursement)
            return null;

        if (input.DueDates is null || input.DueDates.Count != input.Installments)
            return Error.Validation("Products.InvalidDueDates", "ACTUAL_360 requires exactly one due date per installment.");

        var previous = disbursement;
        foreach (var pair in input.DueDates)
        {
            if (pair.TheoreticalDueDate <= previous)
                return Error.Validation("Products.InvalidDueDates", "Theoretical due dates must be strictly increasing and after the disbursement.");
            if (pair.DueDate <= disbursement)
                return Error.Validation("Products.InvalidDueDates", "Due dates must be after the disbursement.");
            previous = pair.TheoreticalDueDate;
        }

        return null;
    }
}
