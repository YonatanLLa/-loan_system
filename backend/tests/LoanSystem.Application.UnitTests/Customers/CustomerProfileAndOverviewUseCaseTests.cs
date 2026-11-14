using LoanSystem.Common.Core.Application;
using LoanSystem.Modules.Customers.Core.Application;
using LoanSystem.Modules.Customers.Core.Contracts;
using LoanSystem.Modules.Customers.Core.Contracts.Consents;
using LoanSystem.Modules.Customers.Core.Contracts.IntegrationEvents;
using LoanSystem.Modules.Customers.Core.Ports.Outbound;
using Shouldly;
using Xunit;

namespace LoanSystem.Application.UnitTests.Customers;

public class CustomerProfileAndOverviewUseCaseTests
{
    private readonly FakeCustomersClock _clock = new();
    private readonly InMemoryDocumentTypeCatalog _docTypes = new();
    private readonly InMemoryCustomerRepository _customers = new();
    private readonly InMemoryConsentTextRepository _consentTexts = new();
    private readonly InMemoryCustomerConsentRepository _customerConsents = new();
    private readonly FakeCustomersUnitOfWork _uow = new();
    private readonly FakeCustomersOutbox _outbox = new();
    private readonly FakeCustomersCurrentUser _currentUser = new();
    private readonly FakeCustomersClientInfo _clientInfo = new();
    private readonly FakeCustomersAuditContext _audit = new();
    private readonly FakeCustomerHistoryReader _historyReader = new();
    private readonly FakeCustomerPortfolioReader _portfolioReader = new();

    private RegisterCustomerUseCase CreateRegister() => new(
        _docTypes, _customers, _consentTexts, _customerConsents, _uow, _outbox, _currentUser, _clientInfo, _clock, _audit);

    private static RegisterCustomerRequest ValidRequest(string docNumber = "12345678", IReadOnlyList<ConsentAcceptance>? consents = null) =>
        new(
            "NATURAL",
            "DNI",
            docNumber,
            "Luis Alberto Torres",
            new DateOnly(1991, 8, 20),
            "Luis.Torres@Example.PE",
            "+51 987 654 321",
            null,
            "Av. Javier Prado 400",
            "San Isidro",
            4200m,
            "Contador",
            "Corporacion Sur",
            36,
            consents ?? [new ConsentAcceptance("DATA_PROCESSING", "v1.0")]);

    [Fact]
    public async Task RegisterCustomer_PersistsCustomerConsentsAndOutboxEventAtomically()
    {
        var result = await CreateRegister().ExecuteAsync(ValidRequest());

        result.IsSuccess.ShouldBeTrue();
        result.Value.DocNumber.ShouldBe("12345678");
        result.Value.FullName.ShouldBe("LUIS ALBERTO TORRES");
        _customers.Items.Count.ShouldBe(1);
        _customerConsents.Items.Count.ShouldBe(1);
        _customerConsents.Items[0].Channel.ShouldBe("BACKOFFICE");
        _uow.CommittedTransactions.ShouldBe(1);
        _audit.GetActionForEntity("customers").ShouldBe("CREATE");
        var evt = _outbox.Events.OfType<CustomerRegisteredIntegrationEvent>().ShouldHaveSingleItem();
        evt.CustomerId.ShouldBe(result.Value.Id);
    }

    [Fact]
    public async Task RegisterCustomer_RequiresDataProcessingConsentAndCurrentVersion()
    {
        var missingConsent = await CreateRegister().ExecuteAsync(
            ValidRequest(consents: [new ConsentAcceptance("MARKETING", "v1.0")]));
        missingConsent.IsFailure.ShouldBeTrue();
        missingConsent.Error.Code.ShouldBe("Customers.ConsentRequired");

        var staleConsent = await CreateRegister().ExecuteAsync(
            ValidRequest(consents: [new ConsentAcceptance("DATA_PROCESSING", "v0.9")]));
        staleConsent.IsFailure.ShouldBeTrue();
        staleConsent.Error.Code.ShouldBe("Customers.ConsentVersionOutdated");
    }

    [Fact]
    public async Task RegisterCustomer_DuplicatePrecheckAndDbRace_ReturnDocumentAlreadyExists()
    {
        (await CreateRegister().ExecuteAsync(ValidRequest("44556677"))).IsSuccess.ShouldBeTrue();

        var duplicatePrecheck = await CreateRegister().ExecuteAsync(ValidRequest("44556677"));
        duplicatePrecheck.IsFailure.ShouldBeTrue();
        duplicatePrecheck.Error.Code.ShouldBe("Customers.DocumentAlreadyExists");

        _uow.FailNextSaveWithDuplicate = true;
        var raceDuplicate = await CreateRegister().ExecuteAsync(ValidRequest("99887766"));
        raceDuplicate.IsFailure.ShouldBeTrue();
        raceDuplicate.Error.Code.ShouldBe("Customers.DocumentAlreadyExists");
    }

