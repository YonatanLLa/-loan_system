using LoanSystem.Common.Core.Application;
using LoanSystem.Common.Core.Ports.Outbound;
using LoanSystem.Modules.Customers.Core.Contracts;
using LoanSystem.Modules.Customers.Core.Contracts.IntegrationEvents;
using LoanSystem.Modules.Customers.Core.Domain;
using LoanSystem.Modules.Customers.Core.Domain.Validators;
using LoanSystem.Modules.Customers.Core.Ports.Inbound;
using LoanSystem.Modules.Customers.Core.Ports.Outbound;

namespace LoanSystem.Modules.Customers.Core.Application;

internal sealed class RegisterBankAccountUseCase : IRegisterBankAccountUseCase
{
    private readonly ICustomerRepository _customers;
    private readonly ICustomerBankAccountRepository _accounts;
    private readonly ICciValidator _cciValidator;
    private readonly IFieldProtector _protector;
    private readonly ICustomersUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IAuditContext _audit;
    private readonly CustomersOptions _options;

    public RegisterBankAccountUseCase(
        ICustomerRepository customers,
        ICustomerBankAccountRepository accounts,
        ICciValidator cciValidator,
        IFieldProtector protector,
        ICustomersUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
        IAuditContext audit,
        CustomersOptions options)
    {
        _customers = customers;
        _accounts = accounts;
        _cciValidator = cciValidator;
        _protector = protector;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _clock = clock;
        _audit = audit;
        _options = options;
    }

    public async Task<Result<BankAccountResponse>> ExecuteAsync(
        RegisterBankAccountCommand command,
        CancellationToken ct = default)
    {
        if (!CustomerMapper.HasCustomerAccess(
                _currentUser,
                command.CustomerId,
                SecurityPolicies.Admin,
                SecurityPolicies.Operator))
        {
            return CustomerErrors.NotFound(command.CustomerId);
        }

        if (!Enum.TryParse<BankAccountType>(command.Request.AccountType?.Trim(), ignoreCase: true, out var accountType))
        {
            return CustomerErrors.Validation($"Invalid accountType '{command.Request.AccountType}'.");
        }

        if (await _customers.FindByIdAsync(command.CustomerId, ct) is null)
        {
            return CustomerErrors.NotFound(command.CustomerId);
        }

        var existingCount = await _accounts.CountNonInactiveAsync(command.CustomerId, ct);
        if (existingCount >= _options.BankAccounts.MaxActivePerCustomer)
        {
            return CustomerErrors.BankAccountLimitReached(_options.BankAccounts.MaxActivePerCustomer);
        }

        var accountResult = CustomerBankAccount.Register(
            command.CustomerId,
            accountType,
            command.Request.BankName,
            command.Request.Cci,
            command.Request.PhoneWallet,
            command.Request.GatewayToken,
            command.Request.HolderName,
            _cciValidator,
            _protector,
            _clock);

        if (accountResult.IsFailure)
        {
            return accountResult.Error;
        }

        var account = accountResult.Value;
        if (!string.IsNullOrEmpty(account.AccountNumberHash) &&
            await _accounts.ExistsActiveByHashAsync(command.CustomerId, account.AccountNumberHash, ct))
        {
            return CustomerErrors.BankAccountAlreadyExists;
        }

        try
        {
            _audit.SetAction("CREATE", "customer_bank_accounts");
            _accounts.Add(account);
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (DuplicateRecordException)
        {
            return CustomerErrors.BankAccountAlreadyExists;
        }

        return CustomerMapper.ToBankAccountResponse(account);
    }
}

internal sealed class ListBankAccountsUseCase : IListBankAccountsUseCase
{
    private readonly ICustomerRepository _customers;
    private readonly ICustomerBankAccountRepository _accounts;
    private readonly ICurrentUser _currentUser;

    public ListBankAccountsUseCase(
        ICustomerRepository customers,
        ICustomerBankAccountRepository accounts,
        ICurrentUser currentUser)
    {
        _customers = customers;
        _accounts = accounts;
        _currentUser = currentUser;
    }

    public async Task<Result<IReadOnlyList<BankAccountResponse>>> ExecuteAsync(
        Guid customerId,
        CancellationToken ct = default)
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

        if (await _customers.FindByIdAsync(customerId, ct) is null)
        {
            return CustomerErrors.NotFound(customerId);
        }

