using LoanSystem.Modules.Customers.Core.Domain;

namespace LoanSystem.Modules.Customers.Core.Ports.Outbound;

internal sealed record StoredObject(string StorageUri, string Sha256, long SizeBytes);

internal sealed record LocalSignedFile(Stream Stream, string ContentType, string FileName);

internal interface IDocumentStorage
{
    Task<StoredObject> PutAsync(string key, Stream content, string contentType, CancellationToken ct = default);
    Task<Uri> GetSignedReadUrlAsync(string key, TimeSpan ttl, CancellationToken ct = default);
    Task<bool> ExistsAsync(string key, CancellationToken ct = default);
}

internal interface ILocalSignedFileReader
{
    Task<LocalSignedFile?> OpenSignedReadAsync(string token, CancellationToken ct = default);
}

internal interface ICustomerDocumentRepository
{
    void Add(CustomerDocument document);
    Task<CustomerDocument?> FindByIdAsync(Guid customerId, Guid documentId, CancellationToken ct = default);
    Task<IReadOnlyList<CustomerDocument>> ListByCustomerAsync(Guid customerId, bool includeHistory, CancellationToken ct = default);
    Task<IReadOnlyList<CustomerDocument>> GetCurrentByCategoryAsync(Guid customerId, DocumentCategory category, CancellationToken ct = default);
    Task<IReadOnlySet<DocumentCategory>> GetCurrentCategoriesAsync(Guid customerId, CancellationToken ct = default);
}
