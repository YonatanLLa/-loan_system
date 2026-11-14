using System.Security.Cryptography;
using System.Text;
using LoanSystem.Common.Adapters.Crypto;
using LoanSystem.Modules.Customers.Core.Contracts.IntegrationEvents;
using LoanSystem.Modules.Customers.Core.Domain;
using LoanSystem.Modules.Customers.Core.Ports.Outbound;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LoanSystem.Modules.Customers.Adapters.Outbound.Providers;

internal sealed class HmacOtpHasher : IOtpHasher
{
    private readonly byte[] _pepper;

    public HmacOtpHasher(IConfiguration configuration, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        var configured = configuration["Customers:Otp:Pepper"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            _pepper = Encoding.UTF8.GetBytes(configured);
        }
        else if (environment.IsDevelopment() || string.Equals(environment.EnvironmentName, "Testing", StringComparison.OrdinalIgnoreCase))
        {
            _pepper = SHA256.HashData(
                Encoding.UTF8.GetBytes($"{environment.ApplicationName}:{environment.EnvironmentName}:dev-otp-salt-v1"));
        }
        else
        {
            throw new InvalidOperationException(
                "Customers:Otp:Pepper must be configured in non-development environments.");
        }
    }

    public string Hash(string rawCode)
    {
        ArgumentException.ThrowIfNullOrEmpty(rawCode);
        var digest = HMACSHA256.HashData(_pepper, Encoding.UTF8.GetBytes(rawCode));
        return Convert.ToHexString(digest).ToLowerInvariant();
    }

    public bool Verify(string rawCode, string storedHash)
    {
        if (string.IsNullOrEmpty(rawCode) || string.IsNullOrWhiteSpace(storedHash))
        {
            return false;
        }

        var computed = Hash(rawCode);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(computed),
            Encoding.ASCII.GetBytes(storedHash));
    }
}

internal sealed class OutboxOtpSender : IOtpSender
{
    private readonly ICustomersOutbox _outbox;

    public OutboxOtpSender(ICustomersOutbox outbox) => _outbox = outbox;

    public Task SendAsync(
        Guid customerId,
        OtpChannel channel,
        string target,
        string code,
        OtpPurpose purpose,
        CancellationToken ct = default)
    {
        _outbox.Enqueue(new OtpDeliveryRequestedIntegrationEvent(
            customerId,
            channel.ToString(),
            target,
            code,
            purpose.ToString()));

        return Task.CompletedTask;
    }
}

internal sealed class CustomersFieldProtectorAdapter : IFieldProtector
{
    private readonly AesGcmFieldProtector _inner;

    public CustomersFieldProtectorAdapter(AesGcmFieldProtector inner) => _inner = inner;

    public string Protect(string plaintext) => _inner.Protect(plaintext);
    public string Unprotect(string protectedValue) => _inner.Unprotect(protectedValue);
    public string Hash(string plaintext) => _inner.Hash(plaintext);
}

internal sealed class CustomerLoggerDiagnostics : ICustomerDiagnostics
{
    private readonly ILogger<CustomerLoggerDiagnostics> _logger;

    public CustomerLoggerDiagnostics(ILogger<CustomerLoggerDiagnostics> logger) => _logger = logger;

    public void LogOrphanedDocumentObject(string storageKey, Guid customerId, Exception exception) =>
        _logger.LogWarning(
            exception,
            "Database commit failed after uploading document object {StorageKey} for customer {CustomerId}. Object retained for cleanup.",
            storageKey,
            customerId);

    public void LogKycCheckAttemptFailure(
        KycCheckType checkType,
        Guid customerId,
        int attempt,
        int maxAttempts,
        Exception exception) =>
        _logger.LogWarning(
            exception,
            "KYC verification check {CheckType} failed for customer {CustomerId} on attempt {Attempt}/{MaxAttempts}.",
            checkType,
            customerId,
            attempt,
            maxAttempts);
}
