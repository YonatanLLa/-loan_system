using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Dapper;
using LoanSystem.Common.Adapters.Clock;
using LoanSystem.Common.Core.Application;
using LoanSystem.Modules.Customers.Adapters.Inbound.Jobs;
using LoanSystem.Modules.Customers.Core.Contracts;
using LoanSystem.Modules.Customers.Core.Contracts.Consents;
using LoanSystem.Modules.Customers.Core.Contracts.IntegrationEvents;
using LoanSystem.Modules.Customers.Core.Ports.Inbound;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Quartz;
using Shouldly;
using Testcontainers.PostgreSql;
using Xunit;

namespace LoanSystem.IntegrationTests;

public class CustomersIntegrationTests : IAsyncLifetime
{
    private PostgreSqlContainer? _postgresContainer;
    private bool _dockerAvailable = true;

    public async Task InitializeAsync()
    {
        try
        {
            _postgresContainer = new PostgreSqlBuilder("postgres:17-alpine")
                .WithDatabase("loan_system_customers_test")
                .WithUsername("postgres")
                .WithPassword("postgrespassword")
                .Build();

            await _postgresContainer.StartAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Docker not available or failed to start: {ex.Message}");
            _dockerAvailable = false;
        }
    }

    public async Task DisposeAsync()
    {
        if (_postgresContainer is not null)
        {
            await _postgresContainer.DisposeAsync();
        }
    }

