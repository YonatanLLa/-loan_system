using System.Net;
using System.Security.Cryptography;
using System.Text;
using LoanSystem.Common.Core.Application;
using LoanSystem.Common.Core.Contracts;
using LoanSystem.Common.Core.Ports.Outbound;
using LoanSystem.Modules.Customers.Core.Contracts;
using LoanSystem.Modules.Customers.Core.Domain;
using LoanSystem.Modules.Customers.Core.Ports.Outbound;

namespace LoanSystem.Application.UnitTests.Customers;

internal sealed class FakeCustomersClock(DateTimeOffset? initial = null) : IClock
{
    public DateTimeOffset UtcNow { get; set; } = initial ?? new DateTimeOffset(2026, 11, 14, 15, 0, 0, TimeSpan.Zero);
    public DateTime LimaNow => UtcNow.UtcDateTime.AddHours(-5);
    public DateOnly LimaToday => DateOnly.FromDateTime(LimaNow);
    public void Advance(TimeSpan delta) => UtcNow = UtcNow.Add(delta);
}

internal sealed class FakeCustomersCurrentUser : ICurrentUser
{
    public Guid? UserId { get; set; } = Guid.NewGuid();
    public string? Email { get; set; } = "staff@loan.test";
    public IReadOnlyCollection<string> Roles { get; set; } = [SecurityPolicies.Operator];
    public Guid? CustomerId { get; set; }
    public bool IsAuthenticated { get; set; } = true;
    public bool IsInRole(string role) => Roles.Contains(role, StringComparer.OrdinalIgnoreCase);

    public FakeCustomersCurrentUser AsRole(string role, Guid? customerId = null)
    {
        Roles = [role];
        CustomerId = customerId;
        UserId = Guid.NewGuid();
        IsAuthenticated = true;
        return this;
    }
}

internal sealed class FakeCustomersClientInfo : IClientInfoProvider
{
    public IPAddress? IpAddress { get; set; } = IPAddress.Parse("190.12.34.56");
    public string? UserAgent { get; set; } = "CustomersUnitTests/1.0";
}

internal sealed class FakeCustomersAuditContext : IAuditContext
{
    private readonly Dictionary<string, string> _entityActions = new(StringComparer.OrdinalIgnoreCase);
    public string? Action { get; private set; }
    public Guid? ActorId { get; private set; }
    public Guid? ApprovalRequestId { get; private set; }

    public void SetAction(string action) => Action = action;
    public void SetAction(string action, string entityName) => _entityActions[entityName] = action;
    public string? GetActionForEntity(string entityName) =>
        _entityActions.TryGetValue(entityName, out var act) ? act : null;
    public void SetActor(Guid userId) => ActorId = userId;
    public void SetApproval(Guid approvalRequestId) => ApprovalRequestId = approvalRequestId;
}

