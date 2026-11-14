using System.Text.RegularExpressions;
using LoanSystem.Common.Core.Application;
using LoanSystem.Common.Core.Domain;

namespace LoanSystem.Modules.Customers.Core.Domain;

/// <summary>
/// Normalized identity document validated against both catalog rules (<c>document_types</c>)
/// and fixed Peruvian format/check-digit invariants (BE-CUS-01, CUS-01.1).
/// </summary>
internal sealed partial class IdentityDocument : ValueObject
{
    private static readonly int[] RucWeights = [5, 4, 3, 2, 7, 6, 5, 4, 3, 2];

    public DocumentType Type { get; private set; }
    public string Number { get; private set; } = string.Empty;

    private IdentityDocument() { }

    private IdentityDocument(DocumentType type, string number)
    {
        Type = type;
        Number = number;
    }

    public static string Normalize(string? rawNumber)
    {
        if (string.IsNullOrWhiteSpace(rawNumber))
        {
            return string.Empty;
        }

        return WhitespaceOrHyphenRegex().Replace(rawNumber.Trim().ToUpperInvariant(), string.Empty);
    }

    public static Result<IdentityDocument> Create(DocumentType type, string? rawNumber, DocumentTypeRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);

        if (!rule.IsActive || rule.Code != type)
        {
            return CustomerErrors.DocumentTypeInactive(type.ToString());
        }

        var normalized = Normalize(rawNumber);
        if (string.IsNullOrEmpty(normalized))
        {
            return CustomerErrors.InvalidDocumentNumber("Document number is required.");
        }

        if (rule.CharLength.HasValue && normalized.Length != rule.CharLength.Value)
        {
            return CustomerErrors.InvalidDocumentNumber(
                $"Document '{type}' must have exact length {rule.CharLength.Value}.");
        }

        if (!string.IsNullOrWhiteSpace(rule.ValidationRegex))
        {
            var matchesCatalog = Regex.IsMatch(
                normalized,
                rule.ValidationRegex,
                RegexOptions.CultureInvariant,
                TimeSpan.FromTicks(TimeSpan.TicksPerSecond));

            if (!matchesCatalog)
            {
                return CustomerErrors.InvalidDocumentNumber(
                    $"Document number does not match configured pattern for '{type}'.");
            }
        }

        var fixedValidation = type switch
        {
            DocumentType.DNI => ValidateDni(normalized),
            DocumentType.CE => ValidateCe(normalized),
            DocumentType.RUC => ValidateRuc(normalized),
            DocumentType.PASAPORTE => ValidatePassport(normalized),
            _ => CustomerErrors.InvalidDocumentNumber($"Unsupported document type '{type}'.")
        };

        if (fixedValidation.IsFailure)
        {
            return fixedValidation.Error;
        }

        return new IdentityDocument(type, normalized);
    }

    private static Result ValidateDni(string normalized) =>
        DniRegex().IsMatch(normalized)
            ? Result.Success()
            : CustomerErrors.InvalidDocumentNumber("DNI must consist of exactly 8 digits.");

    private static Result ValidateCe(string normalized) =>
        CeRegex().IsMatch(normalized)
            ? Result.Success()
            : CustomerErrors.InvalidDocumentNumber("CE must consist of 9 digits.");

    private static Result ValidatePassport(string normalized) =>
        PassportRegex().IsMatch(normalized)
            ? Result.Success()
            : CustomerErrors.InvalidDocumentNumber("PASAPORTE must be 6 to 12 alphanumeric characters.");

    private static Result ValidateRuc(string normalized)
    {
        if (!RucFormatRegex().IsMatch(normalized))
        {
            return CustomerErrors.InvalidDocumentNumber(
                "RUC must have 11 digits starting with 10, 15, 16, 17, or 20.");
        }

        var sum = 0;
        for (var i = 0; i < 10; i++)
        {
            sum += (normalized[i] - '0') * RucWeights[i];
        }

        var remainder = 11 - (sum % 11);
        var expectedDigit = remainder switch
        {
            10 => 0,
            11 => 1,
            _ => remainder
        };

        var actualDigit = normalized[10] - '0';
        if (actualDigit != expectedDigit)
        {
            return CustomerErrors.InvalidDocumentNumber("RUC check digit is invalid.");
        }

        return Result.Success();
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Type;
        yield return Number;
    }

    [GeneratedRegex(@"[\s-]+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceOrHyphenRegex();

    [GeneratedRegex(@"^\d{8}$", RegexOptions.CultureInvariant)]
    private static partial Regex DniRegex();

    [GeneratedRegex(@"^\d{9}$", RegexOptions.CultureInvariant)]
    private static partial Regex CeRegex();

    [GeneratedRegex(@"^(10|15|16|17|20)\d{9}$", RegexOptions.CultureInvariant)]
    private static partial Regex RucFormatRegex();

    [GeneratedRegex(@"^[A-Z0-9]{6,12}$", RegexOptions.CultureInvariant)]
    private static partial Regex PassportRegex();
}
