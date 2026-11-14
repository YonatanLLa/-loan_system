namespace LoanSystem.Modules.Customers.Core.Domain;

/// <summary>Rule metadata loaded from the <c>document_types</c> catalog table (RF-04.1).</summary>
internal sealed record DocumentTypeRule(
    DocumentType Code,
    string Name,
    int? CharLength,
    string? ValidationRegex,
    bool IsActive);
