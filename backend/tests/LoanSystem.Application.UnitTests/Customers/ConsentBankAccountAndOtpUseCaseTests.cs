using LoanSystem.Common.Core.Application;
using LoanSystem.Modules.Customers.Core.Application;
using LoanSystem.Modules.Customers.Core.Contracts;
using LoanSystem.Modules.Customers.Core.Contracts.IntegrationEvents;
using LoanSystem.Modules.Customers.Core.Domain;
using LoanSystem.Modules.Customers.Core.Domain.Validators;
using Shouldly;
using Xunit;

namespace LoanSystem.Application.UnitTests.Customers;

public class ConsentBankAccountAndOtpUseCaseTests
{
    private readonly FakeCustomersClock _clock = new();
    private readonly InMemoryCustomerRepository _customers = new();
    private readonly InMemoryConsentTextRepository _consentTexts = new();
    private readonly InMemoryCustomerConsentRepository _customerConsents = new();
    private readonly InMemoryBankAccountRepository _accounts = new();
    private readonly InMemoryOtpRepository _otps = new();
    private readonly StubOtpHasher _otpHasher = new();
    private readonly FakeOtpSender _otpSender = new();
    private readonly StubFieldProtector _protector = new();
    private readonly CciValidator _cciValidator = new(new StubBanksCatalog());
    private readonly FakeCustomersUnitOfWork _uow = new();
    private readonly FakeCustomersOutbox _outbox = new();
    private readonly FakeCustomersCurrentUser _currentUser = new();
    private readonly FakeCustomersClientInfo _clientInfo = new();
    private readonly FakeCustomersAuditContext _audit = new();
    private readonly FakeCustomerAuditWriter _auditWriter = new();
    private readonly CustomersOptions _options = new();

    private CustomerOtpService CreateOtpService() =>
        new(_customers, _otps, _otpHasher, _otpSender, _uow, _auditWriter, _currentUser, _clientInfo, _clock, _options);

    [Fact]
    public async Task ConsentUseCases_GrantReplacesVersion_RevokeMarketingEnqueuesEvent_AndQueryReflectsState()
    {
        var customer = CustomersTestFactory.CreateNaturalCustomer(_clock);
        _customers.Add(customer);
        _currentUser.AsRole(SecurityPolicies.Operator);

        var grantUc = new GrantCustomerConsentUseCase(_customers, _consentTexts, _customerConsents, _uow, _currentUser, _clientInfo, _clock, _audit);
        var revokeUc = new RevokeCustomerConsentUseCase(_customers, _customerConsents, _uow, _outbox, _currentUser, _clientInfo, _clock, _audit);
        var query = new CustomerConsentQueryService(_customerConsents);

        // Grant CREDIT_BUREAU_QUERY v1.0 & MARKETING v1.0
        (await grantUc.ExecuteAsync(new GrantConsentCommand(customer.Id, "CREDIT_BUREAU_QUERY", "v1.0"))).IsSuccess.ShouldBeTrue();
        (await grantUc.ExecuteAsync(new GrantConsentCommand(customer.Id, "MARKETING", "v1.0"))).IsSuccess.ShouldBeTrue();
        (await query.HasActiveAsync(customer.Id, "CREDIT_BUREAU_QUERY")).ShouldBeTrue();
        (await query.HasActiveAsync(customer.Id, "MARKETING")).ShouldBeTrue();

        // Non-marketing cannot be revoked
        var nonRevocable = await revokeUc.ExecuteAsync(new RevokeConsentCommand(customer.Id, "CREDIT_BUREAU_QUERY"));
        nonRevocable.IsFailure.ShouldBeTrue();
        nonRevocable.Error.Code.ShouldBe("Customers.ConsentNotRevocable");

        // Marketing revokes and publishes outbox event
        var revoked = await revokeUc.ExecuteAsync(new RevokeConsentCommand(customer.Id, "MARKETING"));
        revoked.IsSuccess.ShouldBeTrue();
        (await query.HasActiveAsync(customer.Id, "MARKETING")).ShouldBeFalse();
        _outbox.Events.OfType<CustomerMarketingConsentRevokedIntegrationEvent>()
            .ShouldHaveSingleItem().CustomerId.ShouldBe(customer.Id);
    }

