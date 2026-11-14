using LoanSystem.Common.Core.Application;
using LoanSystem.Common.Core.Ports.Outbound;
using LoanSystem.Modules.Customers.Core.Contracts;
using LoanSystem.Modules.Customers.Core.Contracts.IntegrationEvents;
using LoanSystem.Modules.Customers.Core.Domain;
using LoanSystem.Modules.Customers.Core.Ports.Inbound;
using LoanSystem.Modules.Customers.Core.Ports.Outbound;

namespace LoanSystem.Modules.Customers.Core.Application;

internal sealed class GetCustomerKycUseCase : IGetCustomerKycUseCase
{
    private readonly ICustomerRepository _customers;
    private readonly ICustomerDocumentRepository _documents;
    private readonly IKycVerificationRepository _verifications;
    private readonly ICurrentUser _currentUser;
    private readonly CustomersOptions _options;

    public GetCustomerKycUseCase(
        ICustomerRepository customers,
        ICustomerDocumentRepository documents,
        IKycVerificationRepository verifications,
        ICurrentUser currentUser,
        CustomersOptions options)
    {
        _customers = customers;
        _documents = documents;
        _verifications = verifications;
        _currentUser = currentUser;
        _options = options;
    }

    public async Task<Result<CustomerKycStatusResponse>> ExecuteAsync(Guid customerId, CancellationToken ct = default)
    {
        if (!CustomerMapper.HasCustomerAccess(
                _currentUser,
                customerId,
                SecurityPolicies.Admin,
                SecurityPolicies.Analyst,
                SecurityPolicies.Operator,
                SecurityPolicies.Auditor))
        {
            return CustomerErrors.NotFound(customerId);
        }

        var customer = await _customers.FindByIdAsync(customerId, ct);
        if (customer is null)
        {
            return CustomerErrors.NotFound(customerId);
        }

        var currentDocs = await _documents.ListByCustomerAsync(customerId, includeHistory: false, ct);
        var latestChecks = await _verifications.GetLatestByCustomerAsync(customerId, ct);

        return CustomerMapper.ToKycStatusResponse(
            customer,
            currentDocs,
            latestChecks,
            _options.Kyc.IncomeProofThreshold);
    }
}

internal sealed class SubmitKycUseCase : ISubmitKycUseCase
{
    private readonly ICustomerRepository _customers;
    private readonly ICustomerDocumentRepository _documents;
    private readonly IKycVerificationRepository _verifications;
    private readonly ICustomersUnitOfWork _unitOfWork;
    private readonly ICustomersOutbox _outbox;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IAuditContext _audit;
    private readonly CustomersOptions _options;

    public SubmitKycUseCase(
        ICustomerRepository customers,
        ICustomerDocumentRepository documents,
        IKycVerificationRepository verifications,
        ICustomersUnitOfWork unitOfWork,
        ICustomersOutbox outbox,
        ICurrentUser currentUser,
        IClock clock,
        IAuditContext audit,
        CustomersOptions options)
    {
        _customers = customers;
        _documents = documents;
        _verifications = verifications;
        _unitOfWork = unitOfWork;
        _outbox = outbox;
        _currentUser = currentUser;
        _clock = clock;
        _audit = audit;
        _options = options;
    }

    public async Task<Result<CustomerKycStatusResponse>> ExecuteAsync(Guid customerId, CancellationToken ct = default)
    {
        if (!CustomerMapper.HasCustomerAccess(
                _currentUser,
                customerId,
                SecurityPolicies.Admin,
                SecurityPolicies.Analyst,
                SecurityPolicies.Operator))
        {
            return CustomerErrors.NotFound(customerId);
        }

        var customer = await _customers.FindByIdAsync(customerId, ct);
        if (customer is null)
        {
            return CustomerErrors.NotFound(customerId);
        }

        var presentCategories = await _documents.GetCurrentCategoriesAsync(customerId, ct);
        var submitResult = customer.SubmitKyc(presentCategories, _options.Kyc.IncomeProofThreshold, _clock);
        if (submitResult.IsFailure)
        {
            return submitResult.Error;
        }

        await using var tx = await _unitOfWork.BeginTransactionAsync(ct);

        _audit.SetAction("KYC_SUBMIT", "customers");
        _outbox.Enqueue(new CustomerKycSubmittedIntegrationEvent(customer.Id));

        await _unitOfWork.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        var currentDocs = await _documents.ListByCustomerAsync(customerId, includeHistory: false, ct);
        var latestChecks = await _verifications.GetLatestByCustomerAsync(customerId, ct);
        return CustomerMapper.ToKycStatusResponse(customer, currentDocs, latestChecks, _options.Kyc.IncomeProofThreshold);
    }
}

