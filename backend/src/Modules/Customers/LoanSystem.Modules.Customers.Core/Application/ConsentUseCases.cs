using LoanSystem.Common.Core.Application;
using LoanSystem.Common.Core.Ports.Outbound;
using LoanSystem.Modules.Customers.Core.Contracts;
using LoanSystem.Modules.Customers.Core.Contracts.IntegrationEvents;
using LoanSystem.Modules.Customers.Core.Domain;
using LoanSystem.Modules.Customers.Core.Ports.Inbound;
using LoanSystem.Modules.Customers.Core.Ports.Outbound;

namespace LoanSystem.Modules.Customers.Core.Application;

internal sealed class GetCurrentConsentsUseCase : IGetCurrentConsentsUseCase
{
    private readonly IConsentTextRepository _consentTexts;

    public GetCurrentConsentsUseCase(IConsentTextRepository consentTexts)
    {
        _consentTexts = consentTexts;
    }

    public async Task<Result<IReadOnlyList<CurrentConsentTextResponse>>> ExecuteAsync(CancellationToken ct = default)
    {
        var texts = await _consentTexts.GetCurrentAllAsync(ct);
        var mapped = texts
            .Select(t => new CurrentConsentTextResponse(
                t.Purpose.ToString(),
                t.Version,
                t.Body,
                t.Sha256,
                t.PublishedAt))
            .ToArray();

        return mapped;
    }
}

internal sealed class ListCustomerConsentsUseCase : IListCustomerConsentsUseCase
{
    private readonly ICustomerRepository _customers;
    private readonly ICustomerConsentRepository _consents;
    private readonly ICurrentUser _currentUser;

    public ListCustomerConsentsUseCase(
        ICustomerRepository customers,
        ICustomerConsentRepository consents,
        ICurrentUser currentUser)
    {
        _customers = customers;
        _consents = consents;
        _currentUser = currentUser;
    }

    public async Task<Result<IReadOnlyList<CustomerConsentResponse>>> ExecuteAsync(
        Guid customerId,
        CancellationToken ct = default)
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

        if (await _customers.FindByIdAsync(customerId, ct) is null)
        {
            return CustomerErrors.NotFound(customerId);
        }

        var rows = await _consents.ListByCustomerAsync(customerId, ct);
        return rows
            .Select(r => new CustomerConsentResponse(
                r.Id,
                r.Purpose.ToString(),
                r.ConsentText?.Version ?? string.Empty,
                r.GrantedAt,
                r.Channel,
                r.RevokedAt))
            .ToArray();
    }
}

internal sealed class GrantCustomerConsentUseCase : IGrantCustomerConsentUseCase
{
    private readonly ICustomerRepository _customers;
    private readonly IConsentTextRepository _consentTexts;
    private readonly ICustomerConsentRepository _consents;
    private readonly ICustomersUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IClientInfoProvider _clientInfo;
    private readonly IClock _clock;
    private readonly IAuditContext _audit;

    public GrantCustomerConsentUseCase(
        ICustomerRepository customers,
        IConsentTextRepository consentTexts,
        ICustomerConsentRepository consents,
        ICustomersUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClientInfoProvider clientInfo,
        IClock clock,
        IAuditContext audit)
    {
        _customers = customers;
        _consentTexts = consentTexts;
        _consents = consents;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _clientInfo = clientInfo;
        _clock = clock;
        _audit = audit;
    }

    public async Task<Result<CustomerConsentResponse>> ExecuteAsync(
        GrantConsentCommand command,
        CancellationToken ct = default)
    {
        if (!CustomerMapper.HasCustomerAccess(
                _currentUser,
                command.CustomerId,
                SecurityPolicies.Admin,
                SecurityPolicies.Analyst,
                SecurityPolicies.Operator))
        {
            return CustomerErrors.NotFound(command.CustomerId);
        }

        if (!Enum.TryParse<ConsentPurpose>(command.Purpose?.Trim(), ignoreCase: true, out var purpose))
        {
            return CustomerErrors.Validation($"Invalid consent purpose '{command.Purpose}'.");
        }

        if (await _customers.FindByIdAsync(command.CustomerId, ct) is null)
        {
            return CustomerErrors.NotFound(command.CustomerId);
        }

        var currentText = await _consentTexts.FindCurrentByPurposeAsync(purpose, ct);
        if (currentText is null)
        {
            return CustomerErrors.ConsentVersionOutdated(purpose.ToString(), "<unavailable>");
        }

        if (!string.Equals(currentText.Version, command.Version?.Trim(), StringComparison.Ordinal))
        {
            return CustomerErrors.ConsentVersionOutdated(purpose.ToString(), currentText.Version);
        }

        var active = await _consents.FindActiveAsync(command.CustomerId, purpose, ct);
        if (active is not null)
        {
            if (active.ConsentTextId == currentText.Id)
            {
                return new CustomerConsentResponse(
                    active.Id,
                    active.Purpose.ToString(),
                    currentText.Version,
                    active.GrantedAt,
                    active.Channel,
                    active.RevokedAt);
            }

            // Revoking the prior consent row clears ux_customer_consents_active before granting the newer version
            active.Revoke(_clientInfo.IpAddress, _clock);
        }

        var channel = _currentUser.IsInRole(SecurityPolicies.Client) &&
                      !CustomerMapper.CanViewUnmaskedPii(_currentUser)
            ? "PORTAL"
            : "BACKOFFICE";

        var consent = CustomerConsent.Grant(
            command.CustomerId,
            currentText,
            _clientInfo.IpAddress,
            _clientInfo.UserAgent,
            channel,
            _clock);

        _audit.SetAction("GRANT_CONSENT", "customer_consents");
        _consents.Add(consent);
        await _unitOfWork.SaveChangesAsync(ct);

        return new CustomerConsentResponse(
            consent.Id,
            consent.Purpose.ToString(),
            currentText.Version,
            consent.GrantedAt,
            consent.Channel,
            consent.RevokedAt);
    }
}

