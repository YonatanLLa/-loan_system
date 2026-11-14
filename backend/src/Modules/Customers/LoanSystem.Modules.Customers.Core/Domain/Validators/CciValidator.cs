using System.Text.RegularExpressions;
using LoanSystem.Common.Core.Application;
using LoanSystem.Modules.Customers.Core.Ports.Outbound;

namespace LoanSystem.Modules.Customers.Core.Domain.Validators;

/// <summary>
/// Validates Peruvian 20-digit CCI numbers and resolves the 3-digit bank code against <see cref="IBanksCatalog"/>.
/// Isolated behind <see cref="ICciValidator"/> so CCE check-digit verification can be plugged in later (Plan §14 R1).
/// </summary>
internal interface ICciValidator
{
    Result<ValidatedCci> Validate(string? rawCci);
}

internal sealed record ValidatedCci(string NormalizedCci, string BankCode, string BankName, string Last4);

internal sealed partial class CciValidator : ICciValidator
{
    private readonly IBanksCatalog _banks;

    public CciValidator(IBanksCatalog banks)
    {
        _banks = banks ?? throw new ArgumentNullException(nameof(banks));
    }

    public Result<ValidatedCci> Validate(string? rawCci)
    {
        if (string.IsNullOrWhiteSpace(rawCci))
        {
            return CustomerErrors.InvalidCci("CCI is required for savings or checking accounts.");
        }

        var normalized = WhitespaceOrHyphenRegex().Replace(rawCci.Trim(), string.Empty);
        if (!TwentyDigitsRegex().IsMatch(normalized))
        {
            return CustomerErrors.InvalidCci("CCI must consist of exactly 20 numeric digits.");
        }

        var bankCode = normalized[..3];
        if (!_banks.TryGetBankName(bankCode, out var bankName))
        {
            return CustomerErrors.InvalidCci($"Bank code '{bankCode}' is not recognized in the banks catalog.");
        }

        var last4 = normalized[^4..];
        return new ValidatedCci(normalized, bankCode, bankName, last4);
    }

    [GeneratedRegex(@"[\s-]+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceOrHyphenRegex();

    [GeneratedRegex(@"^\d{20}$", RegexOptions.CultureInvariant)]
    private static partial Regex TwentyDigitsRegex();
}