internal sealed class FakeCustomersUnitOfWork : ICustomersUnitOfWork
{
    public int SaveChangesCalls { get; private set; }
    public int CommittedTransactions { get; private set; }
    public bool FailNextSaveWithConcurrency { get; set; }
    public bool FailNextSaveWithDuplicate { get; set; }
    public Exception? FailNextSaveException { get; set; }

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        SaveChangesCalls++;
        if (FailNextSaveException is { } ex)
        {
            FailNextSaveException = null;
            throw ex;
        }
        if (FailNextSaveWithDuplicate)
        {
            FailNextSaveWithDuplicate = false;
            throw new DuplicateRecordException("ux_test", new InvalidOperationException("duplicate"));
        }
        if (FailNextSaveWithConcurrency)
        {
            FailNextSaveWithConcurrency = false;
            throw new ConcurrencyConflictException();
        }
        return Task.FromResult(1);
    }

    public Task<ITransactionScope> BeginTransactionAsync(CancellationToken ct = default) =>
        Task.FromResult<ITransactionScope>(new FakeTx(this));

    private sealed class FakeTx(FakeCustomersUnitOfWork owner) : ITransactionScope
    {
        public Task CommitAsync(CancellationToken ct = default)
        {
            owner.CommittedTransactions++;
            return Task.CompletedTask;
        }
        public Task RollbackAsync(CancellationToken ct = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

internal sealed class FakeCustomersOutbox : ICustomersOutbox
{
    public List<object> Events { get; } = [];
    public void Enqueue<TEvent>(TEvent @event) where TEvent : class => Events.Add(@event);
}

internal sealed class InMemoryDocumentTypeCatalog : IDocumentTypeCatalog
{
    private readonly Dictionary<DocumentType, DocumentTypeRule> _rules = new()
    {
        [DocumentType.DNI] = new DocumentTypeRule(DocumentType.DNI, "DNI", 8, @"^\d{8}$", true),
        [DocumentType.CE] = new DocumentTypeRule(DocumentType.CE, "CE", 9, @"^\d{9}$", true),
        [DocumentType.RUC] = new DocumentTypeRule(DocumentType.RUC, "RUC", 11, @"^(10|15|16|17|20)\d{9}$", true),
        [DocumentType.PASAPORTE] = new DocumentTypeRule(DocumentType.PASAPORTE, "Pasaporte", null, @"^[A-Z0-9]{6,12}$", true)
    };

    public void SetRule(DocumentTypeRule rule) => _rules[rule.Code] = rule;

    public Task<DocumentTypeRule?> GetRuleAsync(DocumentType type, CancellationToken ct = default) =>
        Task.FromResult(_rules.TryGetValue(type, out var r) ? r : null);
}

internal sealed class InMemoryCustomerRepository : ICustomerRepository
{
    public List<Customer> Items { get; } = [];

    public void Add(Customer customer) => Items.Add(customer);

    public Task<Customer?> FindByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(Items.FirstOrDefault(c => c.Id == id));

    public Task<bool> ExistsByDocumentAsync(DocumentType type, string normalizedNumber, CancellationToken ct = default) =>
        Task.FromResult(Items.Any(c => c.Document.Type == type && c.Document.Number == normalizedNumber));

    public Task<Guid?> FindIdByPortalUserIdAsync(Guid portalUserId, CancellationToken ct = default) =>
        Task.FromResult(Items.FirstOrDefault(c => c.PortalUserId == portalUserId)?.Id);

    public Task<PagedResult<Customer>> SearchAsync(CustomerSearchFilter filter, CancellationToken ct = default)
    {
        IEnumerable<Customer> q = Items;
        if (!string.IsNullOrWhiteSpace(filter.Query))
        {
            var term = filter.Query.Trim();
            q = q.Where(c =>
                c.FullName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                c.Document.Number.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                c.Email.Contains(term, StringComparison.OrdinalIgnoreCase));
        }
        if (filter.KycStatus.HasValue) q = q.Where(c => c.KycStatus == filter.KycStatus.Value);
        if (filter.PersonType.HasValue) q = q.Where(c => c.PersonType == filter.PersonType.Value);
        if (filter.CreatedFromUtc.HasValue) q = q.Where(c => c.CreatedAt >= filter.CreatedFromUtc.Value);
        if (filter.CreatedToUtc.HasValue) q = q.Where(c => c.CreatedAt < filter.CreatedToUtc.Value);

        var ordered = q.OrderByDescending(c => c.CreatedAt).ToList();
        var pageItems = ordered.Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).ToArray();
        return Task.FromResult(new PagedResult<Customer>(pageItems, ordered.Count, filter.Page, filter.PageSize));
    }
}

internal sealed class InMemoryDocumentRepository : ICustomerDocumentRepository
{
    public List<CustomerDocument> Items { get; } = [];

    public void Add(CustomerDocument document) => Items.Add(document);

    public Task<CustomerDocument?> FindByIdAsync(Guid customerId, Guid documentId, CancellationToken ct = default) =>
        Task.FromResult(Items.FirstOrDefault(d => d.CustomerId == customerId && d.Id == documentId));

    public Task<IReadOnlyList<CustomerDocument>> ListByCustomerAsync(Guid customerId, bool includeHistory, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<CustomerDocument>>(Items
            .Where(d => d.CustomerId == customerId && (includeHistory || d.IsCurrent))
            .OrderByDescending(d => d.UploadedAt)
            .ToArray());

    public Task<IReadOnlyList<CustomerDocument>> GetCurrentByCategoryAsync(Guid customerId, DocumentCategory category, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<CustomerDocument>>(Items
            .Where(d => d.CustomerId == customerId && d.DocCategory == category && d.IsCurrent)
            .ToArray());

    public Task<IReadOnlySet<DocumentCategory>> GetCurrentCategoriesAsync(Guid customerId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlySet<DocumentCategory>>(Items
            .Where(d => d.CustomerId == customerId && d.IsCurrent)
            .Select(d => d.DocCategory)
            .ToHashSet());
}

internal sealed class InMemoryKycVerificationRepository : IKycVerificationRepository
{
    public List<KycVerification> Items { get; } = [];

    public void Add(KycVerification verification) => Items.Add(verification);

    public Task<IReadOnlyList<KycVerification>> GetByCustomerAsync(Guid customerId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<KycVerification>>(Items.Where(v => v.CustomerId == customerId).ToArray());

    public Task<IReadOnlyList<KycVerification>> GetLatestByCustomerAsync(Guid customerId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<KycVerification>>(Items
            .Where(v => v.CustomerId == customerId)
            .GroupBy(v => v.CheckType)
            .Select(g => g.OrderByDescending(v => v.RequestedAt).First())
            .ToArray());
}

internal sealed class InMemoryConsentTextRepository : IConsentTextRepository
{
    public List<ConsentText> Items { get; } =
    [
        ConsentText.Create(ConsentPurpose.DATA_PROCESSING, "v1.0", "Autorizo tratamiento de datos.", "sha-dp-1", true, DateTimeOffset.UtcNow.AddDays(-10)),
        ConsentText.Create(ConsentPurpose.CREDIT_BUREAU_QUERY, "v1.0", "Autorizo consulta a centrales.", "sha-cb-1", true, DateTimeOffset.UtcNow.AddDays(-10)),
        ConsentText.Create(ConsentPurpose.MARKETING, "v1.0", "Autorizo comunicaciones comerciales.", "sha-mk-1", true, DateTimeOffset.UtcNow.AddDays(-10))
    ];

    public Task<IReadOnlyList<ConsentText>> GetCurrentAllAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<ConsentText>>(Items.Where(t => t.IsCurrent).ToArray());

    public Task<ConsentText?> FindCurrentByPurposeAsync(ConsentPurpose purpose, CancellationToken ct = default) =>
        Task.FromResult(Items.FirstOrDefault(t => t.IsCurrent && t.Purpose == purpose));
}

internal sealed class InMemoryCustomerConsentRepository : ICustomerConsentRepository
{
    public List<CustomerConsent> Items { get; } = [];

    public void Add(CustomerConsent consent) => Items.Add(consent);

    public Task<IReadOnlyList<CustomerConsent>> ListByCustomerAsync(Guid customerId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<CustomerConsent>>(Items.Where(c => c.CustomerId == customerId).ToArray());

    public Task<CustomerConsent?> FindActiveAsync(Guid customerId, ConsentPurpose purpose, CancellationToken ct = default) =>
        Task.FromResult(Items.FirstOrDefault(c => c.CustomerId == customerId && c.Purpose == purpose && c.RevokedAt == null));
}

internal sealed class InMemoryBankAccountRepository : ICustomerBankAccountRepository, IBankAccountExistenceLookup
{
    public List<CustomerBankAccount> Items { get; } = [];

    public void Add(CustomerBankAccount account) => Items.Add(account);

    public Task<CustomerBankAccount?> FindByIdAsync(Guid customerId, Guid accountId, CancellationToken ct = default) =>
        Task.FromResult(Items.FirstOrDefault(a => a.CustomerId == customerId && a.Id == accountId));

    public Task<IReadOnlyList<CustomerBankAccount>> ListByCustomerAsync(Guid customerId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<CustomerBankAccount>>(Items.Where(a => a.CustomerId == customerId).ToArray());

    public Task<CustomerBankAccount?> FindPrimaryActiveAsync(Guid customerId, CancellationToken ct = default) =>
        Task.FromResult(Items.FirstOrDefault(a =>
            a.CustomerId == customerId && a.IsPrimary && a.Status == BankAccountStatus.ACTIVE));

    public Task<bool> ExistsActiveByHashAsync(Guid customerId, string accountNumberHash, CancellationToken ct = default) =>
        Task.FromResult(Items.Any(a =>
            a.CustomerId == customerId && a.Status != BankAccountStatus.INACTIVE && a.AccountNumberHash == accountNumberHash));

    public Task<int> CountNonInactiveAsync(Guid customerId, CancellationToken ct = default) =>
        Task.FromResult(Items.Count(a => a.CustomerId == customerId && a.Status != BankAccountStatus.INACTIVE));

    public Task<(bool Exists, CustomerBankAccount? Account)> FindGlobalByIdAsync(Guid accountId, CancellationToken ct = default)
    {
        var found = Items.FirstOrDefault(a => a.Id == accountId);
        return Task.FromResult((found is not null, found));
    }
}

internal sealed class InMemoryOtpRepository : IOtpRepository
{
    public List<OtpCode> Items { get; } = [];

    public void Add(OtpCode otp) => Items.Add(otp);

    public Task<OtpCode?> FindByIdAsync(Guid otpRequestId, CancellationToken ct = default) =>
        Task.FromResult(Items.FirstOrDefault(o => o.Id == otpRequestId));

    public Task<IReadOnlyList<OtpCode>> FindActiveByCustomerAndPurposeAsync(Guid customerId, OtpPurpose purpose, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<OtpCode>>(Items
            .Where(o => o.CustomerId == customerId && o.Purpose == purpose && o.ConsumedAt == null && o.InvalidatedAt == null)
            .ToArray());

    public Task<OtpCode?> GetLatestByCustomerAndPurposeAsync(Guid customerId, OtpPurpose purpose, CancellationToken ct = default) =>
        Task.FromResult(Items
            .Where(o => o.CustomerId == customerId && o.Purpose == purpose)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefault());

    public Task<int> CountIssuedSinceAsync(Guid customerId, OtpPurpose purpose, DateTimeOffset sinceUtc, CancellationToken ct = default) =>
        Task.FromResult(Items.Count(o => o.CustomerId == customerId && o.Purpose == purpose && o.CreatedAt >= sinceUtc));
}

internal sealed class FakeDocumentStorage : IDocumentStorage, ILocalSignedFileReader
{
    public Dictionary<string, byte[]> Blobs { get; } = [];
    public Dictionary<string, LocalSignedFile> LocalTokens { get; } = [];

    public async Task<StoredObject> PutAsync(string key, Stream content, string contentType, CancellationToken ct = default)
    {
        using var ms = new MemoryStream();
        await content.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();
        Blobs[key] = bytes;
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        return new StoredObject(key, hash, bytes.Length);
    }

    public Task<Uri> GetSignedReadUrlAsync(string key, TimeSpan ttl, CancellationToken ct = default) =>
        Task.FromResult(new Uri($"https://storage.local/signed/{Uri.EscapeDataString(key)}?ttl={(int)ttl.TotalSeconds}"));

    public Task<bool> ExistsAsync(string key, CancellationToken ct = default) =>
        Task.FromResult(Blobs.ContainsKey(key));

    public Task<LocalSignedFile?> OpenSignedReadAsync(string token, CancellationToken ct = default) =>
        Task.FromResult(LocalTokens.TryGetValue(token, out var f) ? f : null);
}

internal sealed class FakeIdentityVerifier : IIdentityVerifier
{
    public string ProviderName => "FakeTestVerifier";
    public Func<KycCheckType, int, KycCheckOutcome>? CustomHandler { get; set; }
    private readonly Dictionary<KycCheckType, int> _callCounts = [];

    public Task<KycCheckOutcome> VerifyRegistryAsync(IdentityRegistryQuery query, CancellationToken ct = default) =>
        InvokeAsync(KycCheckType.IDENTITY_REGISTRY, () =>
            new KycCheckOutcome(KycCheckType.IDENTITY_REGISTRY, KycCheckResult.PASS, 1.0m, "REG-1", "{}", null));

    public Task<KycCheckOutcome> VerifyLivenessAsync(BiometricQuery query, CancellationToken ct = default) =>
        InvokeAsync(KycCheckType.BIOMETRIC_LIVENESS, () =>
            new KycCheckOutcome(KycCheckType.BIOMETRIC_LIVENESS, KycCheckResult.PASS, 0.99m, "BIO-1", "{}", null));

    public Task<KycCheckOutcome> ScreenWatchlistsAsync(WatchlistQuery query, CancellationToken ct = default) =>
        InvokeAsync(KycCheckType.PEP_SCREENING, () =>
            new KycCheckOutcome(KycCheckType.PEP_SCREENING, KycCheckResult.PASS, 0.0m, "PEP-1", "{}", null));

    public int GetCallCount(KycCheckType type) => _callCounts.TryGetValue(type, out var n) ? n : 0;

    private Task<KycCheckOutcome> InvokeAsync(KycCheckType type, Func<KycCheckOutcome> fallback)
    {
        var count = GetCallCount(type) + 1;
        _callCounts[type] = count;
        var outcome = CustomHandler is not null ? CustomHandler(type, count) : fallback();
        return Task.FromResult(outcome);
    }
}

internal sealed class StubOtpHasher : IOtpHasher
{
    public string Hash(string rawCode) => $"HMAC:{rawCode}";
    public bool Verify(string rawCode, string storedHash) => storedHash == $"HMAC:{rawCode}";
}

internal sealed class FakeOtpSender : IOtpSender
{
    public List<(Guid CustomerId, OtpChannel Channel, string Target, string Code, OtpPurpose Purpose)> Sent { get; } = [];

    public Task SendAsync(Guid customerId, OtpChannel channel, string target, string code, OtpPurpose purpose, CancellationToken ct = default)
    {
        Sent.Add((customerId, channel, target, code, purpose));
        return Task.CompletedTask;
    }
}

internal sealed class StubFieldProtector : IFieldProtector
{
    public string Protect(string plaintext) => $"v1:{Convert.ToBase64String(Encoding.UTF8.GetBytes(plaintext))}";
    public string Unprotect(string protectedValue) =>
        Encoding.UTF8.GetString(Convert.FromBase64String(protectedValue[3..]));
    public string Hash(string plaintext) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(plaintext))).ToLowerInvariant();
}

internal sealed class FakeCustomerAuditWriter : ICustomerAuditWriter
{
    public List<(string EntityName, string EntityId, string Action, Guid? UserId, string? MetadataJson)> Entries { get; } = [];

    public Task WriteAsync(
        string entityName,
        string entityId,
        string action,
        Guid? userId,
        IPAddress? userIp,
        string? userAgent,
        string? metadataJson = null,
        CancellationToken ct = default)
    {
        Entries.Add((entityName, entityId, action, userId, metadataJson));
        return Task.CompletedTask;
    }
}

internal sealed class FakeCustomerDiagnostics : ICustomerDiagnostics
{
    public List<(string StorageKey, Guid CustomerId)> OrphanedObjects { get; } = [];
    public List<(KycCheckType Type, Guid CustomerId, int Attempt)> KycFailures { get; } = [];

    public void LogOrphanedDocumentObject(string storageKey, Guid customerId, Exception exception) =>
        OrphanedObjects.Add((storageKey, customerId));

    public void LogKycCheckAttemptFailure(KycCheckType checkType, Guid customerId, int attempt, int maxAttempts, Exception exception) =>
        KycFailures.Add((checkType, customerId, attempt));
}

internal sealed class FakeCustomerHistoryReader : ICustomerHistoryReader
{
    public List<CustomerChangeEntry> Entries { get; } = [];

    public Task<PagedResult<CustomerChangeEntry>> GetHistoryAsync(Guid customerId, int page, int pageSize, CancellationToken ct = default)
    {
        var items = Entries.Skip((page - 1) * pageSize).Take(pageSize).ToArray();
        return Task.FromResult(new PagedResult<CustomerChangeEntry>(items, Entries.Count, page, pageSize));
    }
}

internal sealed class FakeCustomerPortfolioReader : ICustomerPortfolioReader
{
    public CustomerPortfolio Portfolio { get; set; } = new([], []);

    public Task<CustomerPortfolio> GetAsync(Guid customerId, CancellationToken ct = default) =>
        Task.FromResult(Portfolio);
}

internal sealed class FakeKycExpirationBatchStore : IKycExpirationBatchStore
{
    public List<(Guid CustomerId, DateTimeOffset ExpiresAt)> DueToExpire { get; } = [];
    public List<(Guid CustomerId, DateTimeOffset ExpiresAt)> ExpiringSoon { get; } = [];

    public Task<IReadOnlyList<(Guid CustomerId, DateTimeOffset ExpiresAt)>> ExpireDueBatchAsync(DateTimeOffset utcNow, int batchSize, CancellationToken ct = default)
    {
        var taken = DueToExpire.Take(batchSize).ToArray();
        DueToExpire.RemoveRange(0, taken.Length);
        return Task.FromResult<IReadOnlyList<(Guid, DateTimeOffset)>>(taken);
    }

    public Task<IReadOnlyList<(Guid CustomerId, DateTimeOffset ExpiresAt)>> FindExpiringSoonAsync(DateTimeOffset utcNow, DateTimeOffset windowEndUtc, int batchSize, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<(Guid, DateTimeOffset)>>(ExpiringSoon.Take(batchSize).ToArray());
}

internal sealed class StubBanksCatalog : IBanksCatalog
{
    private static readonly Dictionary<string, string> Map = new()
    {
        ["002"] = "Banco de Crédito del Perú",
        ["003"] = "Interbank",
        ["011"] = "BBVA Perú"
    };

    public bool TryGetBankName(string bankCode, out string bankName) => Map.TryGetValue(bankCode, out bankName!);
    public IReadOnlyDictionary<string, string> GetAll() => Map;
}

internal static class CustomersTestFactory
{
    public static Customer CreateNaturalCustomer(
        IClock clock,
        string dni = "12345678",
        string fullName = "Ana María Quispe",
        decimal income = 2500m,
        Guid? portalUserId = null)
    {
        var doc = IdentityDocument.Create(
            DocumentType.DNI,
            dni,
            new DocumentTypeRule(DocumentType.DNI, "DNI", 8, @"^\d{8}$", true)).Value;

        var customer = Customer.RegisterNatural(
            doc,
            PersonName.Create(fullName).Value,
            new DateOnly(1994, 4, 15),
            ContactInfo.Create("ana.quispe@test.pe", "987654321", null).Value,
            "Av. Primavera 120",
            "Surco",
            SocioeconomicProfile.Create(income, "Contadora", "Estudio Lima", 18).Value,
            Guid.NewGuid(),
            clock).Value;

        if (portalUserId.HasValue)
        {
            customer.LinkPortalUser(portalUserId.Value, clock);
        }

        return customer;
    }
}
