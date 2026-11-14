namespace LoanSystem.Modules.Customers.Core.Contracts.Consents
{
    public sealed record ConsentAcceptance(string Purpose, string Version);
}

namespace LoanSystem.Modules.Customers.Core.Contracts
{
    public sealed record CurrentConsentTextResponse(
        string Purpose,
        string Version,
        string Body,
        string Sha256,
        DateTimeOffset PublishedAt);

    public sealed record GrantConsentRequest(string Purpose, string Version);

    public sealed record GrantConsentCommand(Guid CustomerId, string Purpose, string Version);

    public sealed record RevokeConsentCommand(Guid CustomerId, string Purpose);

    public sealed record CustomerConsentResponse(
        Guid Id,
        string Purpose,
        string Version,
        DateTimeOffset GrantedAt,
        string Channel,
        DateTimeOffset? RevokedAt);
}