internal sealed class ApproveKycUseCase : IApproveKycUseCase
{
    private readonly ICustomerRepository _customers;
    private readonly ICustomerDocumentRepository _documents;
    private readonly IKycVerificationRepository _verifications;
    private readonly ICustomersUnitOfWork _unitOfWork;
    private readonly ICustomersOutbox _outbox;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IAuditContext _audit;
    private readonly CustomersOptions _options;

    public ApproveKycUseCase(
        ICustomerRepository customers,
        ICustomerDocumentRepository documents,
        IKycVerificationRepository verifications,
        ICustomersUnitOfWork unitOfWork,
        ICustomersOutbox outbox,
        ICurrentUser currentUser,
        IClock clock,
        IAuditContext audit,
        CustomersOptions options)
    {
        _customers = customers;
        _documents = documents;
        _verifications = verifications;
        _unitOfWork = unitOfWork;
        _outbox = outbox;
        _currentUser = currentUser;
        _clock = clock;
        _audit = audit;
        _options = options;
    }

    public async Task<Result<CustomerKycStatusResponse>> ExecuteAsync(
        ApproveKycCommand command,
        CancellationToken ct = default)
    {
        var customer = await _customers.FindByIdAsync(command.CustomerId, ct);
        if (customer is null)
        {
            return CustomerErrors.NotFound(command.CustomerId);
        }

        var latestChecks = await _verifications.GetLatestByCustomerAsync(command.CustomerId, ct);
        var isAdmin = _currentUser.IsInRole(SecurityPolicies.Admin);
        var actorId = _currentUser.UserId ?? Guid.Empty;

        var result = customer.ApproveKyc(
            actorId,
            command.Notes,
            isAdmin,
            latestChecks,
            _clock,
            _options.Kyc.ValidityMonths);

        if (result.IsFailure)
        {
            return result.Error;
        }

        await using var tx = await _unitOfWork.BeginTransactionAsync(ct);

        _audit.SetAction("KYC_APPROVE", "customers");
        _outbox.Enqueue(new CustomerKycVerifiedIntegrationEvent(customer.Id, customer.KycExpiresAt!.Value));

        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (ConcurrencyConflictException)
        {
            return CustomerErrors.ConcurrencyConflict;
        }

        var currentDocs = await _documents.ListByCustomerAsync(command.CustomerId, includeHistory: false, ct);
        return CustomerMapper.ToKycStatusResponse(customer, currentDocs, latestChecks, _options.Kyc.IncomeProofThreshold);
    }
}

internal sealed class RejectKycUseCase : IRejectKycUseCase
{
    private readonly ICustomerRepository _customers;
    private readonly ICustomerDocumentRepository _documents;
    private readonly IKycVerificationRepository _verifications;
    private readonly ICustomersUnitOfWork _unitOfWork;
    private readonly ICustomersOutbox _outbox;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IAuditContext _audit;
    private readonly CustomersOptions _options;

    public RejectKycUseCase(
        ICustomerRepository customers,
        ICustomerDocumentRepository documents,
        IKycVerificationRepository verifications,
        ICustomersUnitOfWork unitOfWork,
        ICustomersOutbox outbox,
        ICurrentUser currentUser,
        IClock clock,
        IAuditContext audit,
        CustomersOptions options)
    {
        _customers = customers;
        _documents = documents;
        _verifications = verifications;
        _unitOfWork = unitOfWork;
        _outbox = outbox;
        _currentUser = currentUser;
        _clock = clock;
        _audit = audit;
        _options = options;
    }

