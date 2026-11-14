using LoanSystem.Common.Core.Contracts;
using LoanSystem.Modules.Customers.Core.Domain;
using LoanSystem.Modules.Customers.Core.Ports.Outbound;
using Microsoft.EntityFrameworkCore;

namespace LoanSystem.Modules.Customers.Adapters.Outbound.Persistence;

internal sealed class CustomerRepository : ICustomerRepository
{
    private readonly CustomersDbContext _db;

    public CustomerRepository(CustomersDbContext db) => _db = db;

    public void Add(Customer customer) => _db.Customers.Add(customer);

    public Task<Customer?> FindByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.Customers.FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<bool> ExistsByDocumentAsync(DocumentType type, string normalizedNumber, CancellationToken ct = default) =>
        _db.Customers.AsNoTracking()
            .AnyAsync(c => c.Document.Type == type && c.Document.Number == normalizedNumber, ct);

    public Task<Guid?> FindIdByPortalUserIdAsync(Guid portalUserId, CancellationToken ct = default) =>
        _db.Customers.AsNoTracking()
            .Where(c => c.PortalUserId == portalUserId)
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync(ct);

    public async Task<PagedResult<Customer>> SearchAsync(CustomerSearchFilter filter, CancellationToken ct = default)
    {
        var query = _db.Customers.AsNoTracking().AsQueryable();

        if (filter.KycStatus.HasValue)
        {
            query = query.Where(c => c.KycStatus == filter.KycStatus.Value);
        }

        if (filter.PersonType.HasValue)
        {
            query = query.Where(c => c.PersonType == filter.PersonType.Value);
        }

        if (filter.CreatedFromUtc.HasValue)
        {
            query = query.Where(c => c.CreatedAt >= filter.CreatedFromUtc.Value);
        }

        if (filter.CreatedToUtc.HasValue)
        {
            query = query.Where(c => c.CreatedAt < filter.CreatedToUtc.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Query))
        {
            var term = filter.Query.Trim();
            var normalizedDoc = IdentityDocument.Normalize(term);

            if (_db.Database.IsNpgsql())
            {
                var pattern = $"%{EscapeLike(term)}%";
                query = query.Where(c =>
                    c.Document.Number == normalizedDoc ||
                    EF.Functions.ILike(CustomersDbContext.Unaccent(c.FullName), CustomersDbContext.Unaccent(pattern)) ||
                    EF.Functions.ILike(c.Email, pattern) ||
                    c.PhonePrimary.Contains(term));
            }
            else
            {
                query = query.Where(c =>
                    c.Document.Number == normalizedDoc ||
                    c.FullName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    c.Email.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    c.PhonePrimary.Contains(term, StringComparison.OrdinalIgnoreCase));
            }
        }

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(ct);

        return new PagedResult<Customer>(items, filter.Page, filter.PageSize, totalCount);
    }

    private static string EscapeLike(string input) =>
        input.Replace("\\", "\\\\", StringComparison.Ordinal)
             .Replace("%", "\\%", StringComparison.Ordinal)
             .Replace("_", "\\_", StringComparison.Ordinal);
}
