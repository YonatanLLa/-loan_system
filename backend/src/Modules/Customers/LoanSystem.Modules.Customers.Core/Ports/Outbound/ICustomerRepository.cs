using LoanSystem.Common.Core.Contracts;
using LoanSystem.Modules.Customers.Core.Contracts;
using LoanSystem.Modules.Customers.Core.Domain;

namespace LoanSystem.Modules.Customers.Core.Ports.Outbound;

internal interface ICustomerRepository
{
    void Add(Customer customer);
    Task<Customer?> FindByIdAsync(Guid id, CancellationToken ct = default);
    Task<bool> ExistsByDocumentAsync(DocumentType type, string normalizedNumber, CancellationToken ct = default);
    Task<Guid?> FindIdByPortalUserIdAsync(Guid portalUserId, CancellationToken ct = default);
    Task<PagedResult<Customer>> SearchAsync(CustomerSearchFilter filter, CancellationToken ct = default);
}

internal sealed record CustomerSearchFilter(
    string? Query,
    KycStatus? KycStatus,
    PersonType? PersonType,
    DateTimeOffset? CreatedFromUtc,
    DateTimeOffset? CreatedToUtc,
    int Page,
    int PageSize);
