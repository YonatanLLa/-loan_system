using LoanSystem.Common.Core.Application;
using LoanSystem.Common.Core.Domain;

namespace LoanSystem.Modules.Products.Core.Domain.Engine;

/// <summary>
/// One amortization method (PRO-02.5). Common rules for every strategy:
/// S_0 = P; Interest_k = round(S_{k-1} · i_k); the LAST installment takes Principal_n = S_{n-1}
/// so the residual centavo is absorbed there (RNF-07).
/// </summary>
internal interface IAmortizationStrategy
{
    AmortizationMethod Method { get; }

    Result<IReadOnlyList<AmortizationRow>> Amortize(AmortizationContext context);
}

/// <summary>FRANCES: level installment R; principal_k = R − interest_k.</summary>
internal sealed class FrenchStrategy : IAmortizationStrategy
{
    public AmortizationMethod Method => AmortizationMethod.FRANCES;

    public Result<IReadOnlyList<AmortizationRow>> Amortize(AmortizationContext context)
    {
        var installment = context.Mode == DayCountMode.NOMINAL_PERIODS
            ? CalculateInstallment(context.Principal, context.NominalPeriodRate, context.Installments)
            : CalculateLevelInstallment(context.Principal, context.PeriodRates);

        var rows = new List<AmortizationRow>(context.Installments);
        var balance = context.Principal;

        for (var k = 1; k <= context.Installments; k++)
        {
            var interest = balance * context.PeriodRates[k - 1];
            Money principal;

            if (k == context.Installments)
            {
                principal = balance;
            }
            else
            {
                if (installment < interest)
                    return Error.Validation(
                        "Products.ScheduleNotAmortizing",
                        $"Installment {k} does not cover its interest ({interest.Amount} > {installment.Amount}); the period is too long for this rate.");

                principal = installment - interest;
                if (principal > balance)
                    principal = balance;
            }

            balance -= principal;
            rows.Add(new AmortizationRow(principal, interest));
        }

        return rows;
    }

    /// <summary>
    /// NOMINAL_PERIODS (PRO-02.6): R = round(P · i / (1 − (1+i)^−n)), equivalent to P·i·(1+i)^n / ((1+i)^n − 1)
    /// but written with the discount factor so large terms cannot overflow decimal. With i = 0, R = round(P / n).
    /// </summary>
    public static Money CalculateInstallment(Money principal, decimal periodRate, int installments)
    {
        if (installments <= 0)
            throw new ArgumentOutOfRangeException(nameof(installments), "Installments must be greater than zero.");
        if (periodRate < 0)
            throw new ArgumentOutOfRangeException(nameof(periodRate), "Rate cannot be negative.");

        if (periodRate == 0m)
            return principal / installments;

        var discount = InterestRate.Pow(1m / (1m + periodRate), installments);
        return new Money(principal.Amount * periodRate / (1m - discount), principal.Currency);
    }

    /// <summary>
    /// ACTUAL_360 (PRO-02.6): R = round(P / Σ_k Π_{j≤k} (1 + i_j)^−1). For an effective rate Π_{j≤k}(1+i_j) = q^{D_k},
    /// so this is P / Σ q^−D_k: a level installment even with an irregular first period.
    /// </summary>
    public static Money CalculateLevelInstallment(Money principal, IReadOnlyList<decimal> periodRates)
    {
        var discount = 1m;
        var sum = 0m;
        foreach (var rate in periodRates)
        {
            discount /= 1m + rate;
            sum += discount;
        }

        return new Money(principal.Amount / sum, principal.Currency);
    }
}

/// <summary>ALEMAN: constant principal round(P / n); interest on the balance; decreasing installment.</summary>
internal sealed class GermanStrategy : IAmortizationStrategy
{
    public AmortizationMethod Method => AmortizationMethod.ALEMAN;

    public Result<IReadOnlyList<AmortizationRow>> Amortize(AmortizationContext context)
    {
        var fixedPrincipal = context.Principal / context.Installments;
        var rows = new List<AmortizationRow>(context.Installments);
        var balance = context.Principal;

        for (var k = 1; k <= context.Installments; k++)
        {
            var interest = balance * context.PeriodRates[k - 1];
            var principal = k == context.Installments || fixedPrincipal > balance ? balance : fixedPrincipal;

            balance -= principal;
            rows.Add(new AmortizationRow(principal, interest));
        }

        return rows;
    }
}

/// <summary>AMERICANO: interest only; the whole principal is repaid in the last installment.</summary>
internal sealed class AmericanStrategy : IAmortizationStrategy
{
    public AmortizationMethod Method => AmortizationMethod.AMERICANO;

    public Result<IReadOnlyList<AmortizationRow>> Amortize(AmortizationContext context)
    {
        var zero = Money.Zero(context.Principal.Currency);
        var rows = new List<AmortizationRow>(context.Installments);

        for (var k = 1; k <= context.Installments; k++)
        {
            var interest = context.Principal * context.PeriodRates[k - 1];
            rows.Add(new AmortizationRow(k == context.Installments ? context.Principal : zero, interest));
        }

        return rows;
    }
}

/// <summary>
/// FLAT (PRO-02.7): total interest I = round(P · i · n) on the original principal with the NOMINAL period rate
/// (dates, if any, are only shown). Fixed installment C = round((P + I) / n); interest_k = round(I / n);
/// principal_k = C − interest_k. The last one takes Principal_n = S_{n-1} and Interest_n = I − Σ interest_{k&lt;n}.
/// </summary>
internal sealed class FlatStrategy : IAmortizationStrategy
{
    public AmortizationMethod Method => AmortizationMethod.FLAT;

    public Result<IReadOnlyList<AmortizationRow>> Amortize(AmortizationContext context)
    {
        var n = context.Installments;
        var currency = context.Principal.Currency;
        var zero = Money.Zero(currency);

        var totalInterest = new Money(context.Principal.Amount * context.NominalPeriodRate * n, currency);
        var installment = (context.Principal + totalInterest) / n;
        var fixedInterest = totalInterest / n;

        var rows = new List<AmortizationRow>(n);
        var balance = context.Principal;
        var remainingInterest = totalInterest;

        for (var k = 1; k <= n; k++)
        {
            Money principal;
            Money interest;

            if (k == n)
            {
                principal = balance;
                interest = remainingInterest;
            }
            else
            {
                interest = fixedInterest > remainingInterest ? remainingInterest : fixedInterest;
                principal = installment - interest;
                if (principal < zero)
                    principal = zero;
                if (principal > balance)
                    principal = balance;
            }

            balance -= principal;
            remainingInterest -= interest;
            rows.Add(new AmortizationRow(principal, interest));
        }

        return rows;
    }
}