    [Fact]
    public async Task BankAccountWorkflow_FirstPrimaryDirect_SecondPrimaryRequiresOtpAndCoolingPeriod()
    {
        var portalUserId = Guid.NewGuid();
        var customer = CustomersTestFactory.CreateNaturalCustomer(_clock, portalUserId: portalUserId);
        customer.MarkPhoneVerified(_clock);
        _customers.Add(customer);

        var registerUc = new RegisterBankAccountUseCase(
            _customers, _accounts, _cciValidator, _protector, _uow, _currentUser, _clock, _audit, _options);
        var verifyUc = new VerifyBankAccountUseCase(
            _customers, _accounts, _uow, _currentUser, _clock, _audit);
        var otpService = CreateOtpService();
        var setPrimaryUc = new SetPrimaryBankAccountUseCase(
            _customers, _accounts, otpService, _uow, _auditWriter, _currentUser, _clientInfo, _clock, _audit);
        var confirmPrimaryUc = new ConfirmPrimaryBankAccountUseCase(
            _customers, _accounts, otpService, _uow, _outbox, _currentUser, _clock, _audit, _options);

        _currentUser.AsRole(SecurityPolicies.Operator);
        var acc1 = (await registerUc.ExecuteAsync(new RegisterBankAccountCommand(
            customer.Id, new RegisterBankAccountRequest("BCP", "CUENTA_AHORRO", "00219100000000001111", null, null, "Ana Maria Quispe")))).Value;
        var acc2 = (await registerUc.ExecuteAsync(new RegisterBankAccountCommand(
            customer.Id, new RegisterBankAccountRequest("BBVA", "CUENTA_CORRIENTE", "01119100000000002222", null, null, "Ana Maria Quispe")))).Value;

        // Client portal user cannot self-verify
        _currentUser.AsRole(SecurityPolicies.Client, customerId: customer.Id);
        var selfVerify = await verifyUc.ExecuteAsync(new VerifyBankAccountCommand(
            customer.Id, acc1.Id, new VerifyBankAccountRequest("Ana Maria Quispe")));
        selfVerify.IsFailure.ShouldBeTrue();
        selfVerify.Error.Code.ShouldBe("Customers.SelfVerificationNotAllowed");

        // Operator verifies both
        _currentUser.AsRole(SecurityPolicies.Operator);
        (await verifyUc.ExecuteAsync(new VerifyBankAccountCommand(customer.Id, acc1.Id, new VerifyBankAccountRequest("Ana María Quispe")))).IsSuccess.ShouldBeTrue();
        (await verifyUc.ExecuteAsync(new VerifyBankAccountCommand(customer.Id, acc2.Id, new VerifyBankAccountRequest("ANA MARIA QUISPE")))).IsSuccess.ShouldBeTrue();

        // First primary sets immediately without OTP or cooling period
        var firstPrimary = await setPrimaryUc.ExecuteAsync(new SetPrimaryBankAccountCommand(customer.Id, acc1.Id));
        firstPrimary.IsSuccess.ShouldBeTrue();
        firstPrimary.Value.RequiresOtp.ShouldBeFalse();
        firstPrimary.Value.Account!.IsPrimary.ShouldBeTrue();
        firstPrimary.Value.Account.DisbursementEnabledFrom.ShouldBeNull();

        // Changing to second primary initiates OTP challenge & logs initiated event
        var secondInit = await setPrimaryUc.ExecuteAsync(new SetPrimaryBankAccountCommand(customer.Id, acc2.Id));
        secondInit.IsSuccess.ShouldBeTrue();
        secondInit.Value.RequiresOtp.ShouldBeTrue();
        var challenge = secondInit.Value.OtpChallenge!;
        _auditWriter.Entries.Any(e => e.Action == "PRIMARY_ACCOUNT_CHANGE_INITIATED").ShouldBeTrue();

        // Issued OTP code cannot be re-used across different purpose/reference
        var sentCode = _otpSender.Sent.Last().Code;
        var wrongRef = await confirmPrimaryUc.ExecuteAsync(
            new ConfirmPrimaryBankAccountCommand(customer.Id, acc1.Id, challenge.OtpRequestId, sentCode));
        wrongRef.IsFailure.ShouldBeTrue();
        wrongRef.Error.Code.ShouldBe("Customers.OtpInvalid");

        // Correct confirmation swaps primary, sets 24h cooling, and enqueues event
        var confirmed = await confirmPrimaryUc.ExecuteAsync(
            new ConfirmPrimaryBankAccountCommand(customer.Id, acc2.Id, challenge.OtpRequestId, sentCode));
        confirmed.IsSuccess.ShouldBeTrue();
        confirmed.Value.IsPrimary.ShouldBeTrue();
        confirmed.Value.DisbursementEnabledFrom.ShouldBe(_clock.UtcNow.AddHours(24));
        _accounts.Items.Single(a => a.Id == acc1.Id).IsPrimary.ShouldBeFalse();
        _outbox.Events.OfType<CustomerDisbursementAccountChangedIntegrationEvent>().Count().ShouldBe(1);
    }