        var list = await _accounts.ListByCustomerAsync(customerId, ct);
        return list.Select(CustomerMapper.ToBankAccountResponse).ToArray();
    }
}

internal sealed class VerifyBankAccountUseCase : IVerifyBankAccountUseCase
{
    private readonly ICustomerRepository _customers;
    private readonly ICustomerBankAccountRepository _accounts;
    private readonly ICustomersUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IAuditContext _audit;

    public VerifyBankAccountUseCase(
        ICustomerRepository customers,
        ICustomerBankAccountRepository accounts,
        ICustomersUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
        IAuditContext audit)
    {
        _customers = customers;
        _accounts = accounts;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _clock = clock;
        _audit = audit;
    }

    public async Task<Result<BankAccountResponse>> ExecuteAsync(
        VerifyBankAccountCommand command,
        CancellationToken ct = default)
    {
        if (_currentUser.CustomerId == command.CustomerId)
        {
            return Error.Forbidden(
                "Customers.SelfVerificationNotAllowed",
                "A customer portal user cannot verify ownership of their own bank account.");
        }

        var customer = await _customers.FindByIdAsync(command.CustomerId, ct);
        if (customer is null)
        {
            return CustomerErrors.NotFound(command.CustomerId);
        }

        var account = await _accounts.FindByIdAsync(command.CustomerId, command.AccountId, ct);
        if (account is null)
        {
            return CustomerErrors.BankAccountNotFound(command.AccountId);
        }

        var verifyResult = account.VerifyOwnership(
            _currentUser.UserId ?? Guid.Empty,
            command.Request.HolderName,
            customer.FullName,
            command.Request.Method,
            command.Request.Notes,
            _clock);

        if (verifyResult.IsFailure)
        {
            return verifyResult.Error;
        }

        _audit.SetAction("VERIFY_ACCOUNT", "customer_bank_accounts");
        await _unitOfWork.SaveChangesAsync(ct);

        return CustomerMapper.ToBankAccountResponse(account);
    }
}

internal sealed class DeactivateBankAccountUseCase : IDeactivateBankAccountUseCase
{
    private readonly ICustomerRepository _customers;
    private readonly ICustomerBankAccountRepository _accounts;
    private readonly ICustomersUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IAuditContext _audit;

    public DeactivateBankAccountUseCase(
        ICustomerRepository customers,
        ICustomerBankAccountRepository accounts,
        ICustomersUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
        IAuditContext audit)
    {
        _customers = customers;
        _accounts = accounts;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _clock = clock;
        _audit = audit;
    }

    public async Task<Result> ExecuteAsync(DeactivateBankAccountCommand command, CancellationToken ct = default)
    {
        if (!CustomerMapper.HasCustomerAccess(
                _currentUser,
                command.CustomerId,
                SecurityPolicies.Admin,
                SecurityPolicies.Operator))
        {
            return CustomerErrors.NotFound(command.CustomerId);
        }

        if (await _customers.FindByIdAsync(command.CustomerId, ct) is null)
        {
            return CustomerErrors.NotFound(command.CustomerId);
        }

        var account = await _accounts.FindByIdAsync(command.CustomerId, command.AccountId, ct);
        if (account is null)
        {
            return CustomerErrors.BankAccountNotFound(command.AccountId);
        }

        _audit.SetAction("DEACTIVATE_ACCOUNT", "customer_bank_accounts");
        account.Deactivate(_clock);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }
}

internal sealed class SetPrimaryBankAccountUseCase : ISetPrimaryBankAccountUseCase
{
    private readonly ICustomerRepository _customers;
    private readonly ICustomerBankAccountRepository _accounts;
    private readonly ICustomerOtpService _otpService;
    private readonly ICustomersUnitOfWork _unitOfWork;
    private readonly ICustomerAuditWriter _auditWriter;
    private readonly ICurrentUser _currentUser;
    private readonly IClientInfoProvider _clientInfo;
    private readonly IClock _clock;
    private readonly IAuditContext _audit;

    public SetPrimaryBankAccountUseCase(
        ICustomerRepository customers,
        ICustomerBankAccountRepository accounts,
        ICustomerOtpService otpService,
        ICustomersUnitOfWork unitOfWork,
        ICustomerAuditWriter auditWriter,
        ICurrentUser currentUser,
        IClientInfoProvider clientInfo,
        IClock clock,
        IAuditContext audit)
    {
        _customers = customers;
        _accounts = accounts;
        _otpService = otpService;
        _unitOfWork = unitOfWork;
        _auditWriter = auditWriter;
        _currentUser = currentUser;
        _clientInfo = clientInfo;
        _clock = clock;
        _audit = audit;
    }