    public async Task<Result<CustomerKycStatusResponse>> ExecuteAsync(
        RejectKycCommand command,
        CancellationToken ct = default)
    {
        var customer = await _customers.FindByIdAsync(command.CustomerId, ct);
        if (customer is null)
        {
            return CustomerErrors.NotFound(command.CustomerId);
        }

        var isAdmin = _currentUser.IsInRole(SecurityPolicies.Admin);
        var actorId = _currentUser.UserId ?? Guid.Empty;

        var result = customer.RejectKyc(actorId, command.Reason, isAdmin, _clock);
        if (result.IsFailure)
        {
            return result.Error;
        }

        await using var tx = await _unitOfWork.BeginTransactionAsync(ct);

        _audit.SetAction("KYC_REJECT", "customers");
        _outbox.Enqueue(new CustomerKycRejectedIntegrationEvent(customer.Id, customer.KycRejectedReason!));

        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (ConcurrencyConflictException)
        {
            return CustomerErrors.ConcurrencyConflict;
        }

        var currentDocs = await _documents.ListByCustomerAsync(command.CustomerId, includeHistory: false, ct);
        var latestChecks = await _verifications.GetLatestByCustomerAsync(command.CustomerId, ct);
        return CustomerMapper.ToKycStatusResponse(customer, currentDocs, latestChecks, _options.Kyc.IncomeProofThreshold);
    }
}

internal sealed class ResubmitKycUseCase : IResubmitKycUseCase
{
    private readonly ICustomerRepository _customers;
    private readonly ICustomerDocumentRepository _documents;
    private readonly IKycVerificationRepository _verifications;
    private readonly ICustomersUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IAuditContext _audit;
    private readonly CustomersOptions _options;

    public ResubmitKycUseCase(
        ICustomerRepository customers,
        ICustomerDocumentRepository documents,
        IKycVerificationRepository verifications,
        ICustomersUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
        IAuditContext audit,
        CustomersOptions options)
    {
        _customers = customers;
        _documents = documents;
        _verifications = verifications;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _clock = clock;
        _audit = audit;
        _options = options;
    }

    public async Task<Result<CustomerKycStatusResponse>> ExecuteAsync(Guid customerId, CancellationToken ct = default)
    {
        if (!CustomerMapper.HasCustomerAccess(
                _currentUser,
                customerId,
                SecurityPolicies.Admin,
                SecurityPolicies.Analyst,
                SecurityPolicies.Operator))
        {
            return CustomerErrors.NotFound(customerId);
        }

        var customer = await _customers.FindByIdAsync(customerId, ct);
        if (customer is null)
        {
            return CustomerErrors.NotFound(customerId);
        }

        var result = customer.ResubmitKyc(_clock);
        if (result.IsFailure)
        {
            return result.Error;
        }

        _audit.SetAction("KYC_RESUBMIT", "customers");
        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (ConcurrencyConflictException)
        {
            return CustomerErrors.ConcurrencyConflict;
        }

        var currentDocs = await _documents.ListByCustomerAsync(customerId, includeHistory: false, ct);
        var latestChecks = await _verifications.GetLatestByCustomerAsync(customerId, ct);
        return CustomerMapper.ToKycStatusResponse(customer, currentDocs, latestChecks, _options.Kyc.IncomeProofThreshold);
    }
}

internal sealed class ExpireKycUseCase : IExpireKycUseCase
{
    private const int BatchSize = 500;

    private readonly IKycExpirationBatchStore _batchStore;
    private readonly ICustomersOutbox _outbox;
    private readonly ICustomersUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly CustomersOptions _options;

    public ExpireKycUseCase(
        IKycExpirationBatchStore batchStore,
        ICustomersOutbox outbox,
        ICustomersUnitOfWork unitOfWork,
        IClock clock,
        CustomersOptions options)
    {
        _batchStore = batchStore;
        _outbox = outbox;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _options = options;
    }