internal sealed class RevokeCustomerConsentUseCase : IRevokeCustomerConsentUseCase
{
    private readonly ICustomerRepository _customers;
    private readonly ICustomerConsentRepository _consents;
    private readonly ICustomersUnitOfWork _unitOfWork;
    private readonly ICustomersOutbox _outbox;
    private readonly ICurrentUser _currentUser;
    private readonly IClientInfoProvider _clientInfo;
    private readonly IClock _clock;
    private readonly IAuditContext _audit;

    public RevokeCustomerConsentUseCase(
        ICustomerRepository customers,
        ICustomerConsentRepository consents,
        ICustomersUnitOfWork unitOfWork,
        ICustomersOutbox outbox,
        ICurrentUser currentUser,
        IClientInfoProvider clientInfo,
        IClock clock,
        IAuditContext audit)
    {
        _customers = customers;
        _consents = consents;
        _unitOfWork = unitOfWork;
        _outbox = outbox;
        _currentUser = currentUser;
        _clientInfo = clientInfo;
        _clock = clock;
        _audit = audit;
    }

    public async Task<Result> ExecuteAsync(RevokeConsentCommand command, CancellationToken ct = default)
    {
        if (!CustomerMapper.HasCustomerAccess(
                _currentUser,
                command.CustomerId,
                SecurityPolicies.Admin,
                SecurityPolicies.Analyst,
                SecurityPolicies.Operator))
        {
            return CustomerErrors.NotFound(command.CustomerId);
        }

        if (!Enum.TryParse<ConsentPurpose>(command.Purpose?.Trim(), ignoreCase: true, out var purpose))
        {
            return CustomerErrors.Validation($"Invalid consent purpose '{command.Purpose}'.");
        }

        if (purpose != ConsentPurpose.MARKETING)
        {
            return CustomerErrors.ConsentNotRevocable(purpose.ToString());
        }

        if (await _customers.FindByIdAsync(command.CustomerId, ct) is null)
        {
            return CustomerErrors.NotFound(command.CustomerId);
        }

        var active = await _consents.FindActiveAsync(command.CustomerId, ConsentPurpose.MARKETING, ct);
        if (active is null)
        {
            return Result.Success();
        }

        await using var tx = await _unitOfWork.BeginTransactionAsync(ct);

        _audit.SetAction("REVOKE_CONSENT", "customer_consents");
        var revokeResult = active.Revoke(_clientInfo.IpAddress, _clock);
        if (revokeResult.IsFailure)
        {
            return revokeResult;
        }

        _outbox.Enqueue(new CustomerMarketingConsentRevokedIntegrationEvent(command.CustomerId));

        await _unitOfWork.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Result.Success();
    }
}

internal sealed class CustomerConsentQueryService : ICustomerConsentQuery
{
    private readonly ICustomerConsentRepository _consents;

    public CustomerConsentQueryService(ICustomerConsentRepository consents)
    {
        _consents = consents;
    }

    public async Task<bool> HasActiveAsync(Guid customerId, string purpose, CancellationToken ct = default)
    {
        if (!Enum.TryParse<ConsentPurpose>(purpose?.Trim(), ignoreCase: true, out var parsed))
        {
            return false;
        }

        return await _consents.FindActiveAsync(customerId, parsed, ct) is not null;
    }
}

internal sealed class CustomerPortalLookupService : ICustomerPortalLookup
{
    private readonly ICustomerRepository _customers;

    public CustomerPortalLookupService(ICustomerRepository customers)
    {
        _customers = customers;
    }

    public Task<Guid?> FindCustomerIdByPortalUserIdAsync(Guid portalUserId, CancellationToken ct = default) =>
        _customers.FindIdByPortalUserIdAsync(portalUserId, ct);
}