    [Fact]
    public async Task GetAndSearchCustomer_EnforcesPiiMaskingForAuditorCollectorAndOwnershipForClient()
    {
        var customer = CustomersTestFactory.CreateNaturalCustomer(_clock);
        _customers.Add(customer);

        _currentUser.AsRole(SecurityPolicies.Auditor);
        var getByAuditor = await new GetCustomerUseCase(_customers, _currentUser).ExecuteAsync(customer.Id);
        getByAuditor.IsSuccess.ShouldBeTrue();
        getByAuditor.Value.DocNumber.ShouldBe("****5678");
        getByAuditor.Value.Email.ShouldBe("a***@test.pe");
        getByAuditor.Value.PhonePrimary.ShouldBe("*****4321");

        var searchByCollector = await new SearchCustomersUseCase(_customers, _currentUser.AsRole(SecurityPolicies.Collector))
            .ExecuteAsync(new SearchCustomersRequest(Q: "Ana"));
        searchByCollector.IsSuccess.ShouldBeTrue();
        searchByCollector.Value.Items.ShouldHaveSingleItem().DocNumber.ShouldBe("****5678");

        _currentUser.AsRole(SecurityPolicies.Client, customerId: Guid.NewGuid());
        var otherClient = await new GetCustomerUseCase(_customers, _currentUser).ExecuteAsync(customer.Id);
        otherClient.IsFailure.ShouldBeTrue();
        otherClient.Error.Code.ShouldBe("Customers.NotFound");
    }

    [Fact]
    public async Task UpdateCustomer_DetectsConcurrencyConflict_AndTracksCriticalVsStandardAuditActions()
    {
        var customer = CustomersTestFactory.CreateNaturalCustomer(_clock);
        customer.MarkEmailVerified(_clock);
        _customers.Add(customer);

        var useCase = new UpdateCustomerUseCase(_customers, _uow, _currentUser.AsRole(SecurityPolicies.Analyst), _clock, _audit);

        // Wrong expected version
        var stale = await useCase.ExecuteAsync(new UpdateCustomerCommand(
            customer.Id,
            new UpdateCustomerRequest(null, new AddressPatch("Av Nueva", "Lince"), null),
            ExpectedVersion: 99));
        stale.IsFailure.ShouldBeTrue();
        stale.Error.Code.ShouldBe("Customers.ConcurrencyConflict");

        // Occupation-only update -> UPDATE
        var nonCritical = await useCase.ExecuteAsync(new UpdateCustomerCommand(
            customer.Id,
            new UpdateCustomerRequest(null, null, new SocioeconomicProfilePatch(null, "Auditora Senior", null, null)),
            ExpectedVersion: customer.Version));
        nonCritical.IsSuccess.ShouldBeTrue();
        _audit.GetActionForEntity("customers").ShouldBe("UPDATE");

        // Email change -> UPDATE_CRITICAL + resets email verification
        var critical = await useCase.ExecuteAsync(new UpdateCustomerCommand(
            customer.Id,
            new UpdateCustomerRequest(new ContactPatch("nueva@test.pe", null, null), null, null),
            ExpectedVersion: nonCritical.Value.Version));
        critical.IsSuccess.ShouldBeTrue();
        critical.Value.EmailVerified.ShouldBeFalse();
        _audit.GetActionForEntity("customers").ShouldBe("UPDATE_CRITICAL");
    }

    [Fact]
    public async Task GetOverviewAndHistory_AggregatePortfolioDebtDpdAndPagedAuditEntries()
    {
        var customer = CustomersTestFactory.CreateNaturalCustomer(_clock);
        _customers.Add(customer);

        _portfolioReader.Portfolio = new CustomerPortfolio(
            Loans:
            [
                new CustomerLoanSummary(Guid.NewGuid(), "LN-001", "Personal", "VIGENTE", "PEN", 1000.12m, 50.10m, 0m, 0, 5, null, _clock.UtcNow),
                new CustomerLoanSummary(Guid.NewGuid(), "LN-002", "Micro", "EN_MORA", "PEN", 500.55m, 25.25m, 15.10m, 12, 18, null, _clock.UtcNow),
                new CustomerLoanSummary(Guid.NewGuid(), "LN-000", "Closed", "CANCELADO", "PEN", 0m, 0m, 0m, 0, 30, null, _clock.UtcNow)
            ],
            Applications:
            [
                new CustomerApplicationSummary(Guid.NewGuid(), "APP-1", "Personal", 2000m, "PEN", 12, "REJECTED", _clock.UtcNow.AddDays(-5)),
                new CustomerApplicationSummary(Guid.NewGuid(), "APP-2", "Micro", 3500m, "PEN", 6, "EVALUATION", _clock.UtcNow.AddDays(-1))
            ]);

        var overview = await new GetCustomerOverviewUseCase(_customers, _portfolioReader, _currentUser.AsRole(SecurityPolicies.Collector))
            .ExecuteAsync(customer.Id);

        overview.IsSuccess.ShouldBeTrue();
        overview.Value.ActiveLoans.Count.ShouldBe(2);
        overview.Value.ClosedLoans.Count.ShouldBe(1);
        overview.Value.Debt.Principal.Amount.ShouldBe(1500.67m);
        overview.Value.Debt.Interest.Amount.ShouldBe(75.35m);
        overview.Value.Debt.LateFees.Amount.ShouldBe(15.10m);
        overview.Value.Debt.Total.Amount.ShouldBe(1591.12m);
        overview.Value.CurrentMaxDaysPastDue.ShouldBe(12);
        overview.Value.HistoricalMaxDaysPastDue.ShouldBe(30);
        overview.Value.HasActiveDelinquency.ShouldBeTrue();
        overview.Value.Applications.Select(a => a.ApplicationNumber).ShouldBe(["APP-2", "APP-1"]);
        overview.Value.Customer.DocNumber.ShouldBe("****5678");

        _historyReader.Entries.Add(new CustomerChangeEntry(_clock.UtcNow, Guid.NewGuid(), "op@loan.test", "UPDATE_CRITICAL", [], null, null));
        var history = await new GetCustomerHistoryUseCase(_customers, _historyReader)
            .ExecuteAsync(new GetCustomerHistoryRequest(customer.Id, 1, 10));
        history.IsSuccess.ShouldBeTrue();
        history.Value.Items.Count.ShouldBe(1);
    }
}