    public async Task<Result<int>> ExecuteAsync(CancellationToken ct = default)
    {
        var totalExpired = 0;

        while (true)
        {
            var expiredBatch = await _batchStore.ExpireDueBatchAsync(_clock.UtcNow, BatchSize, ct);
            if (expiredBatch.Count == 0)
            {
                break;
            }

            foreach (var (customerId, expiresAt) in expiredBatch)
            {
                _outbox.Enqueue(new CustomerKycExpiredIntegrationEvent(customerId, expiresAt));
            }

            await _unitOfWork.SaveChangesAsync(ct);
            totalExpired += expiredBatch.Count;

            if (expiredBatch.Count < BatchSize)
            {
                break;
            }
        }

        var windowEnd = _clock.UtcNow.Add(TimeSpanUtils.Days(_options.Kyc.ExpiringSoonDays));
        var expiringSoon = await _batchStore.FindExpiringSoonAsync(_clock.UtcNow, windowEnd, BatchSize, ct);
        if (expiringSoon.Count > 0)
        {
            foreach (var (customerId, expiresAt) in expiringSoon)
            {
                _outbox.Enqueue(new CustomerKycExpiringSoonIntegrationEvent(customerId, expiresAt));
            }

            await _unitOfWork.SaveChangesAsync(ct);
        }

        return totalExpired;
    }
}

internal sealed class RunKycChecksUseCase : IRunKycChecksUseCase
{
    private const int MaxTransientAttempts = 3;

    private static readonly KycCheckType[] RequiredChecks =
    [
        KycCheckType.IDENTITY_REGISTRY,
        KycCheckType.BIOMETRIC_LIVENESS,
        KycCheckType.PEP_SCREENING
    ];

    private readonly ICustomerRepository _customers;
    private readonly ICustomerDocumentRepository _documents;
    private readonly IKycVerificationRepository _verifications;
    private readonly IIdentityVerifier _verifier;
    private readonly ICustomersUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly CustomersOptions _options;
    private readonly ICustomerDiagnostics _diagnostics;

    public RunKycChecksUseCase(
        ICustomerRepository customers,
        ICustomerDocumentRepository documents,
        IKycVerificationRepository verifications,
        IIdentityVerifier verifier,
        ICustomersUnitOfWork unitOfWork,
        IClock clock,
        CustomersOptions options,
        ICustomerDiagnostics diagnostics)
    {
        _customers = customers;
        _documents = documents;
        _verifications = verifications;
        _verifier = verifier;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _options = options;
        _diagnostics = diagnostics;
    }

    public async Task ExecuteForSubmissionAsync(Guid customerId, CancellationToken ct = default)
    {
        var customer = await _customers.FindByIdAsync(customerId, ct);
        if (customer is null)
        {
            return;
        }

        var existing = await _verifications.GetByCustomerAsync(customerId, ct);
        var currentRoundStart = customer.KycSubmittedAt ?? DateTimeOffset.MinValue;

        var pendingOrToRun = new List<KycVerification>();
        var createdAny = false;

        foreach (var checkType in RequiredChecks)
        {
            var match = existing
                .Where(v => v.CheckType == checkType && v.RequestedAt >= currentRoundStart)
                .OrderByDescending(v => v.RequestedAt)
                .FirstOrDefault();

            if (match is null)
            {
                match = KycVerification.CreatePending(
                    customerId,
                    checkType,
                    _verifier.ProviderName,
                    externalReference: null,
                    requestedBy: null,
                    _clock);
                _verifications.Add(match);
                pendingOrToRun.Add(match);
                createdAny = true;
            }
            else if (match.Result == KycCheckResult.PENDING)
            {
                pendingOrToRun.Add(match);
            }
        }

        if (createdAny)
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }

        if (pendingOrToRun.Count == 0)
        {
            return;
        }