    public async Task<Result<SetPrimaryBankAccountResult>> ExecuteAsync(
        SetPrimaryBankAccountCommand command,
        CancellationToken ct = default)
    {
        if (!CustomerMapper.HasCustomerAccess(
                _currentUser,
                command.CustomerId,
                SecurityPolicies.Admin,
                SecurityPolicies.Operator))
        {
            return CustomerErrors.NotFound(command.CustomerId);
        }

        if (await _customers.FindByIdAsync(command.CustomerId, ct) is null)
        {
            return CustomerErrors.NotFound(command.CustomerId);
        }

        var target = await _accounts.FindByIdAsync(command.CustomerId, command.AccountId, ct);
        if (target is null)
        {
            return CustomerErrors.BankAccountNotFound(command.AccountId);
        }

        if (target.Status != BankAccountStatus.ACTIVE || !target.IsVerified)
        {
            return CustomerErrors.AccountNotVerified;
        }

        var existingPrimary = await _accounts.FindPrimaryActiveAsync(command.CustomerId, ct);
        if (existingPrimary is null || existingPrimary.Id == target.Id)
        {
            var firstResult = target.SetAsFirstPrimary(_clock);
            if (firstResult.IsFailure)
            {
                return firstResult.Error;
            }

            _audit.SetAction("SET_PRIMARY_ACCOUNT", "customer_bank_accounts");
            await _unitOfWork.SaveChangesAsync(ct);

            return new SetPrimaryBankAccountResult(
                RequiresOtp: false,
                Account: CustomerMapper.ToBankAccountResponse(target),
                OtpChallenge: null);
        }

        var otpResult = await _otpService.IssueAsync(
            command.CustomerId,
            OtpPurpose.ACCOUNT_CHANGE.ToString(),
            referenceId: target.Id,
            preferredChannel: null,
            ct);

        if (otpResult.IsFailure)
        {
            return otpResult.Error;
        }

        await _auditWriter.WriteAsync(
            "customer_bank_accounts",
            target.Id.ToString(),
            "PRIMARY_ACCOUNT_CHANGE_INITIATED",
            _currentUser.UserId,
            _clientInfo.IpAddress,
            _clientInfo.UserAgent,
            $"{{\"customerId\":\"{command.CustomerId}\",\"initiatedBy\":\"{_currentUser.UserId}\"}}",
            ct);

        return new SetPrimaryBankAccountResult(
            RequiresOtp: true,
            Account: null,
            OtpChallenge: otpResult.Value);
    }
}

internal sealed class ConfirmPrimaryBankAccountUseCase : IConfirmPrimaryBankAccountUseCase
{
    private readonly ICustomerRepository _customers;
    private readonly ICustomerBankAccountRepository _accounts;
    private readonly ICustomerOtpService _otpService;
    private readonly ICustomersUnitOfWork _unitOfWork;
    private readonly ICustomersOutbox _outbox;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IAuditContext _audit;
    private readonly CustomersOptions _options;

    public ConfirmPrimaryBankAccountUseCase(
        ICustomerRepository customers,
        ICustomerBankAccountRepository accounts,
        ICustomerOtpService otpService,
        ICustomersUnitOfWork unitOfWork,
        ICustomersOutbox outbox,
        ICurrentUser currentUser,
        IClock clock,
        IAuditContext audit,
        CustomersOptions options)
    {
        _customers = customers;
        _accounts = accounts;
        _otpService = otpService;
        _unitOfWork = unitOfWork;
        _outbox = outbox;
        _currentUser = currentUser;
        _clock = clock;
        _audit = audit;
        _options = options;
    }

