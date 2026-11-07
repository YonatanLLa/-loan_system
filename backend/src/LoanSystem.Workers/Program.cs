using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Quartz;
using LoanSystem.Common.Adapters.Clock;
using LoanSystem.Common.Adapters.Outbox;
using LoanSystem.Common.Core.Ports.Outbound;
using LoanSystem.Modules.Administration.Adapters;
using LoanSystem.Modules.Collections.Adapters;
using LoanSystem.Modules.Collections.Adapters.Inbound.Jobs;
using LoanSystem.Modules.Customers.Adapters;
using LoanSystem.Modules.Integrations.Adapters;
using LoanSystem.Modules.Loans.Adapters;
using LoanSystem.Modules.Notifications.Adapters;
using LoanSystem.Modules.Notifications.Adapters.Inbound.Jobs;
using LoanSystem.Modules.Origination.Adapters;
using LoanSystem.Modules.Payments.Adapters;
using LoanSystem.Modules.Products.Adapters;
using LoanSystem.Modules.Reports.Adapters;
using LoanSystem.Modules.Security.Adapters;
using LoanSystem.Modules.Treasury.Adapters;
using LoanSystem.Modules.Treasury.Adapters.Inbound.Jobs;

var builder = Host.CreateApplicationBuilder(args);

// Common Infrastructure
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddCommonPersistence(builder.Configuration); // Dapper connection factory + outbox dispatcher
// Integration event handlers: services.AddIntegrationEventHandler<TEvent, THandler>() in each module registration

// Register 12 modules
builder.Services.AddSecurityModule(builder.Configuration);
builder.Services.AddCustomersModule(builder.Configuration);
builder.Services.AddProductsModule(builder.Configuration);
builder.Services.AddOriginationModule(builder.Configuration);
builder.Services.AddLoansModule(builder.Configuration);
builder.Services.AddPaymentsModule(builder.Configuration);
builder.Services.AddTreasuryModule(builder.Configuration);
builder.Services.AddCollectionsModule(builder.Configuration);
builder.Services.AddIntegrationsModule(builder.Configuration);
builder.Services.AddNotificationsModule(builder.Configuration);
builder.Services.AddAdministrationModule(builder.Configuration);
builder.Services.AddReportsModule(builder.Configuration);

// Quartz: each module owns its jobs (inbound adapters). Schedules run in America/Lima.
builder.Services.AddQuartz(q =>
{
    q.AddCollectionsJobs(LimaTimeZone.Instance);    // DailyMoraJob 00:05
    q.AddTreasuryJobs(LimaTimeZone.Instance);       // DailyBalanceJob 06:00, disbursements every 15 min
    q.AddNotificationsJobs(LimaTimeZone.Instance);  // DailyRemindersJob 09:00
    q.AddOutboxJobs();                              // Outbox dispatcher every 10 s
});

builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

var host = builder.Build();
host.Run();
