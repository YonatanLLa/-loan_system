using LoanSystem.Common.Core.Domain;

namespace LoanSystem.Modules.Products.Core.Domain;

internal sealed record ScheduleInstallment(
    int Number,
    Money Principal,
    Money Interest,
    Money Total,
    Money RemainingBalance);

internal sealed record AmortizationSchedule(
    Money Principal,
    Money TotalInterest,
    Money TotalPayment,
    IReadOnlyList<ScheduleInstallment> Installments);

/// <summary>
/// French (constant installment) amortization computed exclusively with 128-bit decimal
/// and Half-Up rounding. The last installment absorbs the residual centavo so that the
/// amortized principal always matches the loan principal exactly.
/// </summary>
internal static class FrenchAmortizationSchedule
{
    public static Money CalculateInstallment(Money principal, decimal monthlyRate, int termMonths)
    {
        if (termMonths <= 0)
            throw new ArgumentOutOfRangeException(nameof(termMonths), "Term must be greater than zero.");
        if (monthlyRate < 0)
            throw new ArgumentOutOfRangeException(nameof(monthlyRate), "Rate cannot be negative.");

        if (monthlyRate == 0m)
            return principal / termMonths;

        // quota = P * r * (1+r)^n / ((1+r)^n - 1)
        var growth = Pow(1m + monthlyRate, termMonths);
        return new Money(principal.Amount * monthlyRate * growth / (growth - 1m), principal.Currency);
    }

    public static AmortizationSchedule Build(Money principal, decimal annualRatePercentage, int termMonths)
    {
        var monthlyRate = annualRatePercentage / 100m / 12m;
        var installment = CalculateInstallment(principal, monthlyRate, termMonths);

        var installments = new List<ScheduleInstallment>(termMonths);
        var balance = principal;
        var totalInterest = Money.Zero(principal.Currency);

        for (var i = 1; i <= termMonths; i++)
        {
            var interest = balance * monthlyRate;
            Money principalPart;
            Money total;

            if (i == termMonths)
            {
                // Residual centavo adjustment on the last installment
                principalPart = balance;
                total = principalPart + interest;
            }
            else
            {
                principalPart = installment - interest;
                if (principalPart > balance)
                    principalPart = balance;
                total = principalPart + interest;
            }

            balance -= principalPart;
            totalInterest += interest;

            installments.Add(new ScheduleInstallment(i, principalPart, interest, total, balance));
        }

        return new AmortizationSchedule(principal, totalInterest, principal + totalInterest, installments);
    }

    /// <summary>Exponentiation by squaring in decimal (no floating point).</summary>
    private static decimal Pow(decimal value, int exponent)
    {
        var result = 1m;
        var b = value;
        var e = exponent;
        while (e > 0)
        {
            if ((e & 1) == 1)
                result *= b;
            b *= b;
            e >>= 1;
        }

        return result;
    }
}
