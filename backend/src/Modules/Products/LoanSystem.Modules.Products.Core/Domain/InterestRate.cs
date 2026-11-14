namespace LoanSystem.Modules.Products.Core.Domain;

/// <summary>Mirrors the PostgreSQL enum <c>rate_type</c> ('NOMINAL', 'EFECTIVA').</summary>
internal enum RateType
{
    Efectiva = 1,
    Nominal = 2
}

internal static class RateTypeParser
{
    public static bool TryParse(string? value, out RateType rateType)
    {
        switch (value?.Trim().ToUpperInvariant())
        {
            case "EFECTIVA":
            case "TEA":
                rateType = RateType.Efectiva;
                return true;
            case "NOMINAL":
            case "TNA":
                rateType = RateType.Nominal;
                return true;
            default:
                rateType = default;
                return false;
        }
    }
}

/// <summary>
/// Converts an annual rate into the period rate used by the amortization engine, with a 360-day year
/// (plan D2). See backend/docs/DECISION_01_TASA_EFECTIVA_TEA.md.
/// <list type="bullet">
/// <item>EFECTIVA (TEA, Peruvian standard): i_d = (1 + TEA)^(d/360) - 1</item>
/// <item>NOMINAL (TNA): i_d = TNA * d / 360</item>
/// </list>
/// With d = 30 this is exactly the monthly conversion (1 + TEA)^(1/12) - 1 and TNA / 12.
/// Everything is computed in decimal (no floating point, enforced by architecture tests).
/// The returned rate is NOT rounded: rounding happens per component when interest becomes Money.
/// </summary>
internal static class InterestRate
{
    public const int DaysPerYear = 360;
    private const int MaxIterations = 100;
    private static readonly decimal Tolerance = 0.0000000000000000000001m; // 1e-22

    /// <param name="annualRatePercentage">Annual rate in percent (30 = 30 %).</param>
    public static decimal ToMonthlyRate(decimal annualRatePercentage, RateType rateType)
    {
        if (annualRatePercentage < 0)
            throw new ArgumentOutOfRangeException(nameof(annualRatePercentage), "Rate cannot be negative.");

        return ToPeriodRate(annualRatePercentage / 100m, rateType, 30);
    }

    /// <summary>Rate of a period of <paramref name="days"/> days (PRO-02.1).</summary>
    /// <param name="annualRateFraction">Annual rate as a fraction (0.30 = 30 %).</param>
    public static decimal ToPeriodRate(decimal annualRateFraction, RateType rateType, int days)
    {
        if (annualRateFraction < 0)
            throw new ArgumentOutOfRangeException(nameof(annualRateFraction), "Rate cannot be negative.");
        if (days <= 0)
            throw new ArgumentOutOfRangeException(nameof(days), "Days must be greater than zero.");

        if (annualRateFraction == 0m)
            return 0m;

        switch (rateType)
        {
            case RateType.Efectiva:
                // (1+TEA)^(days/360) reduced to (1+TEA)^(m/n) so only integer powers and n-th roots are used.
                var g = Gcd(days, DaysPerYear);
                var n = DaysPerYear / g;
                var m = days / g;
                return Pow(NthRoot(1m + annualRateFraction, n), m) - 1m;
            case RateType.Nominal:
                return annualRateFraction * days / DaysPerYear;
            default:
                throw new ArgumentOutOfRangeException(nameof(rateType), rateType, "Unsupported rate type.");
        }
    }

    /// <summary>
    /// Daily growth factor q = (1 + TEA)^(1/360). With it the rate of any period of d days is q^d - 1,
    /// so a schedule with many different period lengths needs a single root (plan: DailyGrowthFactor).
    /// </summary>
    public static decimal DailyGrowthFactor(decimal annualRateFraction)
    {
        if (annualRateFraction < 0)
            throw new ArgumentOutOfRangeException(nameof(annualRateFraction), "Rate cannot be negative.");

        return annualRateFraction == 0m ? 1m : NthRoot(1m + annualRateFraction, DaysPerYear);
    }

    private static int Gcd(int a, int b)
    {
        while (b != 0)
            (a, b) = (b, a % b);
        return a;
    }

    /// <summary>
    /// n-th root of <paramref name="value"/> (value &gt; 1) using Newton's method in decimal:
    /// x_{k+1} = x_k - (x_k^n - a) / (n * x_k^(n-1)).
    /// Starting above the root (1 + (a-1)/n, Bernoulli bound) guarantees monotonic convergence.
    /// </summary>
    internal static decimal NthRoot(decimal value, int n)
    {
        if (n == 1)
            return value;

        var x = 1m + (value - 1m) / n;
        for (var i = 0; i < MaxIterations; i++)
        {
            var xPowNMinus1 = Pow(x, n - 1);
            var delta = (xPowNMinus1 * x - value) / (n * xPowNMinus1);
            x -= delta;
            if (Math.Abs(delta) < Tolerance)
                break;
        }

        return x;
    }

    internal static decimal Pow(decimal value, int exponent)
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
