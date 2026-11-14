namespace LoanSystem.Modules.Products.Core.Domain;

/// <summary>Amortization method. Member names match the PostgreSQL <c>amortization_method</c> enum labels exactly.</summary>
internal enum AmortizationMethod
{
    FRANCES = 1,
    FLAT = 2,
    ALEMAN = 3,
    AMERICANO = 4
}

/// <summary>Payment frequency. Member names match the PostgreSQL <c>payment_frequency</c> enum labels exactly.</summary>
internal enum PaymentFrequency
{
    DIARIA = 1,
    SEMANAL = 2,
    QUINCENAL = 3,
    MENSUAL = 4
}

/// <summary>Where a due date moves when the theoretical one is not a business day (BE-PRO-06).</summary>
internal enum HolidayShift
{
    NEXT = 1,
    PREV = 2
}

/// <summary>
/// How the engine counts the days of each period (plan D3).
/// <list type="bullet">
/// <item><c>NOMINAL_PERIODS</c>: no disbursement date; every period is 1/7/15/30 days by frequency (quick simulator).</item>
/// <item><c>ACTUAL_360</c>: with a disbursement date; real days between theoretical due dates over a 360-day year.</item>
/// </list>
/// </summary>
internal enum DayCountMode
{
    NOMINAL_PERIODS = 1,
    ACTUAL_360 = 2
}

internal static class AmortizationMethodParser
{
    public const string Supported = "FRANCES (alias FRENCH), FLAT, ALEMAN, AMERICANO";

    public static bool TryParse(string? value, out AmortizationMethod method)
    {
        switch (value?.Trim().ToUpperInvariant())
        {
            case "FRANCES":
            case "FRENCH": // legacy alias kept for the existing simulator clients
                method = AmortizationMethod.FRANCES;
                return true;
            case "FLAT":
                method = AmortizationMethod.FLAT;
                return true;
            case "ALEMAN":
                method = AmortizationMethod.ALEMAN;
                return true;
            case "AMERICANO":
                method = AmortizationMethod.AMERICANO;
                return true;
            default:
                method = default;
                return false;
        }
    }
}

internal static class PaymentFrequencyParser
{
    public const string Supported = "DIARIA, SEMANAL, QUINCENAL, MENSUAL";

    public static bool TryParse(string? value, out PaymentFrequency frequency)
    {
        switch (value?.Trim().ToUpperInvariant())
        {
            case "DIARIA":
                frequency = PaymentFrequency.DIARIA;
                return true;
            case "SEMANAL":
                frequency = PaymentFrequency.SEMANAL;
                return true;
            case "QUINCENAL":
                frequency = PaymentFrequency.QUINCENAL;
                return true;
            case "MENSUAL":
                frequency = PaymentFrequency.MENSUAL;
                return true;
            default:
                frequency = default;
                return false;
        }
    }

    /// <summary>Nominal days per period used in <see cref="DayCountMode.NOMINAL_PERIODS"/> (PRO-02.2).</summary>
    public static int NominalDays(this PaymentFrequency frequency) => frequency switch
    {
        PaymentFrequency.DIARIA => 1,
        PaymentFrequency.SEMANAL => 7,
        PaymentFrequency.QUINCENAL => 15,
        PaymentFrequency.MENSUAL => 30,
        _ => throw new ArgumentOutOfRangeException(nameof(frequency), frequency, "Unsupported payment frequency.")
    };
}
