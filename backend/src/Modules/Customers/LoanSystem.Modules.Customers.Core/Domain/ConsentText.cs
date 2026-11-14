using LoanSystem.Common.Core.Domain;

namespace LoanSystem.Modules.Customers.Core.Domain;

internal sealed class ConsentText : Entity<Guid>
{
    public ConsentPurpose Purpose { get; private set; }
    public string Version { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public string Sha256 { get; private set; } = string.Empty;
    public bool IsCurrent { get; private set; }
    public DateTimeOffset PublishedAt { get; private set; }

    private ConsentText() { }

    public static ConsentText Create(
        ConsentPurpose purpose,
        string version,
        string body,
        string sha256,
        bool isCurrent,
        DateTimeOffset publishedAt) =>
        new()
        {
            Id = Guid.NewGuid(),
            Purpose = purpose,
            Version = version,
            Body = body,
            Sha256 = sha256,
            IsCurrent = isCurrent,
            PublishedAt = publishedAt
        };
}
