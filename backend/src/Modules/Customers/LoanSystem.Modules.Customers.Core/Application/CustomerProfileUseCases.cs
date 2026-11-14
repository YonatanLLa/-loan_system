using LoanSystem.Common.Core.Application;
using LoanSystem.Common.Core.Contracts;
using LoanSystem.Common.Core.Ports.Outbound;
using LoanSystem.Modules.Customers.Core.Contracts;
using LoanSystem.Modules.Customers.Core.Contracts.IntegrationEvents;
using LoanSystem.Modules.Customers.Core.Domain;
using LoanSystem.Modules.Customers.Core.Ports.Inbound;
using LoanSystem.Modules.Customers.Core.Ports.Outbound;

namespace LoanSystem.Modules.Customers.Core.Application;

internal sealed class RegisterCustomerUseCase : IRegisterCustomerUseCase
{
    private readonly IDocumentTypeCatalog _docTypes;
    private readonly ICustomerRepository _customers;
    private readonly IConsentTextRepository _consentTexts;
    private readonly ICustomerConsentRepository _customerConsents;
    private readonly ICustomersUnitOfWork _unitOfWork;
    private readonly ICustomersOutbox _outbox;
    private readonly ICurrentUser _currentUser;
    private readonly IClientInfoProvider _clientInfo;
    private readonly IClock _clock;
    private readonly IAuditContext _audit;

    public RegisterCustomerUseCase(
        IDocumentTypeCatalog docTypes,
        ICustomerRepository customers,
        IConsentTextRepository consentTexts,
        ICustomerConsentRepository customerConsents,
        ICustomersUnitOfWork unitOfWork,
        ICustomersOutbox outbox,
        ICurrentUser currentUser,
        IClientInfoProvider clientInfo,
        IClock clock,
        IAuditContext audit)
    {
        _docTypes = docTypes;
        _customers = customers;
        _consentTexts = consentTexts;
        _customerConsents = customerConsents;
        _unitOfWork = unitOfWork;
        _outbox = outbox;
        _currentUser = currentUser;
        _clientInfo = clientInfo;
        _clock = clock;
        _audit = audit;
    }

    public async Task<Result<CustomerResponse>> ExecuteAsync(RegisterCustomerRequest request, CancellationToken ct = default)
    {
        if (!Enum.TryParse<PersonType>(request.PersonType?.Trim(), ignoreCase: true, out var personType))
        {
            return CustomerErrors.Validation($"Invalid personType '{request.PersonType}'. Expected NATURAL or JURIDICA.");
        }

        if (!Enum.TryParse<DocumentType>(request.DocType?.Trim(), ignoreCase: true, out var docType))
        {
            return CustomerErrors.Validation($"Invalid docType '{request.DocType}'.");
        }

        var rule = await _docTypes.GetRuleAsync(docType, ct);
        if (rule is null || !rule.IsActive)
        {
            return CustomerErrors.DocumentTypeInactive(docType.ToString());
        }

        var docResult = IdentityDocument.Create(docType, request.DocNumber, rule);
        if (docResult.IsFailure)
        {
            return docResult.Error;
        }

        var nameResult = PersonName.Create(request.FullName);
        if (nameResult.IsFailure)
        {
            return nameResult.Error;
        }

        var contactResult = ContactInfo.Create(request.Email, request.PhonePrimary, request.PhoneSecondary);
        if (contactResult.IsFailure)
        {
            return contactResult.Error;
        }

        var profileResult = SocioeconomicProfile.Create(
            request.MonthlyNetIncome,
            request.Occupation,
            request.EmployerName,
            request.WorkSeniorityMonths);
        if (profileResult.IsFailure)
        {
            return profileResult.Error;
        }

        if (await _customers.ExistsByDocumentAsync(docType, docResult.Value.Number, ct))
        {
            return CustomerErrors.DocumentAlreadyExists;
        }

        var consentsResult = await ResolveAcceptedConsentsAsync(request, ct);
        if (consentsResult.IsFailure)
        {
            return consentsResult.Error;
        }

        var actorId = _currentUser.UserId ?? Guid.Empty;
        var customerResult = personType == PersonType.NATURAL
            ? Customer.RegisterNatural(
                docResult.Value,
                nameResult.Value,
                request.BirthDate,
                contactResult.Value,
                request.Address,
                request.District,
                profileResult.Value,
                actorId,
                _clock)
            : Customer.RegisterLegalEntity(
                docResult.Value,
                nameResult.Value,
                request.BirthDate,
                contactResult.Value,
                request.Address,
                request.District,
                profileResult.Value,
                actorId,
                _clock);

        if (customerResult.IsFailure)
        {
            return customerResult.Error;
        }

        var customer = customerResult.Value;
        var channel = _currentUser.IsInRole(SecurityPolicies.Client) &&
                      !CustomerMapper.CanViewUnmaskedPii(_currentUser)
            ? "PORTAL"
            : "BACKOFFICE";

        try
        {
            await using var tx = await _unitOfWork.BeginTransactionAsync(ct);

            _audit.SetAction("CREATE", "customers");
            _customers.Add(customer);

            foreach (var text in consentsResult.Value)
            {
                _customerConsents.Add(CustomerConsent.Grant(
                    customer.Id,
                    text,
                    _clientInfo.IpAddress,
                    _clientInfo.UserAgent,
                    channel,
                    _clock));
            }

            _outbox.Enqueue(new CustomerRegisteredIntegrationEvent(customer.Id, customer.PersonType.ToString()));

            await _unitOfWork.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DuplicateRecordException)
        {
            return CustomerErrors.DocumentAlreadyExists;
        }

        return CustomerMapper.ToResponse(customer, _currentUser);
    }

