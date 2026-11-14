using LoanSystem.Modules.Customers.Core.Domain;

namespace LoanSystem.Modules.Customers.Core.Ports.Outbound;

internal interface IDocumentTypeCatalog
{
    Task<DocumentTypeRule?> GetRuleAsync(DocumentType type, CancellationToken ct = default);
}

internal interface IBanksCatalog
{
    bool TryGetBankName(string bankCode, out string bankName);
    IReadOnlyDictionary<string, string> GetAll();
}
