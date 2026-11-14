using LoanSystem.Common.Adapters.Configuration;
using LoanSystem.Common.Adapters.Crypto;
using LoanSystem.Common.Adapters.Http;
using LoanSystem.Common.Adapters.Outbox;
using LoanSystem.Common.Adapters.Persistence;
using LoanSystem.Common.Core.Ports.Outbound;
using LoanSystem.Modules.Customers.Adapters.Inbound.Events;
using LoanSystem.Modules.Customers.Adapters.Outbound.Persistence;
using LoanSystem.Modules.Customers.Adapters.Outbound.Providers;
using LoanSystem.Modules.Customers.Core.Application;
using LoanSystem.Modules.Customers.Core.Contracts;
using LoanSystem.Modules.Customers.Core.Contracts.IntegrationEvents;
using LoanSystem.Modules.Customers.Core.Domain.Validators;
using LoanSystem.Modules.Customers.Core.Ports.Inbound;
using LoanSystem.Modules.Customers.Core.Ports.Outbound;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LoanSystem.Modules.Customers.Adapters;

public static class CustomersModule
{
    public static IServiceCollection AddCustomersModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetRequiredConnectionString("Database");

        services.AddOptions<CustomersOptions>()
            .Bind(configuration.GetSection(CustomersOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<CustomersOptions>, CustomersOptionsValidator>();
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<CustomersOptions>>().Value);

        services.AddAuditLogging();
        services.AddMemoryCache();
        services.AddHttpContextAccessor();
        services.TryAddSingleton<IDbConnectionFactory>(_ => new NpgsqlConnectionFactory(connectionString));
        services.TryAddScoped<ICurrentUser, NullCurrentUser>();
        services.TryAddScoped<IClientInfoProvider, HttpClientInfoProvider>();

        services.AddDbContext<CustomersDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString, CustomersDbContext.MapEnums)
                   .UseAuditLog(sp);
        });
        services.AddScoped<ICustomersUnitOfWork>(sp => sp.GetRequiredService<CustomersDbContext>());
        services.AddScoped<ICustomersOutbox>(sp => sp.GetRequiredService<CustomersDbContext>());

        // Persistence & readers
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IDocumentTypeCatalog, DocumentTypeCatalog>();
        services.AddSingleton<IBanksCatalog, StaticBanksCatalog>();
        services.AddScoped<ICustomerDocumentRepository, CustomerDocumentRepository>();
        services.AddScoped<IKycVerificationRepository, KycVerificationRepository>();
        services.AddScoped<IConsentTextRepository, ConsentTextRepository>();
        services.AddScoped<ICustomerConsentRepository, CustomerConsentRepository>();
        services.AddScoped<IOtpRepository, OtpRepository>();
        services.AddScoped<ICustomerBankAccountRepository, CustomerBankAccountRepository>();
        services.AddScoped<IBankAccountExistenceLookup, BankAccountExistenceLookup>();
        services.AddScoped<ICustomerAuditWriter, CustomerAuditWriter>();
        services.AddScoped<ICustomerHistoryReader, CustomerHistoryReader>();
        services.AddScoped<ICustomerPortfolioReader, CustomerPortfolioReader>();
        services.AddScoped<IKycExpirationBatchStore, KycExpirationBatchStore>();

        // Crypto, OTP, identity verification & object storage
        services.AddSingleton(sp => new AesGcmFieldProtector(
            sp.GetRequiredService<IConfiguration>(),
            sp.GetRequiredService<IHostEnvironment>(),
            "Customers:FieldProtection",
            sp.GetService<ILogger<AesGcmFieldProtector>>()));
        services.AddSingleton<IFieldProtector, CustomersFieldProtectorAdapter>();
        services.AddSingleton<IOtpHasher, HmacOtpHasher>();
        services.AddScoped<IOtpSender, OutboxOtpSender>();
        services.AddSingleton<ICustomerDiagnostics, CustomerLoggerDiagnostics>();
        services.AddSingleton<ICciValidator, CciValidator>();
        services.AddSingleton<IIdentityVerifier, FakeIdentityVerifier>();

        services.AddHttpClient("CustomersS3");
        services.AddSingleton<IDocumentStorage>(sp =>
        {
            var opts = sp.GetRequiredService<CustomersOptions>();
            if (string.Equals(opts.Storage.Provider, "S3", StringComparison.OrdinalIgnoreCase))
            {
                var factory = sp.GetRequiredService<IHttpClientFactory>();
                return new S3DocumentStorage(
                    factory.CreateClient("CustomersS3"),
                    opts,
                    sp.GetRequiredService<IConfiguration>(),
                    sp.GetRequiredService<IClock>());
            }

            return new LocalDocumentStorage(
                opts,
                sp.GetRequiredService<IConfiguration>(),
                sp.GetRequiredService<IHostEnvironment>(),
                sp.GetRequiredService<IClock>());
        });
        services.AddSingleton<ILocalSignedFileReader>(sp =>
        {
            var storage = sp.GetRequiredService<IDocumentStorage>();
            return storage as ILocalSignedFileReader ?? new NullLocalSignedFileReader();
        });

        // Cross-module contracts
        services.AddScoped<ICustomerDisbursementEligibility, CustomerDisbursementEligibilityService>();
        services.AddScoped<ICustomerConsentQuery, CustomerConsentQueryService>();
        services.AddScoped<ICustomerPortalLookup, CustomerPortalLookupService>();
        services.AddScoped<ICustomerOtpService, CustomerOtpService>();

        // Inbound use cases
        services.AddScoped<IRegisterCustomerUseCase, RegisterCustomerUseCase>();
        services.AddScoped<IGetCustomerUseCase, GetCustomerUseCase>();
        services.AddScoped<ISearchCustomersUseCase, SearchCustomersUseCase>();
        services.AddScoped<IUpdateCustomerUseCase, UpdateCustomerUseCase>();
        services.AddScoped<IGetCustomerHistoryUseCase, GetCustomerHistoryUseCase>();
        services.AddScoped<IGetCustomerOverviewUseCase, GetCustomerOverviewUseCase>();

        services.AddScoped<IUploadCustomerDocumentUseCase, UploadCustomerDocumentUseCase>();
        services.AddScoped<IListCustomerDocumentsUseCase, ListCustomerDocumentsUseCase>();
        services.AddScoped<IGetDocumentUrlUseCase, GetDocumentUrlUseCase>();
        services.AddScoped<IDownloadLocalDocumentUseCase, DownloadLocalDocumentUseCase>();

        services.AddScoped<IGetCustomerKycUseCase, GetCustomerKycUseCase>();
        services.AddScoped<ISubmitKycUseCase, SubmitKycUseCase>();
        services.AddScoped<IApproveKycUseCase, ApproveKycUseCase>();
        services.AddScoped<IRejectKycUseCase, RejectKycUseCase>();
        services.AddScoped<IResubmitKycUseCase, ResubmitKycUseCase>();
        services.AddScoped<RunKycChecksUseCase>();
        services.AddScoped<IRunKycChecksUseCase>(sp => sp.GetRequiredService<RunKycChecksUseCase>());
        services.AddScoped<IRetryKycChecksUseCase, RetryKycChecksUseCase>();
        services.AddScoped<IExpireKycUseCase, ExpireKycUseCase>();

        services.AddScoped<IGetCurrentConsentsUseCase, GetCurrentConsentsUseCase>();
        services.AddScoped<IListCustomerConsentsUseCase, ListCustomerConsentsUseCase>();
        services.AddScoped<IGrantCustomerConsentUseCase, GrantCustomerConsentUseCase>();
        services.AddScoped<IRevokeCustomerConsentUseCase, RevokeCustomerConsentUseCase>();

        services.AddScoped<IRegisterBankAccountUseCase, RegisterBankAccountUseCase>();
        services.AddScoped<IListBankAccountsUseCase, ListBankAccountsUseCase>();
        services.AddScoped<IVerifyBankAccountUseCase, VerifyBankAccountUseCase>();
        services.AddScoped<IDeactivateBankAccountUseCase, DeactivateBankAccountUseCase>();
        services.AddScoped<ISetPrimaryBankAccountUseCase, SetPrimaryBankAccountUseCase>();
        services.AddScoped<IConfirmPrimaryBankAccountUseCase, ConfirmPrimaryBankAccountUseCase>();

        services.AddScoped<IStartContactVerificationUseCase, StartContactVerificationUseCase>();
        services.AddScoped<IConfirmContactVerificationUseCase, ConfirmContactVerificationUseCase>();

        // Integration events & handlers
        services.AddIntegrationEventHandler<CustomerKycSubmittedIntegrationEvent, RunKycChecksHandler>();
        services.RegisterIntegrationEvent<CustomerRegisteredIntegrationEvent>();
        services.RegisterIntegrationEvent<CustomerKycVerifiedIntegrationEvent>();
        services.RegisterIntegrationEvent<CustomerKycRejectedIntegrationEvent>();
        services.RegisterIntegrationEvent<CustomerKycExpiredIntegrationEvent>();
        services.RegisterIntegrationEvent<CustomerKycExpiringSoonIntegrationEvent>();
        services.RegisterIntegrationEvent<CustomerDisbursementAccountChangedIntegrationEvent>();
        services.RegisterIntegrationEvent<CustomerMarketingConsentRevokedIntegrationEvent>();
        services.RegisterIntegrationEvent<OtpDeliveryRequestedIntegrationEvent>();

        return services;
    }

    private sealed class NullCurrentUser : ICurrentUser
    {
        public Guid? UserId => null;
        public string? Email => null;
        public IReadOnlyCollection<string> Roles => Array.Empty<string>();
        public Guid? CustomerId => null;
        public bool IsAuthenticated => false;
        public bool IsInRole(string role) => false;
    }
}
