using System.Diagnostics;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using LoanSystem.Common.Adapters.Http;
using LoanSystem.Common.Adapters.Outbox;
using LoanSystem.Common.Core.Application;
using LoanSystem.Common.Core.Contracts;
using LoanSystem.Common.Core.Domain;
using LoanSystem.Common.Core.Ports.Outbound;
using LoanSystem.Modules.Customers.Adapters;
using LoanSystem.Modules.Customers.Adapters.Inbound.Http;
using LoanSystem.Modules.Customers.Core.Contracts;
using LoanSystem.Modules.Customers.Core.Contracts.Consents;
using LoanSystem.Modules.Customers.Core.Contracts.IntegrationEvents;
using LoanSystem.Modules.Customers.Core.Ports.Inbound;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace LoanSystem.Adapters.UnitTests.Customers;

public class CustomersEndpointsTests
{
    private static readonly Guid CustomerId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid OtherCustomerId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid DocId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid AccountId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

    [Fact]
    public async Task RegisterCustomer_MissingIdempotencyKey_Returns400WithStandardCode()
    {
        var tracker = new CallTracker();
        await using var harness = BuildPipeline(tracker);

        var ctx = await harness.SendAsync("POST", "/api/customers", ValidRegisterJson(), role: SecurityPolicies.Operator);

        ctx.Response.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        harness.ReadBody(ctx).ShouldContain("Common.MissingIdempotencyKey");
        tracker.Count("RegisterCustomer").ShouldBe(0);
    }

    [Theory]
    [InlineData("GET", "/api/customers", SecurityPolicies.Collector, 200)]
    [InlineData("GET", "/api/customers", SecurityPolicies.Auditor, 200)]
    [InlineData("GET", "/api/customers", SecurityPolicies.Client, 403)]
    [InlineData("POST", "/api/customers", SecurityPolicies.Analyst, 201)]
    [InlineData("POST", "/api/customers", SecurityPolicies.Collector, 403)]
    [InlineData("POST", "/api/customers", SecurityPolicies.Auditor, 403)]
    [InlineData("POST", "/api/customers", SecurityPolicies.Client, 403)]
    [InlineData("GET", "/api/customers/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/history", SecurityPolicies.Auditor, 200)]
    [InlineData("GET", "/api/customers/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/history", SecurityPolicies.Operator, 403)]
    [InlineData("GET", "/api/customers/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/history", SecurityPolicies.Collector, 403)]
    [InlineData("POST", "/api/customers/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/kyc/approve", SecurityPolicies.Analyst, 200)]
    [InlineData("POST", "/api/customers/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/kyc/approve", SecurityPolicies.Operator, 403)]
    [InlineData("POST", "/api/customers/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/kyc/reject", SecurityPolicies.Operator, 403)]
    [InlineData("POST", "/api/customers/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/kyc/retry-checks", SecurityPolicies.Operator, 403)]
    [InlineData("POST", "/api/customers/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/bank-accounts", SecurityPolicies.Operator, 201)]
    [InlineData("POST", "/api/customers/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/bank-accounts", SecurityPolicies.Analyst, 403)]
    [InlineData("POST", "/api/customers/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/bank-accounts/dddddddd-dddd-dddd-dddd-dddddddddddd/verify", SecurityPolicies.Operator, 200)]
    [InlineData("POST", "/api/customers/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/bank-accounts/dddddddd-dddd-dddd-dddd-dddddddddddd/verify", SecurityPolicies.Analyst, 403)]
    [InlineData("POST", "/api/customers/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/bank-accounts/dddddddd-dddd-dddd-dddd-dddddddddddd/verify", SecurityPolicies.Client, 403)]
    public async Task Endpoints_EnforceRoleAuthorizationMatrix(string method, string path, string role, int expectedStatus)
    {
        var tracker = new CallTracker();
        await using var harness = BuildPipeline(tracker);

        string? body = null;
        if (method is "POST" or "PUT")
        {
            body = path switch
            {
                "/api/customers" => ValidRegisterJson(),
                _ when path.EndsWith("/bank-accounts", StringComparison.Ordinal) =>
                    JsonSerializer.Serialize(new RegisterBankAccountRequest("BCP", "CUENTA_AHORRO", "00219100000000001234", null, null)),
                _ when path.EndsWith("/verify", StringComparison.Ordinal) =>
                    JsonSerializer.Serialize(new VerifyBankAccountRequest("Ana Quispe")),
                _ when path.EndsWith("/approve", StringComparison.Ordinal) =>
                    JsonSerializer.Serialize(new ApproveKycRequest("Verified cleanly.")),
                _ when path.EndsWith("/reject", StringComparison.Ordinal) =>
                    JsonSerializer.Serialize(new RejectKycRequest("Mismatch detected.")),
                _ => "{}"
            };
        }

        var ctx = await harness.SendAsync(method, path, body, role: role, idempotencyKey: Guid.NewGuid().ToString("N"));
        ctx.Response.StatusCode.ShouldBe(expectedStatus, harness.ReadBody(ctx));
    }