    public async Task<Result<BankAccountResponse>> ExecuteAsync(
        ConfirmPrimaryBankAccountCommand command,
        CancellationToken ct = default)
    {
        if (!CustomerMapper.HasCustomerAccess(
                _currentUser,
                command.CustomerId,
                SecurityPolicies.Admin,
                SecurityPolicies.Operator))
        {
            return CustomerErrors.NotFound(command.CustomerId);
        }

        if (await _customers.FindByIdAsync(command.CustomerId, ct) is null)
        {
            return CustomerErrors.NotFound(command.CustomerId);
        }

        var target = await _accounts.FindByIdAsync(command.CustomerId, command.AccountId, ct);
        if (target is null)
        {
            return CustomerErrors.BankAccountNotFound(command.AccountId);
        }

        if (target.Status != BankAccountStatus.ACTIVE || !target.IsVerified)
        {
            return CustomerErrors.AccountNotVerified;
        }

        var verifyOtp = await _otpService.VerifyAsync(
            command.CustomerId,
            command.OtpRequestId,
            command.Code,
            OtpPurpose.ACCOUNT_CHANGE.ToString(),
            referenceId: target.Id,
            ct);

        if (verifyOtp.IsFailure)
        {
            return verifyOtp.Error;
        }

        var existingPrimary = await _accounts.FindPrimaryActiveAsync(command.CustomerId, ct);
        var oldLast4 = existingPrimary?.Last4 ?? string.Empty;

        try
        {
            await using var tx = await _unitOfWork.BeginTransactionAsync(ct);

            if (existingPrimary is not null && existingPrimary.Id != target.Id)
            {
                existingPrimary.ClearPrimary(_clock);
            }

            var cooling = TimeSpanUtils.Hours(_options.BankAccounts.CoolingPeriodHours);
            var promoteResult = target.PromoteToPrimaryWithCooling(cooling, _clock);
            if (promoteResult.IsFailure)
            {
                return promoteResult.Error;
            }

            _audit.SetAction("CHANGE_PRIMARY_ACCOUNT", "customer_bank_accounts");
            _outbox.Enqueue(new CustomerDisbursementAccountChangedIntegrationEvent(
                command.CustomerId,
                oldLast4,
                target.Last4,
                target.DisbursementEnabledFrom!.Value));

            await _unitOfWork.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DuplicateRecordException)
        {
            return CustomerErrors.ConcurrencyConflict;
        }
        catch (ConcurrencyConflictException)
        {
            return CustomerErrors.ConcurrencyConflict;
        }

        return CustomerMapper.ToBankAccountResponse(target);
    }
}

internal sealed class CustomerDisbursementEligibilityService : ICustomerDisbursementEligibility
{
    private readonly ICustomerRepository _customers;
    private readonly IBankAccountExistenceLookup _accountLookup;
    private readonly IClock _clock;

    public CustomerDisbursementEligibilityService(
        ICustomerRepository customers,
        IBankAccountExistenceLookup accountLookup,
        IClock clock)
    {
        _customers = customers;
        _accountLookup = accountLookup;
        _clock = clock;
    }

    public async Task<DisbursementEligibility> CheckAsync(
        Guid customerId,
        Guid bankAccountId,
        CancellationToken ct = default)
    {
        var reasons = new List<string>();
        var now = _clock.UtcNow;

        var customer = await _customers.FindByIdAsync(customerId, ct);
        if (customer is null)
        {
            reasons.Add(DisbursementEligibilityReasons.KycNotVerified);
        }
        else if (customer.KycStatus != KycStatus.VERIFIED)
        {
            reasons.Add(customer.KycStatus == KycStatus.EXPIRED
                ? DisbursementEligibilityReasons.KycExpired
                : DisbursementEligibilityReasons.KycNotVerified);
        }
        else if (customer.IsKycEffectivelyExpired(now))
        {
            reasons.Add(DisbursementEligibilityReasons.KycExpired);
        }

        var (exists, account) = await _accountLookup.FindGlobalByIdAsync(bankAccountId, ct);
        if (!exists || account is null)
        {
            reasons.Add(DisbursementEligibilityReasons.AccountNotFound);
        }
        else if (account.CustomerId != customerId)
        {
            reasons.Add(DisbursementEligibilityReasons.AccountNotOwned);
        }
        else
        {
            if (account.Status != BankAccountStatus.ACTIVE)
            {
                reasons.Add(DisbursementEligibilityReasons.AccountInactive);
            }

            if (!account.IsVerified)
            {
                reasons.Add(DisbursementEligibilityReasons.AccountNotVerified);
            }

            if (account.DisbursementEnabledFrom.HasValue && account.DisbursementEnabledFrom.Value > now)
            {
                reasons.Add(DisbursementEligibilityReasons.AccountInCoolingPeriod);
            }
        }

        return new DisbursementEligibility(reasons.Count == 0, reasons);
    }
}
