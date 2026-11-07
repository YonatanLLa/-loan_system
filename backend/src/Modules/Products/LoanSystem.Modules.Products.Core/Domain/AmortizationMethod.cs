namespace LoanSystem.Modules.Products.Core.Domain;

internal enum AmortizationMethod
{
    French = 1
}

internal static class AmortizationMethodParser
{
    public static bool TryParse(string? value, out AmortizationMethod method)
    {
        switch (value?.Trim().ToUpperInvariant())
        {
            case "FRENCH":
                method = AmortizationMethod.French;
                return true;
            default:
                method = default;
                return false;
        }
    }
}
