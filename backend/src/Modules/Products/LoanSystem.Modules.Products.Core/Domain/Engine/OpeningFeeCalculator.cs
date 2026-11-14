using LoanSystem.Common.Core.Application;
using LoanSystem.Common.Core.Domain;

namespace LoanSystem.Modules.Products.Core.Domain.Engine;

/// <summary>Opening fee configuration of a product: percentage of the requested amount plus a fixed amount.</summary>
/// <param name="RateFraction">0.02 = 2 % of the requested amount.</param>
internal sealed record OpeningFeeTerms(decimal RateFraction, Money Fixed, CommissionMode Mode)
{
    public static OpeningFeeTerms None(string currency = Money.DefaultCurrency) =>
        new(0m, Money.Zero(currency), CommissionMode.DEDUCTIVA);
}

/// <param name="FinancedPrincipal">What enters the amortization engine.</param>
/// <param name="NetDisbursement">What the customer actually receives.</param>
internal sealed record OpeningFeeBreakdown(
    Money RequestedAmount,
    Money Fee,
    CommissionMode Mode,
    Money FinancedPrincipal,
    Money NetDisbursement);

/// <summary>
/// Opening fee (BE-PRO-03): fee = round(requested · rate) + fixed.
/// <list type="bullet">
/// <item>DEDUCTIVA: financed = requested; net disbursement = requested − fee (must stay &gt; 0).</item>
/// <item>FINANCIADA: financed = requested + fee; net disbursement = requested.</item>
/// </list>
/// The fee never accrues interest by itself: in FINANCIADA it does only because it is part of the principal.
/// </summary>
internal static class OpeningFeeCalculator
{
    /// <summary>Default upper bound of the percentage (Products:Rules:MaxOpeningFeeRatePercentage = 20).</summary>
    public const decimal DefaultMaxRateFraction = 0.20m;

    /// <summary>PRO-03.1</summary>
    public static Money Calculate(Money requested, decimal feeRateFraction, Money feeFixed) =>
        requested * feeRateFraction + feeFixed;

    /// <summary>PRO-03.2: fee and its effect on the financed principal and the net disbursement.</summary>
    public static Result<OpeningFeeBreakdown> Apply(Money requested, OpeningFeeTerms terms)
    {
        ArgumentNullException.ThrowIfNull(terms);

        var invalid = ValidateTerms(terms, requested.Currency, DefaultMaxRateFraction);
        if (invalid is not null)
            return invalid;

        var fee = Calculate(requested, terms.RateFraction, terms.Fixed);

        if (terms.Mode == CommissionMode.FINANCIADA)
            return new OpeningFeeBreakdown(requested, fee, terms.Mode, requested + fee, requested);

        var net = requested - fee;
        if (net.Amount <= 0)
            return FeeExceedsAmount(fee, requested);

        return new OpeningFeeBreakdown(requested, fee, terms.Mode, requested, net);
    }

    /// <summary>
    /// PRO-03.4 product-level rules: rate between 0 and <paramref name="maxRateFraction"/>, fixed ≥ 0 and,
    /// in DEDUCTIVA, the fee on the product's minimum amount must be smaller than that amount.
    /// </summary>
    public static Error? ValidateProductTerms(OpeningFeeTerms terms, Money minAmount, decimal maxRateFraction = DefaultMaxRateFraction)
    {
        ArgumentNullException.ThrowIfNull(terms);

        var invalid = ValidateTerms(terms, minAmount.Currency, maxRateFraction);
        if (invalid is not null)
            return invalid;

        if (terms.Mode == CommissionMode.DEDUCTIVA)
        {
            var feeOnMin = Calculate(minAmount, terms.RateFraction, terms.Fixed);
            if (feeOnMin >= minAmount)
                return FeeExceedsAmount(feeOnMin, minAmount);
        }

        return null;
    }

    private static Error? ValidateTerms(OpeningFeeTerms terms, string currency, decimal maxRateFraction)
    {
        if (terms.RateFraction < 0 || terms.RateFraction > maxRateFraction)
            return Error.Validation(
                "Products.InvalidOpeningFee",
                $"Opening fee percentage must be between 0 and {maxRateFraction * 100m:0.##} %.");

        if (terms.Fixed.Amount < 0)
            return Error.Validation("Products.InvalidOpeningFee", "Opening fee fixed amount cannot be negative.");

        if (terms.Fixed.Currency != currency)
            return Error.Validation("Products.InvalidOpeningFee", "Opening fee currency must match the loan currency.");

        if (!Enum.IsDefined(terms.Mode))
            return Error.Validation("Products.InvalidOpeningFee", $"Opening fee mode must be one of: {CommissionModeParser.Supported}.");

        return null;
    }

    private static Error FeeExceedsAmount(Money fee, Money amount) =>
        Error.Unprocessable(
                "Products.OpeningFeeExceedsAmount",
                $"A deductive opening fee of {fee.Amount} leaves nothing to disburse from {amount.Amount}.")
            .WithExtension("fee", fee.Amount)
            .WithExtension("amount", amount.Amount);
}