    private async Task<Result<IReadOnlyList<ConsentText>>> ResolveAcceptedConsentsAsync(
        RegisterCustomerRequest request,
        CancellationToken ct)
    {
        if (request.Consents is null || request.Consents.Count == 0)
        {
            return CustomerErrors.ConsentRequired(ConsentPurpose.DATA_PROCESSING.ToString());
        }

        var currentTexts = await _consentTexts.GetCurrentAllAsync(ct);
        var byPurpose = currentTexts.ToDictionary(t => t.Purpose);

        var resolved = new Dictionary<ConsentPurpose, ConsentText>();
        foreach (var item in request.Consents)
        {
            if (!Enum.TryParse<ConsentPurpose>(item.Purpose?.Trim(), ignoreCase: true, out var purpose))
            {
                return CustomerErrors.Validation($"Invalid consent purpose '{item.Purpose}'.");
            }

            if (!byPurpose.TryGetValue(purpose, out var current))
            {
                return CustomerErrors.ConsentVersionOutdated(purpose.ToString(), "<unavailable>");
            }

            if (!string.Equals(current.Version, item.Version?.Trim(), StringComparison.Ordinal))
            {
                return CustomerErrors.ConsentVersionOutdated(purpose.ToString(), current.Version);
            }

            resolved[purpose] = current;
        }

        if (!resolved.ContainsKey(ConsentPurpose.DATA_PROCESSING))
        {
            return CustomerErrors.ConsentRequired(ConsentPurpose.DATA_PROCESSING.ToString());
        }

        return resolved.Values.ToArray();
    }
}

internal sealed class GetCustomerUseCase : IGetCustomerUseCase
{
    private readonly ICustomerRepository _customers;
    private readonly ICurrentUser _currentUser;

    public GetCustomerUseCase(ICustomerRepository customers, ICurrentUser currentUser)
    {
        _customers = customers;
        _currentUser = currentUser;
    }

    public async Task<Result<CustomerResponse>> ExecuteAsync(Guid customerId, CancellationToken ct = default)
    {
        if (!CustomerMapper.HasCustomerAccess(
                _currentUser,
                customerId,
                SecurityPolicies.Admin,
                SecurityPolicies.Analyst,
                SecurityPolicies.Operator,
                SecurityPolicies.Collector,
                SecurityPolicies.Auditor))
        {
            return CustomerErrors.NotFound(customerId);
        }

        var customer = await _customers.FindByIdAsync(customerId, ct);
        if (customer is null)
        {
            return CustomerErrors.NotFound(customerId);
        }

        return CustomerMapper.ToResponse(customer, _currentUser);
    }
}

internal sealed class SearchCustomersUseCase : ISearchCustomersUseCase
{
    private const int MaxPageSize = 100;
    private static readonly TimeSpan LimaOffset = TimeSpanUtils.Hours(-5);

    private readonly ICustomerRepository _customers;
    private readonly ICurrentUser _currentUser;

    public SearchCustomersUseCase(ICustomerRepository customers, ICurrentUser currentUser)
    {
        _customers = customers;
        _currentUser = currentUser;
    }

    public async Task<Result<PagedResult<CustomerListItem>>> ExecuteAsync(
        SearchCustomersRequest request,
        CancellationToken ct = default)
    {
        if (request.Page < 1)
        {
            return CustomerErrors.Validation("Page must be greater than or equal to 1.");
        }

        if (request.PageSize < 1 || request.PageSize > MaxPageSize)
        {
            return CustomerErrors.Validation($"PageSize must be between 1 and {MaxPageSize}.");
        }

        KycStatus? kycStatus = null;
        if (!string.IsNullOrWhiteSpace(request.KycStatus))
        {
            if (!Enum.TryParse<KycStatus>(request.KycStatus.Trim(), ignoreCase: true, out var parsedStatus))
            {
                return CustomerErrors.Validation($"Invalid kycStatus '{request.KycStatus}'.");
            }
            kycStatus = parsedStatus;
        }

        PersonType? personType = null;
        if (!string.IsNullOrWhiteSpace(request.PersonType))
        {
            if (!Enum.TryParse<PersonType>(request.PersonType.Trim(), ignoreCase: true, out var parsedType))
            {
                return CustomerErrors.Validation($"Invalid personType '{request.PersonType}'.");
            }
            personType = parsedType;
        }

        if (request.CreatedFrom.HasValue && request.CreatedTo.HasValue && request.CreatedFrom > request.CreatedTo)
        {
            return CustomerErrors.Validation("createdFrom must be on or before createdTo.");
        }

        DateTimeOffset? createdFromUtc = request.CreatedFrom.HasValue
            ? new DateTimeOffset(request.CreatedFrom.Value.ToDateTime(TimeOnly.MinValue), LimaOffset).ToUniversalTime()
            : null;

        DateTimeOffset? createdToUtc = request.CreatedTo.HasValue
            ? new DateTimeOffset(request.CreatedTo.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), LimaOffset).ToUniversalTime()
            : null;