    [Theory]
    [InlineData("GET", "/api/customers/{id}")]
    [InlineData("PATCH", "/api/customers/{id}")]
    [InlineData("GET", "/api/customers/{id}/overview")]
    [InlineData("GET", "/api/customers/{id}/documents")]
    [InlineData("GET", "/api/customers/{id}/kyc")]
    [InlineData("POST", "/api/customers/{id}/kyc/submit")]
    [InlineData("POST", "/api/customers/{id}/kyc/resubmit")]
    [InlineData("GET", "/api/customers/{id}/consents")]
    [InlineData("POST", "/api/customers/{id}/consents")]
    [InlineData("DELETE", "/api/customers/{id}/consents/MARKETING")]
    [InlineData("GET", "/api/customers/{id}/bank-accounts")]
    [InlineData("POST", "/api/customers/{id}/bank-accounts")]
    [InlineData("DELETE", "/api/customers/{id}/bank-accounts/dddddddd-dddd-dddd-dddd-dddddddddddd")]
    [InlineData("PUT", "/api/customers/{id}/bank-accounts/dddddddd-dddd-dddd-dddd-dddddddddddd/primary")]
    [InlineData("POST", "/api/customers/{id}/bank-accounts/dddddddd-dddd-dddd-dddd-dddddddddddd/primary/confirm")]
    [InlineData("POST", "/api/customers/{id}/contact/verify/start")]
    [InlineData("POST", "/api/customers/{id}/contact/verify/confirm")]
    public async Task RoleClient_IsIsolatedToOwnCustomerId_Returns404ForOthers(string method, string template)
    {
        var tracker = new CallTracker();
        await using var harness = BuildPipeline(tracker);

        async Task<HttpContext> SendFor(Guid targetId)
        {
            var path = template.Replace("{id}", targetId.ToString(), StringComparison.Ordinal);
            var body = method is "POST" or "PATCH" or "PUT" ? "{}" : null;
            return await harness.SendAsync(
                method,
                path,
                body,
                role: SecurityPolicies.Client,
                customerClaimId: CustomerId,
                idempotencyKey: Guid.NewGuid().ToString("N"));
        }

        // Cross-customer request must stop in RequireCustomerOwnershipOrStaff filter with 404 before invoking use case
        var otherCtx = await SendFor(OtherCustomerId);
        otherCtx.Response.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
        tracker.TotalCalls.ShouldBe(0);

        // Same-customer request reaches endpoint handler
        var ownCtx = await SendFor(CustomerId);
        ownCtx.Response.StatusCode.ShouldBeLessThan(300, harness.ReadBody(ownCtx));
        tracker.TotalCalls.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task UpdateCustomer_ValidatesIfMatchHeaderAndEmitsETag()
    {
        var tracker = new CallTracker();
        await using var harness = BuildPipeline(tracker);

        var badCtx = await harness.SendAsync(
            "PATCH",
            $"/api/customers/{CustomerId}",
            "{}",
            role: SecurityPolicies.Operator,
            headers: new Dictionary<string, string> { ["If-Match"] = "\"not-a-version\"" });
        badCtx.Response.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        tracker.Count("UpdateCustomer").ShouldBe(0);

        var okCtx = await harness.SendAsync(
            "PATCH",
            $"/api/customers/{CustomerId}",
            "{}",
            role: SecurityPolicies.Operator,
            headers: new Dictionary<string, string> { ["If-Match"] = "\"1\"" });
        okCtx.Response.StatusCode.ShouldBe(StatusCodes.Status200OK);
        okCtx.Response.Headers.ETag.ToString().ShouldBe("\"2\"");
        tracker.LastExpectedVersion.ShouldBe(1);
    }

    [Fact]
    public async Task UploadDocumentMultipartAndAnonymousEndpoints_WorkAndNeverExposeRawCci()
    {
        var tracker = new CallTracker();
        await using var harness = BuildPipeline(tracker);

        // Anonymous consent catalog works without auth
        var consentsCtx = await harness.SendAsync("GET", "/api/consents/current");
        consentsCtx.Response.StatusCode.ShouldBe(StatusCodes.Status200OK);

        // Anonymous signed local file stream works without auth
        var fileCtx = await harness.SendAsync("GET", "/api/customers/files/valid-token");
        fileCtx.Response.StatusCode.ShouldBe(StatusCodes.Status200OK);
        fileCtx.Response.ContentType.ShouldBe("application/pdf");

        // Multipart upload works with category & file
        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");
        form.Add(fileContent, "file", "front.png");
        form.Add(new StringContent("DNI_FRONTAL"), "category");

        await using var ms = new MemoryStream();
        await form.CopyToAsync(ms);
        ms.Position = 0;

        var uploadCtx = await harness.SendRawAsync(
            "POST",
            $"/api/customers/{CustomerId}/documents",
            ms.ToArray(),
            form.Headers.ContentType!.ToString(),
            role: SecurityPolicies.Operator);
        uploadCtx.Response.StatusCode.ShouldBe(StatusCodes.Status201Created);

        // List bank accounts JSON never exposes CCI, encrypted or hash fields (CA-09.d)
        var acctCtx = await harness.SendAsync("GET", $"/api/customers/{CustomerId}/bank-accounts", role: SecurityPolicies.Auditor);
        acctCtx.Response.StatusCode.ShouldBe(StatusCodes.Status200OK);
        var acctJson = harness.ReadBody(acctCtx);
        acctJson.ShouldNotContain("cci", Case.Insensitive);
        acctJson.ShouldNotContain("accountNumberEncrypted", Case.Insensitive);
        acctJson.ShouldNotContain("accountNumberHash", Case.Insensitive);
    }

    [Fact]
    public void AddCustomersModule_RegistersAllNineIntegrationEventsAndPublicContracts()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Database"] = "Host=localhost;Database=loans;Username=u;Password=p"
            })
            .Build();
        services.AddSingleton<IConfiguration>(config);
        services.AddSingleton<IHostEnvironment>(new TestEnv());
        services.AddSingleton<IClock, FixedClock>();
        services.AddCustomersModule(config);

        using var sp = services.BuildServiceProvider();
        var registry = sp.GetRequiredService<IntegrationEventTypeRegistry>();

        Type[] expectedEvents =
        [
            typeof(CustomerRegisteredIntegrationEvent),
            typeof(CustomerKycSubmittedIntegrationEvent),
            typeof(CustomerKycVerifiedIntegrationEvent),
            typeof(CustomerKycRejectedIntegrationEvent),
            typeof(CustomerKycExpiredIntegrationEvent),
            typeof(CustomerKycExpiringSoonIntegrationEvent),
            typeof(CustomerDisbursementAccountChangedIntegrationEvent),
            typeof(CustomerMarketingConsentRevokedIntegrationEvent),
            typeof(OtpDeliveryRequestedIntegrationEvent)
        ];

        foreach (var eventType in expectedEvents)
        {
            registry.TryResolve(eventType.FullName!, out var resolved).ShouldBeTrue(eventType.Name);
            resolved.ShouldBe(eventType);
        }

        using var scope = sp.CreateScope();
        scope.ServiceProvider.GetRequiredService<ICustomerConsentQuery>().ShouldNotBeNull();
        scope.ServiceProvider.GetRequiredService<ICustomerDisbursementEligibility>().ShouldNotBeNull();
        scope.ServiceProvider.GetRequiredService<ICustomerPortalLookup>().ShouldNotBeNull();
        scope.ServiceProvider.GetRequiredService<ICustomerOtpService>().ShouldNotBeNull();
    }

    private static string ValidRegisterJson() =>
        JsonSerializer.Serialize(new RegisterCustomerRequest(
            "NATURAL", "DNI", "12345678", "Ana Quispe", new DateOnly(1995, 5, 1),
            "ana@test.pe", "987654321", null, "Av Sol 10", "Lima", 3000m, null, null, 0,
            [new ConsentAcceptance("DATA_PROCESSING", "v1.0")]));

    private static PipelineHarness BuildPipeline(CallTracker tracker)
    {
        var services = new ServiceCollection();
        var diagnosticListener = new DiagnosticListener("Microsoft.AspNetCore");
        services.AddSingleton(diagnosticListener);
        services.AddSingleton<DiagnosticSource>(diagnosticListener);
        services.AddLogging();
        services.AddProblemDetails();
        services.AddRouting();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUserProvider>();
        services.AddAuthentication("HeaderTest")
            .AddScheme<AuthenticationSchemeOptions, HeaderTestAuthHandler>("HeaderTest", _ => { });
        services.AddAuthorization(o => o.AddLoanSystemAuthorizationPolicies());

        RegisterStubUseCases(services, tracker);

        var sp = services.BuildServiceProvider();
        var app = new ApplicationBuilder(sp);
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseEndpoints(endpoints => endpoints.MapCustomersEndpoints());

        return new PipelineHarness(sp, app.Build());
    }

    private static void RegisterStubUseCases(IServiceCollection services, CallTracker tracker)
    {
        var sampleCustomer = new CustomerResponse(
            CustomerId, "NATURAL", "DNI", "12345678", "ANA QUISPE", new DateOnly(1995, 5, 1),
            "ana@test.pe", true, "987654321", true, null, "Av 1", "Lima", 3000m, null, null, 0,
            "PENDING", null, false, "LOW", 0m, 2, DateTimeOffset.UtcNow);
        var sampleKyc = new CustomerKycStatusResponse(
            CustomerId, "PENDING", false, null, null, null, null, null, false, "LOW", [], [], [], []);
        var sampleDoc = new CustomerDocumentResponse(
            DocId, CustomerId, "DNI_FRONTAL", "abc", "image/png", 8, "front.png", true, DateTimeOffset.UtcNow, null);
        var sampleAccount = new BankAccountResponse(
            AccountId, "BCP", "002", "CUENTA_AHORRO", "1234", null, true, false, "ACTIVE", null);

        void Reg<TInterface>(string name, Func<object?, object?> fn) where TInterface : class =>
            services.AddScoped(_ =>
            {
                var proxy = DispatchProxy.Create<TInterface, UseCaseProxy>();
                ((UseCaseProxy)(object)proxy).Init(tracker, name, fn);
                return proxy;
            });

        Reg<IRegisterCustomerUseCase>("RegisterCustomer", _ => Result<CustomerResponse>.Success(sampleCustomer));
        Reg<IGetCustomerUseCase>("GetCustomer", _ => Result<CustomerResponse>.Success(sampleCustomer));
        Reg<ISearchCustomersUseCase>("SearchCustomers", _ => Result<PagedResult<CustomerListItem>>.Success(new PagedResult<CustomerListItem>([], 0, 1, 20)));
        Reg<IUpdateCustomerUseCase>("UpdateCustomer", arg =>
        {
            if (arg is UpdateCustomerCommand cmd) tracker.LastExpectedVersion = cmd.ExpectedVersion;
            return Result<CustomerResponse>.Success(sampleCustomer);
        });
        Reg<IGetCustomerHistoryUseCase>("History", _ => Result<PagedResult<CustomerChangeEntry>>.Success(new PagedResult<CustomerChangeEntry>([], 0, 1, 20)));
        Reg<IGetCustomerOverviewUseCase>("Overview", _ => Result<CustomerOverviewResponse>.Success(
            new CustomerOverviewResponse(sampleCustomer, [], [], new ConsolidatedDebt(Money.Zero(), Money.Zero(), Money.Zero(), Money.Zero()), 0, 0, false, [], 0m)));

        Reg<IUploadCustomerDocumentUseCase>("UploadDoc", _ => Result<CustomerDocumentResponse>.Success(sampleDoc));
        Reg<IListCustomerDocumentsUseCase>("ListDocs", _ => Result.Success<IReadOnlyList<CustomerDocumentResponse>>(new[] { sampleDoc }));
        Reg<IGetDocumentUrlUseCase>("DocUrl", _ => Result<DocumentSignedUrlResponse>.Success(new DocumentSignedUrlResponse("/api/customers/files/tok", DateTimeOffset.UtcNow.AddMinutes(5))));
        Reg<IDownloadLocalDocumentUseCase>("DownloadLocal", _ => Result<LocalDocumentContent>.Success(new LocalDocumentContent(new MemoryStream("%PDF-1.7"u8.ToArray()), "application/pdf", "doc.pdf")));

        Reg<IGetCustomerKycUseCase>("GetKyc", _ => Result<CustomerKycStatusResponse>.Success(sampleKyc));
        Reg<ISubmitKycUseCase>("SubmitKyc", _ => Result<CustomerKycStatusResponse>.Success(sampleKyc));
        Reg<IApproveKycUseCase>("ApproveKyc", _ => Result<CustomerKycStatusResponse>.Success(sampleKyc));
        Reg<IRejectKycUseCase>("RejectKyc", _ => Result<CustomerKycStatusResponse>.Success(sampleKyc));
        Reg<IResubmitKycUseCase>("ResubmitKyc", _ => Result<CustomerKycStatusResponse>.Success(sampleKyc));
        Reg<IRetryKycChecksUseCase>("RetryKyc", _ => Result.Success<IReadOnlyList<KycVerificationResponse>>(Array.Empty<KycVerificationResponse>()));

        Reg<IGetCurrentConsentsUseCase>("CurrentConsents", _ => Result.Success<IReadOnlyList<CurrentConsentTextResponse>>(Array.Empty<CurrentConsentTextResponse>()));
        Reg<IListCustomerConsentsUseCase>("ListConsents", _ => Result.Success<IReadOnlyList<CustomerConsentResponse>>(Array.Empty<CustomerConsentResponse>()));
        Reg<IGrantCustomerConsentUseCase>("GrantConsent", _ => Result<CustomerConsentResponse>.Success(new CustomerConsentResponse(Guid.NewGuid(), "MARKETING", "v1", DateTimeOffset.UtcNow, "BACKOFFICE", null)));
        Reg<IRevokeCustomerConsentUseCase>("RevokeConsent", _ => Result.Success());

        Reg<IRegisterBankAccountUseCase>("RegisterAccount", _ => Result<BankAccountResponse>.Success(sampleAccount));
        Reg<IListBankAccountsUseCase>("ListAccounts", _ => Result.Success<IReadOnlyList<BankAccountResponse>>(new[] { sampleAccount }));
        Reg<IVerifyBankAccountUseCase>("VerifyAccount", _ => Result<BankAccountResponse>.Success(sampleAccount));
        Reg<IDeactivateBankAccountUseCase>("DeactivateAccount", _ => Result.Success());
        Reg<ISetPrimaryBankAccountUseCase>("SetPrimary", _ => Result<SetPrimaryBankAccountResult>.Success(new SetPrimaryBankAccountResult(false, sampleAccount, null)));
        Reg<IConfirmPrimaryBankAccountUseCase>("ConfirmPrimary", _ => Result<BankAccountResponse>.Success(sampleAccount));

        Reg<IStartContactVerificationUseCase>("StartOtp", _ => Result<OtpIssued>.Success(new OtpIssued(Guid.NewGuid(), "EMAIL", "a***@test.pe", DateTimeOffset.UtcNow.AddMinutes(5))));
        Reg<IConfirmContactVerificationUseCase>("ConfirmOtp", _ => Result.Success());
    }

    private sealed class PipelineHarness(ServiceProvider sp, RequestDelegate pipeline) : IAsyncDisposable
    {
        public async Task<HttpContext> SendAsync(
            string method,
            string path,
            string? jsonBody = null,
            string? role = null,
            Guid? customerClaimId = null,
            string? idempotencyKey = null,
            IDictionary<string, string>? headers = null)
        {
            var bytes = jsonBody is null ? [] : Encoding.UTF8.GetBytes(jsonBody);
            var contentType = jsonBody is null ? null : "application/json";
            return await SendRawAsync(method, path, bytes, contentType, role, customerClaimId, idempotencyKey, headers);
        }

        public async Task<HttpContext> SendRawAsync(
            string method,
            string path,
            byte[] bodyBytes,
            string? contentType,
            string? role = null,
            Guid? customerClaimId = null,
            string? idempotencyKey = null,
            IDictionary<string, string>? headers = null)
        {
            await using var scope = sp.CreateAsyncScope();
            var ctx = new DefaultHttpContext
            {
                RequestServices = scope.ServiceProvider,
                Response = { Body = new MemoryStream() }
            };
            ctx.Request.Method = method;
            ctx.Request.Protocol = "HTTP/1.1";
            ctx.Request.Path = path;
            ctx.Features.Set<IHttpRequestBodyDetectionFeature>(new BodyDetectionFeature(bodyBytes.Length > 0));
            if (bodyBytes.Length > 0)
            {
                ctx.Request.Body = new MemoryStream(bodyBytes);
                ctx.Request.ContentLength = bodyBytes.Length;
                ctx.Request.ContentType = contentType;
            }
            if (role is not null) ctx.Request.Headers["X-Test-Role"] = role;
            if (customerClaimId.HasValue) ctx.Request.Headers["X-Test-CustomerId"] = customerClaimId.Value.ToString();
            if (idempotencyKey is not null) ctx.Request.Headers["Idempotency-Key"] = idempotencyKey;
            if (headers is not null)
            {
                foreach (var kv in headers) ctx.Request.Headers[kv.Key] = kv.Value;
            }

            var accessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
            accessor.HttpContext = ctx;
            try
            {
                await pipeline(ctx);
                return ctx;
            }
            finally
            {
                accessor.HttpContext = null;
            }
        }

        public string ReadBody(HttpContext ctx)
        {
            ctx.Response.Body.Position = 0;
            using var reader = new StreamReader(ctx.Response.Body, Encoding.UTF8, leaveOpen: true);
            return reader.ReadToEnd();
        }

        public ValueTask DisposeAsync() => sp.DisposeAsync();
    }

    internal sealed class CallTracker
    {
        private readonly Dictionary<string, int> _counts = [];
        public int TotalCalls => _counts.Values.Sum();
        public int? LastExpectedVersion { get; set; }
        public void Hit(string name) => _counts[name] = Count(name) + 1;
        public int Count(string name) => _counts.TryGetValue(name, out var n) ? n : 0;
    }

    public class UseCaseProxy : DispatchProxy
    {
        private CallTracker _tracker = null!;
        private string _name = string.Empty;
        private Func<object?, object?> _responder = null!;

        internal void Init(CallTracker tracker, string name, Func<object?, object?> responder)
        {
            _tracker = tracker;
            _name = name;
            _responder = responder;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            _tracker.Hit(_name);
            var req = args is { Length: > 1 } ? args[0] : null;
            var resultObj = _responder(req)!;
            var taskInnerType = targetMethod!.ReturnType.GetGenericArguments()[0];
            var fromResult = typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(taskInnerType);
            return fromResult.Invoke(null, [resultObj]);
        }
    }

    private sealed class HeaderTestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-Test-Role", out var roleHeader))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new(ClaimTypes.Role, roleHeader.ToString())
            };
            if (Request.Headers.TryGetValue("X-Test-CustomerId", out var cidHeader))
            {
                claims.Add(new Claim("customer_id", cidHeader.ToString()));
            }

            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
        }
    }

    private sealed class TestEnv : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "LoanSystem.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
        public DateTime LimaNow => DateTime.UtcNow.AddHours(-5);
        public DateOnly LimaToday => DateOnly.FromDateTime(LimaNow);
    }

    private sealed class BodyDetectionFeature(bool canHaveBody) : IHttpRequestBodyDetectionFeature
    {
        public bool CanHaveBody => canHaveBody;
    }
}