        await ExecuteChecksAsync(customer, pendingOrToRun, ct);
    }

    public async Task<Result<IReadOnlyList<KycVerificationResponse>>> RetryFailedAsync(
        Guid customerId,
        Guid requestedBy,
        CancellationToken ct = default)
    {
        var customer = await _customers.FindByIdAsync(customerId, ct);
        if (customer is null)
        {
            return CustomerErrors.NotFound(customerId);
        }

        var latest = await _verifications.GetLatestByCustomerAsync(customerId, ct);
        var errored = latest.Where(v => v.Result == KycCheckResult.ERROR).ToList();
        if (errored.Count == 0)
        {
            return latest.Select(CustomerMapper.ToVerificationResponse).ToArray();
        }

        foreach (var check in errored)
        {
            check.ResetForRetry(_verifier.ProviderName, requestedBy, _clock);
        }

        await _unitOfWork.SaveChangesAsync(ct);
        await ExecuteChecksAsync(customer, errored, ct);

        var updated = await _verifications.GetLatestByCustomerAsync(customerId, ct);
        return updated.Select(CustomerMapper.ToVerificationResponse).ToArray();
    }

    private async Task ExecuteChecksAsync(
        Customer customer,
        IReadOnlyList<KycVerification> checksToExecute,
        CancellationToken ct)
    {
        var docs = await _documents.ListByCustomerAsync(customer.Id, includeHistory: false, ct);
        var selfieUri = docs.FirstOrDefault(d => d.DocCategory == DocumentCategory.SELFIE)?.StorageUri;
        var frontUri = docs.FirstOrDefault(d => d.DocCategory == DocumentCategory.DNI_FRONTAL)?.StorageUri;

        foreach (var verification in checksToExecute)
        {
            var outcome = await InvokeWithTimeoutAndRetryAsync(
                verification.CheckType,
                customer,
                selfieUri,
                frontUri,
                ct);

            verification.RecordOutcome(
                outcome.Result,
                outcome.Score,
                outcome.ExternalReference,
                outcome.RawResponseJson,
                outcome.Error,
                _clock);

            if (outcome.IsPepFlag)
            {
                customer.MarkAsPep(_clock);
            }
            else if (outcome.IsSanctionsHit)
            {
                customer.MarkHighAmlRisk(_clock);
            }
        }

        await _unitOfWork.SaveChangesAsync(ct);
    }

    private async Task<KycCheckOutcome> InvokeWithTimeoutAndRetryAsync(
        KycCheckType checkType,
        Customer customer,
        string? selfieUri,
        string? frontUri,
        CancellationToken ct)
    {
        var timeout = TimeSpanUtils.Seconds(_options.IdentityVerifier.TimeoutSeconds);

        for (var attempt = 1; attempt <= MaxTransientAttempts; attempt++)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(timeout);

            try
            {
                var outcome = checkType switch
                {
                    KycCheckType.IDENTITY_REGISTRY => await _verifier.VerifyRegistryAsync(
                        new IdentityRegistryQuery(
                            customer.Id,
                            customer.PersonType,
                            customer.Document.Type,
                            customer.Document.Number,
                            customer.FullName,
                            customer.BirthDate),
                        timeoutCts.Token),

                    KycCheckType.BIOMETRIC_LIVENESS => await _verifier.VerifyLivenessAsync(
                        new BiometricQuery(customer.Id, customer.Document.Number, selfieUri, frontUri),
                        timeoutCts.Token),

                    _ => await _verifier.ScreenWatchlistsAsync(
                        new WatchlistQuery(
                            customer.Id,
                            customer.PersonType,
                            customer.Document.Type,
                            customer.Document.Number,
                            customer.FullName),
                        timeoutCts.Token)
                };

                if (!outcome.IsTransientError || attempt == MaxTransientAttempts)
                {
                    return outcome;
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _diagnostics.LogKycCheckAttemptFailure(
                    checkType,
                    customer.Id,
                    attempt,
                    MaxTransientAttempts,
                    ex);

                if (attempt == MaxTransientAttempts)
                {
                    return new KycCheckOutcome(
                        checkType,
                        KycCheckResult.ERROR,
                        null,
                        null,
                        "{\"error\":\"provider_error\"}",
                        "Automatic verification provider timed out or failed.");
                }
            }

            var backoffMs = 50L << (attempt - 1);
            await Task.Delay(TimeSpan.FromTicks(backoffMs * TimeSpan.TicksPerMillisecond), ct);
        }

        return new KycCheckOutcome(
            checkType,
            KycCheckResult.ERROR,
            null,
            null,
            "{\"error\":\"retries_exhausted\"}",
            "Automatic verification failed after retries.");
    }
}

internal sealed class RetryKycChecksUseCase : IRetryKycChecksUseCase
{
    private readonly ICurrentUser _currentUser;
    private readonly RunKycChecksUseCase _runner;

    public RetryKycChecksUseCase(ICurrentUser currentUser, RunKycChecksUseCase runner)
    {
        _currentUser = currentUser;
        _runner = runner;
    }

    public Task<Result<IReadOnlyList<KycVerificationResponse>>> ExecuteAsync(
        Guid customerId,
        CancellationToken ct = default) =>
        _runner.RetryFailedAsync(customerId, _currentUser.UserId ?? Guid.Empty, ct);
}
