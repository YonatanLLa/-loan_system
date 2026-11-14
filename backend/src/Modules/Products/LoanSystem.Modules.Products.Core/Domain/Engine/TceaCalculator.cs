using LoanSystem.Common.Core.Domain;

namespace LoanSystem.Modules.Products.Core.Domain.Engine;

/// <summary>A payment made <see cref="DaysFromDisbursement"/> days after the disbursement.</summary>
internal readonly record struct CashFlow(int DaysFromDisbursement, Money Total);

/// <summary>
/// Annual effective cost of a flow (PRO-T.1): the rate that makes the net disbursement equal to the present value
/// of the payments over a 360-day year. Used for the TCEA (BE-ORI-04) and for the real TEA of FLAT (plan D12).
/// <para>
/// Solved for the daily discount factor v = (1 + TCEA)^(−1/360): f(v) = net − Σ T_k · v^{D_k} = 0.
/// f is decreasing and concave in v, so Newton started at v = 1 (where f &lt; 0) converges monotonically
/// without overshooting; bisection is kept as a safety net. Only integer powers in decimal (no Math.Pow).
/// </para>
/// </summary>
internal static class TceaCalculator
{
    private const int MaxNewtonIterations = 50;
    private const int MaxBisectionIterations = 200;
    private static readonly decimal Tolerance = 0.000000000000000001m; // 1e-18
    private static readonly decimal LowerDiscount = 1m / 1.01m;       // q = 1.01 per day ≈ 3 500 % annual

    /// <returns>Annual effective rate as an unrounded fraction (0.3654 = 36.54 %).</returns>
    public static decimal Calculate(Money netDisbursement, IReadOnlyList<CashFlow> flows)
    {
        ArgumentNullException.ThrowIfNull(flows);
        if (netDisbursement.Amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(netDisbursement), "Net disbursement must be greater than zero.");
        if (flows.Count == 0)
            throw new ArgumentException("At least one cash flow is required.", nameof(flows));
        if (flows.Any(f => f.DaysFromDisbursement <= 0 || f.Total.Amount < 0))
            throw new ArgumentException("Cash flows must be non-negative and after the disbursement.", nameof(flows));

        var net = netDisbursement.Amount;
        var totalPaid = flows.Sum(f => f.Total.Amount);
        if (totalPaid == net)
            return 0m;
        if (totalPaid < net)
            throw new ArgumentException("Payments below the net disbursement (negative cost) are not supported.", nameof(flows));

        var v = Newton(net, flows) ?? Bisection(net, flows);
        return InterestRate.Pow(1m / v, InterestRate.DaysPerYear) - 1m;
    }

    private static decimal? Newton(decimal net, IReadOnlyList<CashFlow> flows)
    {
        var v = 1m;
        for (var i = 0; i < MaxNewtonIterations; i++)
        {
            var (f, derivative) = Evaluate(net, flows, v);
            if (derivative == 0m)
                return null;

            var step = f / derivative;
            v -= step;
            if (v <= 0m || v > 1m)
                return null;
            if (Math.Abs(step) < Tolerance)
                return v;
        }

        return null;
    }

    private static decimal Bisection(decimal net, IReadOnlyList<CashFlow> flows)
    {
        // f(low) > 0 (discounting heavily) and f(1) < 0; f is decreasing in v.
        var low = LowerDiscount;
        var high = 1m;
        if (Evaluate(net, flows, low).F < 0m)
            throw new InvalidOperationException("Effective cost is above the supported range (q > 1.01 per day).");

        for (var i = 0; i < MaxBisectionIterations && high - low >= Tolerance; i++)
        {
            var mid = (low + high) / 2m;
            if (Evaluate(net, flows, mid).F > 0m)
                low = mid;
            else
                high = mid;
        }

        return (low + high) / 2m;
    }

    /// <summary>f(v) = net − Σ T·v^D and f'(v) = −Σ T·D·v^(D−1).</summary>
    private static (decimal F, decimal Derivative) Evaluate(decimal net, IReadOnlyList<CashFlow> flows, decimal v)
    {
        var presentValue = 0m;
        var derivative = 0m;
        foreach (var flow in flows)
        {
            var vPowDMinus1 = InterestRate.Pow(v, flow.DaysFromDisbursement - 1);
            presentValue += flow.Total.Amount * vPowDMinus1 * v;
            derivative -= flow.Total.Amount * flow.DaysFromDisbursement * vPowDMinus1;
        }

        return (net - presentValue, derivative);
    }
}
