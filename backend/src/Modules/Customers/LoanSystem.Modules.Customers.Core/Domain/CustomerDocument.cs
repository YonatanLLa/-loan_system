using LoanSystem.Common.Core.Domain;
using LoanSystem.Common.Core.Ports.Outbound;

namespace LoanSystem.Modules.Customers.Core.Domain;

internal sealed class CustomerDocument : Entity<Guid>
{
    public Guid CustomerId { get; private set; }
    public DocumentCategory DocCategory { get; private set; }
    public string FileHash { get; private set; } = string.Empty;
    public string StorageUri { get; private set; } = string.Empty;
    public string MimeType { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }
    public string? OriginalName { get; private set; }
    public bool IsCurrent { get; private set; }
    public Guid? UploadedBy { get; private set; }
    public DateTimeOffset UploadedAt { get; private set; }
    public DateTimeOffset? ReplacedAt { get; private set; }

    private CustomerDocument() { }

    public static CustomerDocument Create(
        Guid id,
        Guid customerId,
        DocumentCategory category,
        string fileHash,
        string storageUri,
        string mimeType,
        long sizeBytes,
        string? originalName,
        Guid? uploadedBy,
        IClock clock) =>
        new()
        {
            Id = id,
            CustomerId = customerId,
            DocCategory = category,
            FileHash = fileHash,
            StorageUri = storageUri,
            MimeType = mimeType,
            SizeBytes = sizeBytes,
            OriginalName = SanitizeOriginalName(originalName),
            IsCurrent = true,
            UploadedBy = uploadedBy == Guid.Empty ? null : uploadedBy,
            UploadedAt = clock.UtcNow,
            ReplacedAt = null
        };

    public static bool AllowsMultipleCurrent(DocumentCategory category) =>
        category is DocumentCategory.OTRO or DocumentCategory.BOLETA_PAGO;

    public void MarkReplaced(IClock clock)
    {
        if (!IsCurrent)
        {
            return;
        }

        IsCurrent = false;
        ReplacedAt = clock.UtcNow;
    }

    private static string? SanitizeOriginalName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var fileOnly = Path.GetFileName(name.Trim());
        return fileOnly.Length > 255 ? fileOnly[..255] : fileOnly;
    }
}
