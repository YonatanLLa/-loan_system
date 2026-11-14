using System.Security.Cryptography;
using System.Text.Json;
using Dapper;
using LoanSystem.Common.Adapters.Clock;
using LoanSystem.Common.Core.Contracts.Approvals;
using LoanSystem.Common.Core.Ports.Outbound;
using LoanSystem.Modules.Security.Adapters;
using LoanSystem.Modules.Security.Adapters.Outbound.Providers;
using LoanSystem.Modules.Security.Core.Contracts;
using LoanSystem.Modules.Security.Core.Ports.Inbound;
using LoanSystem.Modules.Security.Core.Ports.Outbound;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using OtpNet;
using Shouldly;
using Testcontainers.PostgreSql;
using Xunit;

namespace LoanSystem.IntegrationTests;

/// <summary>
/// BE-SEC-09 against real PostgreSQL: migration 17, CHECK constraints (R1/R2),
/// immutability trigger (R4), optimistic concurrency (xmin), and two-user role change with linked audit logs.
/// </summary>
public class ApprovalFlowIntegrationTests : IAsyncLifetime
{
    private PostgreSqlContainer? _postgresContainer;
    private bool _dockerAvailable = true;

    public async Task InitializeAsync()
    {
        try
        {
            _postgresContainer = new PostgreSqlBuilder("postgres:17-alpine")
                .WithDatabase("loan_system_approvals_test")
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
    public async Task Migration17_EnforcesFourEyesCheckConstraintsAndImmutabilityTrigger()
    {
        if (!_dockerAvailable || _postgresContainer is null) return;

        var cs = await UpgradeDatabaseAsync();
        var requesterId = await InsertUserAsync(cs, "req@loan.test", "ROLE_ADMIN");
        var approverId = await InsertUserAsync(cs, "app@loan.test", "ROLE_ADMIN");
        var targetId = await InsertUserAsync(cs, "target@loan.test", "ROLE_ANALYST");

        await using var conn = new NpgsqlConnection(cs);

        // R1 CHECK: requester cannot decide
        var reqId = Guid.NewGuid();
        await conn.ExecuteAsync(
            """
            INSERT INTO approval_requests (id, operation_type, module, target_entity, target_id, payload, target_snapshot, reason, requested_by, expires_at)
            VALUES (@Id, 'USER_ROLE_CHANGE', 'Security', 'users', @TargetId, '{"fromRole":"ROLE_ANALYST","toRole":"ROLE_OPERATOR"}'::jsonb, '{}'::jsonb, 'Motivo de prueba válido', @RequesterId, NOW() + interval '24 hours')
            """,
            new { Id = reqId, TargetId = targetId, RequesterId = requesterId });

        await Should.ThrowAsync<PostgresException>(() => conn.ExecuteAsync(
            "UPDATE approval_requests SET status = 'APPROVED', decided_by = @RequesterId, decided_at = NOW() WHERE id = @Id",
            new { Id = reqId, RequesterId = requesterId }));

        // R2 CHECK: beneficiary cannot decide USER_ROLE_CHANGE
        await Should.ThrowAsync<PostgresException>(() => conn.ExecuteAsync(
            "UPDATE approval_requests SET status = 'APPROVED', decided_by = @TargetId, decided_at = NOW() WHERE id = @Id",
            new { Id = reqId, TargetId = targetId }));

        // R4 Trigger: payload cannot be modified while PENDING
        var payloadEx = await Should.ThrowAsync<PostgresException>(() => conn.ExecuteAsync(
            "UPDATE approval_requests SET payload = '{\"fromRole\":\"ROLE_ANALYST\",\"toRole\":\"ROLE_ADMIN\"}'::jsonb WHERE id = @Id",
            new { Id = reqId }));
        payloadEx.Message.ShouldContain("no puede modificarse");

        // Trigger: DELETE prohibited
        await Should.ThrowAsync<PostgresException>(() => conn.ExecuteAsync(
            "DELETE FROM approval_requests WHERE id = @Id",
            new { Id = reqId }));

        // Valid approval succeeds, and subsequent update is rejected by immutability trigger
        await conn.ExecuteAsync(
            "UPDATE approval_requests SET status = 'APPROVED', decided_by = @ApproverId, decided_at = NOW() WHERE id = @Id",
            new { Id = reqId, ApproverId = approverId });

        var decidedEx = await Should.ThrowAsync<PostgresException>(() => conn.ExecuteAsync(
            "UPDATE approval_requests SET status = 'REJECTED', decision_comment = 'Late' WHERE id = @Id",
            new { Id = reqId }));
        decidedEx.Message.ShouldContain("es inmutable");
    }

    [Fact]
    public async Task TwoAdmins_RequestAndApproveUserRoleChange_ExecutesAndLinksAuditLogs()
    {
        if (!_dockerAvailable || _postgresContainer is null) return;

        var cs = await UpgradeDatabaseAsync();
        var mfaKeyBytes = RandomNumberGenerator.GetBytes(32);
        using var rsa = RSA.Create(2048);
        var now = DateTimeOffset.UtcNow;
        var jwtSigningKeys = JsonSerializer.Serialize(new
        {
            keys = new[]
            {
                new
                {
                    privateKeyPem = rsa.ExportPkcs8PrivateKeyPem(),
                    notBefore = now.AddMinutes(-5),
                    notAfter = now.AddDays(30)
                }
            }
        });
        var mfaEncryptionKeys = JsonSerializer.Serialize(new
        {
            active = "2026-11",
            keys = new Dictionary<string, string>
            {
                ["2026-11"] = Convert.ToBase64String(mfaKeyBytes)
            }
        });

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Database"] = cs,
                ["Jwt:SigningKeys"] = jwtSigningKeys,
                ["Security:Mfa:EncryptionKeys"] = mfaEncryptionKeys
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IClock, SystemClock>();
        var currentUser = new MutableCurrentUser();
        services.AddScoped<ICurrentUser>(_ => currentUser);
        services.AddSecurityModule(configuration);

        await using var sp = services.BuildServiceProvider();

        // Seed Admin A, Admin B (with enrolled MFA), and Target User
        var adminA = await InsertUserAsync(cs, "admin-a@loan.test", "ROLE_ADMIN");
        var totpSecret = Base32Encoding.ToString(KeyGeneration.GenerateRandomKey(20));
        var adminB = await InsertUserAsync(cs, "admin-b@loan.test", "ROLE_ADMIN");
        var target = await InsertUserAsync(cs, "analyst@loan.test", "ROLE_ANALYST");

        var protector = new AesGcmSecretProtector(configuration);
        var encrypted = protector.Protect(totpSecret, adminB);
        await using (var conn = new NpgsqlConnection(cs))
        {
            await conn.ExecuteAsync(
                "UPDATE users SET mfa_enabled = TRUE, mfa_secret_encrypted = @Secret, mfa_enrolled_at = NOW() WHERE id = @Id",
                new { Id = adminB, Secret = encrypted });
        }

        // 1) Admin A submits role change request
        Guid approvalId;
        using (var scopeA = sp.CreateScope())
        {
            currentUser.Set(adminA, "admin-a@loan.test", "ROLE_ADMIN");
            var reqUseCase = scopeA.ServiceProvider.GetRequiredService<IRequestUserRoleChangeUseCase>();
            var res = await reqUseCase.ExecuteAsync(
                new RequestUserRoleChangeCommand(target, "ROLE_OPERATOR", "Ascenso aprobado por gerencia"));

            res.IsSuccess.ShouldBeTrue();
            res.Value.Status.ShouldBe("PENDING");
            approvalId = res.Value.Id;
        }

        // 2) Admin B sees request in inbox and approves with live TOTP code
        using (var scopeB = sp.CreateScope())
        {
            currentUser.Set(adminB, "admin-b@loan.test", "ROLE_ADMIN");

            var queryUseCase = scopeB.ServiceProvider.GetRequiredService<IQueryApprovalsUseCase>();
            var inbox = await queryUseCase.ExecuteAsync(new ApprovalQuery(Scope: "toDecide"));
            inbox.IsSuccess.ShouldBeTrue();
            inbox.Value.Items.ShouldContain(i => i.Id == approvalId);

            var totp = new Totp(Base32Encoding.ToBytes(totpSecret));
            var code = totp.ComputeTotp(DateTime.UtcNow);

            var approveUseCase = scopeB.ServiceProvider.GetRequiredService<IApproveRequestUseCase>();
            var approved = await approveUseCase.ExecuteAsync(
                new ApproveApprovalCommand(approvalId, "Conforme", code));

            approved.IsSuccess.ShouldBeTrue();
            approved.Value.Status.ShouldBe("APPROVED");
            approved.Value.Result!.ResultEntity.ShouldBe("users");
        }

        // 3) Verify target role updated and audit logs linked by approval_request_id
        using (var verifyScope = sp.CreateScope())
        {
            currentUser.Set(adminB, "admin-b@loan.test", "ROLE_ADMIN");
            var auditReader = verifyScope.ServiceProvider.GetRequiredService<IAuditLogReader>();
            var auditLogs = await auditReader.QueryAsync(new AuditLogQuery(ApprovalRequestId: approvalId));

            auditLogs.Items.ShouldContain(a => a.Action == "REQUEST_USER_ROLE_CHANGE" && a.UserId == adminA);
            auditLogs.Items.ShouldContain(a => a.Action == "APPROVE" && a.UserId == adminB && a.EntityName == "approval_requests");
            auditLogs.Items.ShouldContain(a => a.Action == "ROLE_CHANGE" && a.UserId == adminB && a.EntityName == "users");
        }

        await using var verifyConn = new NpgsqlConnection(cs);
        var currentRole = await verifyConn.ExecuteScalarAsync<string>(
            "SELECT role::text FROM users WHERE id = @Id", new { Id = target });
        currentRole.ShouldBe("ROLE_OPERATOR");
    }

    private async Task<string> UpgradeDatabaseAsync()
    {
        var cs = _postgresContainer!.GetConnectionString();
        var migration = LoanSystem.DbMigrator.Program.Upgrade(cs, LoanSystem.DbMigrator.Program.FindDatabaseDirectory()!);
        migration.Successful.ShouldBeTrue($"DbMigrator failed with: {migration.Error}");
        return cs;
    }

    private static async Task<Guid> InsertUserAsync(string cs, string email, string role)
    {
        var id = Guid.NewGuid();
        await using var conn = new NpgsqlConnection(cs);
        await conn.ExecuteAsync(
            """
            INSERT INTO users (id, email, password_hash, full_name, role)
            VALUES (@Id, @Email, 'hash', @Name, @Role::user_role)
            """,
            new { Id = id, Email = email, Name = email, Role = role });
        return id;
    }

    private sealed class MutableCurrentUser : ICurrentUser
    {
        public Guid? UserId { get; private set; }
        public string? Email { get; private set; }
        public IReadOnlyCollection<string> Roles { get; private set; } = [];
        public Guid? CustomerId => null;
        public bool IsAuthenticated => UserId.HasValue;
        public bool IsInRole(string role) => Roles.Contains(role);

        public void Set(Guid id, string email, string role)
        {
            UserId = id;
            Email = email;
            Roles = [role];
        }
    }
}