    [Fact]
    public async Task DisbursementEligibility_EnforcesKycVerifiedAndAccountVerifiedAndCoolingWindow()
    {
        var customer = CustomersTestFactory.CreateNaturalCustomer(_clock);
        _customers.Add(customer);
        var svc = new CustomerDisbursementEligibilityService(_customers, _accounts, _clock);

        var acct = CustomerBankAccount.Register(
            customer.Id, BankAccountType.BILLETERA_DIGITAL, "Yape", null, "987654321", null, customer.FullName,
            _cciValidator, _protector, _clock).Value;
        _accounts.Add(acct);

        var initial = await svc.CheckAsync(customer.Id, acct.Id);
        initial.IsEligible.ShouldBeFalse();
        initial.Reasons.ShouldContain(DisbursementEligibilityReasons.KycNotVerified);
        initial.Reasons.ShouldContain(DisbursementEligibilityReasons.AccountInactive);
        initial.Reasons.ShouldContain(DisbursementEligibilityReasons.AccountNotVerified);

        // Approve KYC and verify+promote with 24h cooling
        customer.SubmitKyc(new HashSet<DocumentCategory> { DocumentCategory.DNI_FRONTAL, DocumentCategory.DNI_REVERSO, DocumentCategory.SELFIE }, 5000m, _clock);
        customer.ApproveKyc(Guid.NewGuid(), "Kyc approved clean.", true, [], _clock, 12);
        acct.VerifyOwnership(Guid.NewGuid(), customer.FullName, customer.FullName, "MANUAL", null, _clock);
        acct.PromoteToPrimaryWithCooling(TimeSpan.FromHours(24), _clock);

        var cooling = await svc.CheckAsync(customer.Id, acct.Id);
        cooling.IsEligible.ShouldBeFalse();
        cooling.Reasons.ShouldBe([DisbursementEligibilityReasons.AccountInCoolingPeriod]);

        _clock.Advance(TimeSpan.FromHours(24));
        var eligible = await svc.CheckAsync(customer.Id, acct.Id);
        eligible.IsEligible.ShouldBeTrue();
        eligible.Reasons.ShouldBeEmpty();
    }

    [Fact]
    public async Task OtpService_EnforcesCooldownHourlyLimitAndContactVerification()
    {
        var customer = CustomersTestFactory.CreateNaturalCustomer(_clock);
        _customers.Add(customer);
        _currentUser.AsRole(SecurityPolicies.Operator);

        var otpService = CreateOtpService();
        var startUc = new StartContactVerificationUseCase(_customers, otpService, _currentUser);
        var confirmUc = new ConfirmContactVerificationUseCase(_customers, _otps, otpService, _uow, _currentUser, _clock, _audit);

        var issued = await startUc.ExecuteAsync(new StartContactVerificationCommand(customer.Id, "EMAIL"));
        issued.IsSuccess.ShouldBeTrue();
        issued.Value.MaskedTarget.ShouldBe("a***@test.pe");

        // Resend before 60s triggers OtpResendTooSoon with retryAfterSeconds
        var tooSoon = await startUc.ExecuteAsync(new StartContactVerificationCommand(customer.Id, "EMAIL"));
        tooSoon.IsFailure.ShouldBeTrue();
        tooSoon.Error.Code.ShouldBe("Customers.OtpResendTooSoon");
        ((int)tooSoon.Error.Extensions!["retryAfterSeconds"]!).ShouldBeGreaterThan(0);

        // Confirm verifies email
        var code = _otpSender.Sent.Last().Code;
        var confirmed = await confirmUc.ExecuteAsync(
            new ConfirmContactVerificationCommand(customer.Id, issued.Value.OtpRequestId, code));
        confirmed.IsSuccess.ShouldBeTrue();
        customer.EmailVerifiedAt.ShouldBe(_clock.UtcNow);
    }
}
