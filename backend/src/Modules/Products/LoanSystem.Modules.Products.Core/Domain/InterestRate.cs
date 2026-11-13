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
/// Converts an annual rate into the monthly period rate used by the amortization engine.
/// See backend/docs/DECISION_01_TASA_EFECTIVA_TEA.md.
/// <list type="bullet">
/// <item>EFECTIVA (TEA, Peruvian standard): i = (1 + TEA)^(1/12) - 1</item>
/// <item>NOMINAL (TNA): i = TNA / 12</item>
/// </list>
/// Everything is computed in decimal (no floating point, enforced by architecture tests).
/// The returned rate is NOT rounded: rounding happens per component when interest becomes Money.
/// </summary>
internal static class InterestRate
{
    private const int PeriodsPerYear = 12;
    private const int MaxIterations = 100;
    private static readonly decimal Tolerance = 0.0000000000000000000001m; // 1e-22

    public static decimal ToMonthlyRate(decimal annualRatePercentage, RateType rateType)
    {
        if (annualRatePercentage < 0)
            throw new ArgumentOutOfRangeException(nameof(annualRatePercentage), "Rate cannot be negative.");

        var annualRate = annualRatePercentage / 100m;
        if (annualRate == 0m)
            return 0m;

        return rateType switch
        {
            RateType.Efectiva => NthRoot(1m + annualRate, PeriodsPerYear) - 1m,
            RateType.Nominal => annualRate / PeriodsPerYear,
            _ => throw new ArgumentOutOfRangeException(nameof(rateType), rateType, "Unsupported rate type.")
        };
    }

    /// <summary>
    /// n-th root of <paramref name="value"/> (value &gt; 1) using Newton's method in decimal:
    /// x_{k+1} = x_k - (x_k^n - a) / (n * x_k^(n-1)).
    /// Starting above the root (1 + (a-1)/n, Bernoulli bound) guarantees monotonic convergence.
    /// </summary>
    private static decimal NthRoot(decimal value, int n)
    {
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