    [Fact]
    public void WebApplicationFactory_InDevelopment_MapsAllCustomersEndpointsAndResolvesContracts()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(Environments.Development);
            builder.UseSetting("Secrets:Provider", "None");
            builder.UseSetting("ConnectionStrings:Redis", "");
        });

        using var scope = factory.Services.CreateScope();
        var endpointSource = scope.ServiceProvider.GetRequiredService<EndpointDataSource>();
        var routeEndpoints = endpointSource.Endpoints.OfType<RouteEndpoint>().ToList();

        (string Method, string Pattern, bool AllowAnonymous)[] expectedRoutes =
        [
            ("POST", "/api/customers/", false),
            ("GET", "/api/customers/", false),
            ("GET", "/api/customers/{id:guid}", false),
            ("PATCH", "/api/customers/{id:guid}", false),
            ("GET", "/api/customers/{id:guid}/history", false),
            ("GET", "/api/customers/{id:guid}/overview", false),
            ("POST", "/api/customers/{id:guid}/documents", false),
            ("GET", "/api/customers/{id:guid}/documents", false),
            ("GET", "/api/customers/{id:guid}/documents/{docId:guid}/url", false),
            ("GET", "/api/customers/files/{token}", true),
            ("GET", "/api/customers/{id:guid}/kyc", false),
            ("POST", "/api/customers/{id:guid}/kyc/submit", false),
            ("POST", "/api/customers/{id:guid}/kyc/approve", false),
            ("POST", "/api/customers/{id:guid}/kyc/reject", false),
            ("POST", "/api/customers/{id:guid}/kyc/resubmit", false),
            ("POST", "/api/customers/{id:guid}/kyc/retry-checks", false),
            ("GET", "/api/consents/current", true),
            ("GET", "/api/customers/{id:guid}/consents", false),
            ("POST", "/api/customers/{id:guid}/consents", false),
            ("DELETE", "/api/customers/{id:guid}/consents/{purpose}", false),
            ("POST", "/api/customers/{id:guid}/bank-accounts", false),
            ("GET", "/api/customers/{id:guid}/bank-accounts", false),
            ("POST", "/api/customers/{id:guid}/bank-accounts/{accountId:guid}/verify", false),
            ("DELETE", "/api/customers/{id:guid}/bank-accounts/{accountId:guid}", false),
            ("PUT", "/api/customers/{id:guid}/bank-accounts/{accountId:guid}/primary", false),
            ("POST", "/api/customers/{id:guid}/bank-accounts/{accountId:guid}/primary/confirm", false),
            ("POST", "/api/customers/{id:guid}/contact/verify/start", false),
            ("POST", "/api/customers/{id:guid}/contact/verify/confirm", false)
        ];

        foreach (var expected in expectedRoutes)
        {
            var matched = routeEndpoints.FirstOrDefault(e =>
                string.Equals(e.RoutePattern.RawText, expected.Pattern, StringComparison.OrdinalIgnoreCase) &&
                e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains(expected.Method, StringComparer.OrdinalIgnoreCase) == true);

            matched.ShouldNotBeNull($"Expected route {expected.Method} {expected.Pattern} to be mapped in WebApi.");
            if (expected.AllowAnonymous)
            {
                matched.Metadata.GetMetadata<IAllowAnonymous>().ShouldNotBeNull($"{expected.Method} {expected.Pattern} should allow anonymous.");
            }
            else
            {
                matched.Metadata.GetMetadata<IAuthorizeData>().ShouldNotBeNull($"{expected.Method} {expected.Pattern} should require authorization.");
            }
        }

        // Cross-module contracts and module services resolve cleanly from DI
        scope.ServiceProvider.GetRequiredService<ICustomerConsentQuery>().ShouldNotBeNull();
        scope.ServiceProvider.GetRequiredService<ICustomerDisbursementEligibility>().ShouldNotBeNull();
        scope.ServiceProvider.GetRequiredService<ICustomerPortalLookup>().ShouldNotBeNull();
        scope.ServiceProvider.GetRequiredService<ICustomerOtpService>().ShouldNotBeNull();
    }

    [Fact]
    public async Task CustomersJobs_RegistersExpireKycQuartzJobAndCronTrigger()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddQuartz(q => q.AddCustomersJobs(LimaTimeZone.Instance));

        await using var sp = services.BuildServiceProvider();
        var schedulerFactory = sp.GetRequiredService<ISchedulerFactory>();
        var scheduler = await schedulerFactory.GetScheduler();

        var job = await scheduler.GetJobDetail(new JobKey("ExpireKycJob"));
        job.ShouldNotBeNull();

        var trigger = await scheduler.GetTrigger(new TriggerKey("ExpireKycTrigger")) as ICronTrigger;
        trigger.ShouldNotBeNull();
        trigger.CronExpressionString.ShouldBe("0 0 2 * * ?");
    }

    [Fact]
    public async Task DbMigrator_AppliesMigrations19To23AndSeed24_CreatesSchemaConsentsAndPassportRule()
    {
        if (!_dockerAvailable || _postgresContainer is null) return;

        var cs = await UpgradeDatabaseAsync();
        await using var conn = new NpgsqlConnection(cs);

        var tables = (await conn.QueryAsync<string>(
            "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public'")).ToHashSet();

        tables.ShouldContain("kyc_verifications");
        tables.ShouldContain("consent_texts");
        tables.ShouldContain("customer_consents");
        tables.ShouldContain("customer_documents");
        tables.ShouldContain("otp_verifications");
        tables.ShouldContain("customer_bank_accounts");

        var currentPurposes = (await conn.QueryAsync<string>(
            "SELECT purpose::text FROM consent_texts WHERE is_current = TRUE")).ToHashSet();
        currentPurposes.ShouldBe(["DATA_PROCESSING", "CREDIT_BUREAU_QUERY", "MARKETING"], ignoreOrder: true);

        var passportActive = await conn.ExecuteScalarAsync<bool>(
            "SELECT is_active FROM document_type_rules WHERE doc_type::text = 'PASAPORTE'");
        passportActive.ShouldBeTrue();

        var normalized = await conn.ExecuteScalarAsync<string>("SELECT unaccent('María Quispe')");
        normalized.ShouldBe("Maria Quispe");

        var indexes = (await conn.QueryAsync<string>(
            "SELECT indexname FROM pg_indexes WHERE schemaname = 'public'")).ToHashSet();
        indexes.ShouldContain("ux_customer_bank_accounts_primary");
        indexes.ShouldContain("ux_customer_bank_accounts_number");
        indexes.ShouldContain("ux_customer_documents_current");
        indexes.ShouldContain("ux_customer_consents_active");
    }

    [Fact]
    public async Task EndToEnd_RegisterConcurrentDuplicate_EncryptsCci_AuditsCriticalUpdateAndDocView_AndExpiresKyc()
    {
        if (!_dockerAvailable || _postgresContainer is null) return;

        var cs = await UpgradeDatabaseAsync();
        var operatorId = await SeedUserAsync(cs, "operator.cus@loan.test", "StrongUserPass!2026", "ROLE_OPERATOR");
        var analystId = await SeedUserAsync(cs, "analyst.cus@loan.test", "StrongUserPass!2026", "ROLE_ANALYST");
        var tempStorage = Path.Combine(Path.GetTempPath(), $"loansystem-it-storage-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempStorage);

        try
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment(Environments.Development);
                builder.UseSetting("Secrets:Provider", "None");
                builder.UseSetting("ConnectionStrings:Database", cs);
                builder.UseSetting("ConnectionStrings:Redis", "");
                builder.UseSetting("Customers:Storage:Provider", "Local");
                builder.UseSetting("Customers:Storage:LocalPath", tempStorage);
            });

            using var operatorClient = factory.CreateClient();
            var opToken = await LoginAndGetTokenAsync(operatorClient, "operator.cus@loan.test", "StrongUserPass!2026");
            operatorClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", opToken);

            // 1) Concurrent duplicate document registration -> 1x 201, 1x 409 (CA-01.e)
            var registerPayload = new RegisterCustomerRequest(
                "NATURAL", "DNI", "45678912", "Rosa Medina", new DateOnly(1992, 3, 14),
                "rosa.medina@loan.test", "987654321", null, "Av. Arequipa 123", "Lince",
                3500m, "Contadora", "Estudio Contable", 36,
                [new ConsentAcceptance("DATA_PROCESSING", "v1")]);

            async Task<HttpResponseMessage> PostCustomerAsync()
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, "/api/customers")
                {
                    Content = JsonContent.Create(registerPayload)
                };
                req.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
                return await operatorClient.SendAsync(req);
            }

            var responses = await Task.WhenAll(PostCustomerAsync(), PostCustomerAsync());
            responses.Count(r => r.StatusCode == HttpStatusCode.Created).ShouldBe(1);
            responses.Count(r => r.StatusCode == HttpStatusCode.Conflict).ShouldBe(1);

            var createdResp = responses.Single(r => r.StatusCode == HttpStatusCode.Created);
            createdResp.Headers.Location.ShouldNotBeNull();
            using var createdDoc = JsonDocument.Parse(await createdResp.Content.ReadAsStringAsync());
            var customerId = createdDoc.RootElement.GetProperty("id").GetGuid();

            // 2) Update critical field with If-Match -> 200, updated ETag, UPDATE_CRITICAL audit row
            using (var patchReq = new HttpRequestMessage(HttpMethod.Patch, $"/api/customers/{customerId}")
            {
                Content = JsonContent.Create(new UpdateCustomerRequest(
                    new ContactPatch(null, "999888777", null),
                    null,
                    new SocioeconomicProfilePatch(4200m, null, null, null)))
            })
            {
                patchReq.Headers.TryAddWithoutValidation("If-Match", "\"0\"");
                var patchResp = await operatorClient.SendAsync(patchReq);
                patchResp.StatusCode.ShouldBe(HttpStatusCode.OK);
                patchResp.Headers.ETag?.ToString().ShouldBe("\"1\"");
            }

            // 3) Upload document & request signed URL -> explicit VIEW_DOCUMENT audit row (CA-02.a)
            Guid docId;
            using (var form = new MultipartFormDataContent())
            {
                var pngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x01 };
                var fileContent = new ByteArrayContent(pngBytes);
                fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");
                form.Add(fileContent, "file", "dni_front.png");
                form.Add(new StringContent("DNI_FRONTAL"), "category");

                var upResp = await operatorClient.PostAsync($"/api/customers/{customerId}/documents", form);
                upResp.StatusCode.ShouldBe(HttpStatusCode.Created);
                using var upJson = JsonDocument.Parse(await upResp.Content.ReadAsStringAsync());
                docId = upJson.RootElement.GetProperty("id").GetGuid();
            }

            var urlResp = await operatorClient.GetAsync($"/api/customers/{customerId}/documents/{docId}/url");
            urlResp.StatusCode.ShouldBe(HttpStatusCode.OK);

            // 4) Register bank account -> encrypted CCI stored, plaintext cci is NULL in DB (CA-09.d)
            const string rawCci = "00219100000000001234";
            using (var acctReq = new HttpRequestMessage(HttpMethod.Post, $"/api/customers/{customerId}/bank-accounts")
            {
                Content = JsonContent.Create(new RegisterBankAccountRequest("BCP", "CUENTA_AHORRO", rawCci, null, "Rosa Medina Flores"))
            })
            {
                acctReq.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
                var acctResp = await operatorClient.SendAsync(acctReq);
                acctResp.StatusCode.ShouldBe(HttpStatusCode.Created);
                var acctBody = await acctResp.Content.ReadAsStringAsync();
                acctBody.ShouldNotContain(rawCci);
            }

            await using (var conn = new NpgsqlConnection(cs))
            {
                var dbRow = await conn.QuerySingleAsync<(string? Cci, string? Enc, string? Hash, string Last4)>(
                    "SELECT cci, account_number_encrypted AS Enc, account_number_hash AS Hash, last4 AS Last4 FROM customer_bank_accounts WHERE customer_id = @CustomerId",
                    new { CustomerId = customerId });
                dbRow.Cci.ShouldBeNull();
                dbRow.Enc.ShouldNotBeNullOrWhiteSpace();
                dbRow.Enc.ShouldNotContain(rawCci);
                dbRow.Hash.ShouldNotBeNullOrWhiteSpace();
                dbRow.Hash.ShouldNotBe(rawCci);
                dbRow.Last4.ShouldBe("1234");

                var auditActions = (await conn.QueryAsync<string>(
                    "SELECT action FROM audit_logs WHERE user_id = @UserId",
                    new { UserId = operatorId })).ToHashSet();
                auditActions.ShouldContain("UPDATE_CRITICAL");
                auditActions.ShouldContain("VIEW_DOCUMENT");

                var leakedCciAudit = await conn.ExecuteScalarAsync<long>(
                    "SELECT COUNT(*) FROM audit_logs WHERE state_after::text LIKE @Pattern OR state_before::text LIKE @Pattern",
                    new { Pattern = $"%{rawCci}%" });
                leakedCciAudit.ShouldBe(0);
            }

            // 5) ROLE_CLIENT JWT includes customer_id claim when portal_user_id is linked (R7)
            await SeedUserAsync(cs, "client.cus@loan.test", "StrongUserPass!2026", "ROLE_CLIENT", linkCustomerId: customerId);
            using var clientPortal = factory.CreateClient();
            var clientToken = await LoginAndGetTokenAsync(clientPortal, "client.cus@loan.test", "StrongUserPass!2026");
            var clientJwt = new JwtSecurityTokenHandler().ReadJwtToken(clientToken);
            clientJwt.Claims.FirstOrDefault(c => c.Type == "customer_id")?.Value.ShouldBe(customerId.ToString());

            // 6) ExpireKycUseCase marks overdue verified customer EXPIRED and emits outbox event (CA-04.d)
            await using (var conn = new NpgsqlConnection(cs))
            {
                await conn.ExecuteAsync(
                    """
                    UPDATE customers
                       SET kyc_status = 'VERIFIED',
                           kyc_verified_by = @AnalystId,
                           kyc_verified_at = NOW() - interval '400 days',
                           kyc_expires_at = NOW() - interval '2 days'
                     WHERE id = @CustomerId
                    """,
                    new { CustomerId = customerId, AnalystId = analystId });
            }

            using (var jobScope = factory.Services.CreateScope())
            {
                var expireUseCase = jobScope.ServiceProvider.GetRequiredService<IExpireKycUseCase>();
                var expireRes = await expireUseCase.ExecuteAsync(CancellationToken.None);
                expireRes.IsSuccess.ShouldBeTrue();
                expireRes.Value.ShouldBeGreaterThanOrEqualTo(1);
            }

            await using (var conn = new NpgsqlConnection(cs))
            {
                var status = await conn.ExecuteScalarAsync<string>(
                    "SELECT kyc_status::text FROM customers WHERE id = @CustomerId",
                    new { CustomerId = customerId });
                status.ShouldBe("EXPIRED");

                var outboxCount = await conn.ExecuteScalarAsync<long>(
                    "SELECT COUNT(*) FROM outbox_messages WHERE event_type = @Type",
                    new { Type = typeof(CustomerKycExpiredIntegrationEvent).FullName });
                outboxCount.ShouldBeGreaterThanOrEqualTo(1);
            }
        }
        finally
        {
            if (Directory.Exists(tempStorage))
            {
                Directory.Delete(tempStorage, recursive: true);
            }
        }
    }

    private async Task<string> UpgradeDatabaseAsync()
    {
        var cs = _postgresContainer!.GetConnectionString();
        var migration = LoanSystem.DbMigrator.Program.Upgrade(cs, LoanSystem.DbMigrator.Program.FindDatabaseDirectory()!);
        migration.Successful.ShouldBeTrue($"DbMigrator failed with: {migration.Error}");
        return cs;
    }

    private static async Task<Guid> SeedUserAsync(string cs, string email, string password, string role, Guid? linkCustomerId = null)
    {
        var id = Guid.NewGuid();
        var hash = BCrypt.Net.BCrypt.HashPassword(password);
        await using var conn = new NpgsqlConnection(cs);
        await conn.ExecuteAsync(
            """
            INSERT INTO users (id, email, password_hash, full_name, role, status)
            VALUES (@Id, @Email, @Hash, @Email, @Role::user_role, 'ACTIVE')
            """,
            new { Id = id, Email = email, Hash = hash, Role = role });

        if (linkCustomerId.HasValue)
        {
            await conn.ExecuteAsync(
                "UPDATE customers SET portal_user_id = @UserId WHERE id = @CustomerId",
                new { UserId = id, CustomerId = linkCustomerId.Value });
        }

        return id;
    }

    private static async Task<string> LoginAndGetTokenAsync(HttpClient client, string email, string password)
    {
        var resp = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        resp.StatusCode.ShouldBe(HttpStatusCode.OK, await resp.Content.ReadAsStringAsync());
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("accessToken").GetString()!;
    }
}
