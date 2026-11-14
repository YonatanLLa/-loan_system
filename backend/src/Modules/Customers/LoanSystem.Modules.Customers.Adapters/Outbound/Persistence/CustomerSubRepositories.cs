using System.Net;
using LoanSystem.Common.Adapters.Persistence;
using LoanSystem.Common.Core.Ports.Outbound;
using LoanSystem.Modules.Customers.Core.Domain;
using LoanSystem.Modules.Customers.Core.Ports.Outbound;
using Microsoft.EntityFrameworkCore;

namespace LoanSystem.Modules.Customers.Adapters.Outbound.Persistence;

internal sealed class CustomerDocumentRepository : ICustomerDocumentRepository
{
    private readonly CustomersDbContext _db;

    public CustomerDocumentRepository(CustomersDbContext db) => _db = db;

    public void Add(CustomerDocument document) => _db.CustomerDocuments.Add(document);

    public Task<CustomerDocument?> FindByIdAsync(Guid customerId, Guid documentId, CancellationToken ct = default) =>
        _db.CustomerDocuments.FirstOrDefaultAsync(d => d.CustomerId == customerId && d.Id == documentId, ct);

    public async Task<IReadOnlyList<CustomerDocument>> ListByCustomerAsync(
        Guid customerId,
        bool includeHistory,
        CancellationToken ct = default)
    {
        var query = _db.CustomerDocuments.AsNoTracking().Where(d => d.CustomerId == customerId);
        if (!includeHistory)
        {
            query = query.Where(d => d.IsCurrent);
        }

        return await query
            .OrderByDescending(d => d.UploadedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CustomerDocument>> GetCurrentByCategoryAsync(
        Guid customerId,
        DocumentCategory category,
        CancellationToken ct = default) =>
        await _db.CustomerDocuments
            .Where(d => d.CustomerId == customerId && d.DocCategory == category && d.IsCurrent)
            .ToListAsync(ct);

    public async Task<IReadOnlySet<DocumentCategory>> GetCurrentCategoriesAsync(Guid customerId, CancellationToken ct = default)
    {
        var categories = await _db.CustomerDocuments.AsNoTracking()
            .Where(d => d.CustomerId == customerId && d.IsCurrent)
            .Select(d => d.DocCategory)
            .Distinct()
            .ToListAsync(ct);

        return categories.ToHashSet();
    }
}

internal sealed class KycVerificationRepository : IKycVerificationRepository
{
    private readonly CustomersDbContext _db;

    public KycVerificationRepository(CustomersDbContext db) => _db = db;

    public void Add(KycVerification verification) => _db.KycVerifications.Add(verification);

    public async Task<IReadOnlyList<KycVerification>> GetByCustomerAsync(Guid customerId, CancellationToken ct = default) =>
        await _db.KycVerifications
            .Where(v => v.CustomerId == customerId)
            .OrderByDescending(v => v.RequestedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<KycVerification>> GetLatestByCustomerAsync(Guid customerId, CancellationToken ct = default)
    {
        var all = await _db.KycVerifications.AsNoTracking()
            .Where(v => v.CustomerId == customerId)
            .OrderByDescending(v => v.RequestedAt)
            .ToListAsync(ct);

        return all
            .GroupBy(v => v.CheckType)
            .Select(g => g.First())
            .OrderBy(v => v.CheckType)
            .ToList();
    }
}

internal sealed class ConsentTextRepository : IConsentTextRepository
{
    private readonly CustomersDbContext _db;

    public ConsentTextRepository(CustomersDbContext db) => _db = db;

    public async Task<IReadOnlyList<ConsentText>> GetCurrentAllAsync(CancellationToken ct = default) =>
        await _db.ConsentTexts.AsNoTracking()
            .Where(t => t.IsCurrent)
            .OrderBy(t => t.Purpose)
            .ToListAsync(ct);

    public Task<ConsentText?> FindCurrentByPurposeAsync(ConsentPurpose purpose, CancellationToken ct = default) =>
        _db.ConsentTexts.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Purpose == purpose && t.IsCurrent, ct);
}

internal sealed class CustomerConsentRepository : ICustomerConsentRepository
{
    private readonly CustomersDbContext _db;

    public CustomerConsentRepository(CustomersDbContext db) => _db = db;

    public void Add(CustomerConsent consent) => _db.CustomerConsents.Add(consent);

    public async Task<IReadOnlyList<CustomerConsent>> ListByCustomerAsync(Guid customerId, CancellationToken ct = default) =>
        await _db.CustomerConsents
            .Include(c => c.ConsentText)
            .AsNoTracking()
            .Where(c => c.CustomerId == customerId)
            .OrderByDescending(c => c.GrantedAt)
            .ToListAsync(ct);

    public Task<CustomerConsent?> FindActiveAsync(Guid customerId, ConsentPurpose purpose, CancellationToken ct = default) =>
        _db.CustomerConsents
            .Include(c => c.ConsentText)
            .FirstOrDefaultAsync(c => c.CustomerId == customerId && c.Purpose == purpose && c.RevokedAt == null, ct);
}

internal sealed class OtpRepository : IOtpRepository
{
    private readonly CustomersDbContext _db;

    public OtpRepository(CustomersDbContext db) => _db = db;

    public void Add(OtpCode otp) => _db.OtpVerifications.Add(otp);

    public Task<OtpCode?> FindByIdAsync(Guid otpRequestId, CancellationToken ct = default) =>
        _db.OtpVerifications.FirstOrDefaultAsync(o => o.Id == otpRequestId, ct);

    public async Task<IReadOnlyList<OtpCode>> FindActiveByCustomerAndPurposeAsync(
        Guid customerId,
        OtpPurpose purpose,
        CancellationToken ct = default) =>
        await _db.OtpVerifications
            .Where(o => o.CustomerId == customerId
                     && o.Purpose == purpose
                     && o.ConsumedAt == null
                     && o.InvalidatedAt == null)
            .ToListAsync(ct);

    public Task<OtpCode?> GetLatestByCustomerAndPurposeAsync(
        Guid customerId,
        OtpPurpose purpose,
        CancellationToken ct = default) =>
        _db.OtpVerifications.AsNoTracking()
            .Where(o => o.CustomerId == customerId && o.Purpose == purpose)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync(ct);

    public Task<int> CountIssuedSinceAsync(
        Guid customerId,
        OtpPurpose purpose,
        DateTimeOffset sinceUtc,
        CancellationToken ct = default) =>
        _db.OtpVerifications.AsNoTracking()
            .CountAsync(o => o.CustomerId == customerId && o.Purpose == purpose && o.CreatedAt >= sinceUtc, ct);
}

internal sealed class CustomerBankAccountRepository : ICustomerBankAccountRepository
{
    private readonly CustomersDbContext _db;

    public CustomerBankAccountRepository(CustomersDbContext db) => _db = db;

    public void Add(CustomerBankAccount account) => _db.BankAccounts.Add(account);

    public Task<CustomerBankAccount?> FindByIdAsync(Guid customerId, Guid accountId, CancellationToken ct = default) =>
        _db.BankAccounts.FirstOrDefaultAsync(a => a.CustomerId == customerId && a.Id == accountId, ct);

    public async Task<IReadOnlyList<CustomerBankAccount>> ListByCustomerAsync(Guid customerId, CancellationToken ct = default) =>
        await _db.BankAccounts.AsNoTracking()
            .Where(a => a.CustomerId == customerId)
            .OrderByDescending(a => a.IsPrimary)
            .ThenByDescending(a => a.CreatedAt)
            .ToListAsync(ct);

    public Task<CustomerBankAccount?> FindPrimaryActiveAsync(Guid customerId, CancellationToken ct = default) =>
        _db.BankAccounts
            .FirstOrDefaultAsync(a => a.CustomerId == customerId && a.IsPrimary && a.Status == BankAccountStatus.ACTIVE, ct);

    public Task<bool> ExistsActiveByHashAsync(Guid customerId, string accountNumberHash, CancellationToken ct = default) =>
        _db.BankAccounts.AsNoTracking()
            .AnyAsync(a => a.CustomerId == customerId
                        && a.AccountNumberHash == accountNumberHash
                        && a.Status != BankAccountStatus.INACTIVE, ct);

    public Task<int> CountNonInactiveAsync(Guid customerId, CancellationToken ct = default) =>
        _db.BankAccounts.AsNoTracking()
            .CountAsync(a => a.CustomerId == customerId && a.Status != BankAccountStatus.INACTIVE, ct);
}

internal sealed class BankAccountExistenceLookup : IBankAccountExistenceLookup
{
    private readonly CustomersDbContext _db;

    public BankAccountExistenceLookup(CustomersDbContext db) => _db = db;

    public async Task<(bool Exists, CustomerBankAccount? Account)> FindGlobalByIdAsync(Guid accountId, CancellationToken ct = default)
    {
        var account = await _db.BankAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == accountId, ct);
        return (account is not null, account);
    }
}

internal sealed class CustomerAuditWriter : ICustomerAuditWriter
{
    private readonly CustomersDbContext _db;
    private readonly IClock _clock;

    public CustomerAuditWriter(CustomersDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task WriteAsync(
        string entityName,
        string entityId,
        string action,
        Guid? userId,
        IPAddress? userIp,
        string? userAgent,
        string? metadataJson = null,
        CancellationToken ct = default)
    {
        var normalizedIp = userIp is { IsIPv4MappedToIPv6: true } ? userIp.MapToIPv4() : userIp;
        var truncatedUa = string.IsNullOrWhiteSpace(userAgent)
            ? null
            : userAgent.Length > 512 ? userAgent[..512] : userAgent;

        _db.AuditLogs.Add(new AuditLogRecord
        {
            EntityName = entityName,
            EntityId = entityId,
            Action = action,
            StateAfter = metadataJson,
            UserId = userId,
            UserIp = normalizedIp,
            UserAgent = truncatedUa,
            CreatedAt = _clock.UtcNow.ToUniversalTime()
        });

        await _db.SaveChangesAsync(ct);
    }
}