        var pageResult = await _customers.SearchAsync(
            new CustomerSearchFilter(
                request.Q?.Trim(),
                kycStatus,
                personType,
                createdFromUtc,
                createdToUtc,
                request.Page,
                request.PageSize),
            ct);

        var mapped = pageResult.Items
            .Select(c => CustomerMapper.ToListItem(c, _currentUser))
            .ToArray();

        return new PagedResult<CustomerListItem>(mapped, pageResult.Total, pageResult.Page, pageResult.PageSize);
    }
}

internal sealed class UpdateCustomerUseCase : IUpdateCustomerUseCase
{
    private readonly ICustomerRepository _customers;
    private readonly ICustomersUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IAuditContext _audit;

    public UpdateCustomerUseCase(
        ICustomerRepository customers,
        ICustomersUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
        IAuditContext audit)
    {
        _customers = customers;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _clock = clock;
        _audit = audit;
    }

    public async Task<Result<CustomerResponse>> ExecuteAsync(UpdateCustomerCommand command, CancellationToken ct = default)
    {
        var customer = await _customers.FindByIdAsync(command.CustomerId, ct);
        if (customer is null)
        {
            return CustomerErrors.NotFound(command.CustomerId);
        }

        var expectedVersion = command.ExpectedVersion ?? command.Request.ExpectedVersion;
        if (expectedVersion.HasValue && customer.Version != expectedVersion.Value)
        {
            return CustomerErrors.ConcurrencyConflict;
        }

        var criticalChanges = new List<string>();

        if (command.Request.Contact is { } contactPatch)
        {
            var nextContact = ContactInfo.Create(
                contactPatch.Email ?? customer.Email,
                contactPatch.PhonePrimary ?? customer.PhonePrimary,
                contactPatch.PhoneSecondary ?? customer.PhoneSecondary);

            if (nextContact.IsFailure)
            {
                return nextContact.Error;
            }

            criticalChanges.AddRange(customer.UpdateContact(nextContact.Value, _clock));
        }

        if (command.Request.Address is { } addressPatch)
        {
            var addressResult = customer.UpdateAddress(
                addressPatch.Address ?? customer.Address,
                addressPatch.District ?? customer.District,
                _clock);

            if (addressResult.IsFailure)
            {
                return addressResult.Error;
            }

            criticalChanges.AddRange(addressResult.Value);
        }

        if (command.Request.Profile is { } profilePatch)
        {
            var nextProfile = SocioeconomicProfile.Create(
                profilePatch.MonthlyNetIncome ?? customer.MonthlyNetIncome.Amount,
                profilePatch.Occupation ?? customer.Occupation,
                profilePatch.EmployerName ?? customer.EmployerName,
                profilePatch.WorkSeniorityMonths ?? customer.WorkSeniorityMonths);

            if (nextProfile.IsFailure)
            {
                return nextProfile.Error;
            }

            criticalChanges.AddRange(customer.UpdateSocioeconomicProfile(nextProfile.Value, _clock));
        }

        _audit.SetAction(criticalChanges.Count > 0 ? "UPDATE_CRITICAL" : "UPDATE", "customers");

        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (ConcurrencyConflictException)
        {
            return CustomerErrors.ConcurrencyConflict;
        }

        return CustomerMapper.ToResponse(customer, _currentUser);
    }
}

internal sealed class GetCustomerHistoryUseCase : IGetCustomerHistoryUseCase
{
    private const int MaxPageSize = 100;

    private readonly ICustomerRepository _customers;
    private readonly ICustomerHistoryReader _historyReader;

    public GetCustomerHistoryUseCase(ICustomerRepository customers, ICustomerHistoryReader historyReader)
    {
        _customers = customers;
        _historyReader = historyReader;
    }

    public async Task<Result<PagedResult<CustomerChangeEntry>>> ExecuteAsync(
        GetCustomerHistoryRequest request,
        CancellationToken ct = default)
    {
        if (request.Page < 1)
        {
            return CustomerErrors.Validation("Page must be greater than or equal to 1.");
        }

        if (request.PageSize < 1 || request.PageSize > MaxPageSize)
        {
            return CustomerErrors.Validation($"PageSize must be between 1 and {MaxPageSize}.");
        }

        if (await _customers.FindByIdAsync(request.CustomerId, ct) is null)
        {
            return CustomerErrors.NotFound(request.CustomerId);
        }

        return await _historyReader.GetHistoryAsync(request.CustomerId, request.Page, request.PageSize, ct);
    }
}
