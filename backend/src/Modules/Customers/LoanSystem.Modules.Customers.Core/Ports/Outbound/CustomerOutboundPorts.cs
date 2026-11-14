using System.Net;
using LoanSystem.Modules.Customers.Core.Domain;

namespace LoanSystem.Modules.Customers.Core.Ports.Outbound;

internal interface IOtpHasher
{
    string Hash(string rawCode);
    bool Verify(string rawCode, string storedHash);
}

internal interface IOtpSender
{
    Task SendAsync(Guid customerId, OtpChannel channel, string target, string code, OtpPurpose purpose, CancellationToken ct = default);
}

internal interface IOtpRepository
{
    void Add(OtpCode otp);
    Task<OtpCode?> FindByIdAsync(Guid otpRequestId, CancellationToken ct = default);
    Task<IReadOnlyList<OtpCode>> FindActiveByCustomerAndPurposeAsync(Guid customerId, OtpPurpose purpose, CancellationToken ct = default);
    Task<OtpCode?> GetLatestByCustomerAndPurposeAsync(Guid customerId, OtpPurpose purpose, CancellationToken ct = default);
    Task<int> CountIssuedSinceAsync(Guid customerId, OtpPurpose purpose, DateTimeOffset sinceUtc, CancellationToken ct = default);
}

internal interface IFieldProtector
{
    string Protect(string plaintext);
    string Unprotect(string protectedValue);
    string Hash(string plaintext);
}

internal interface ICustomerBankAccountRepository
{
    void Add(CustomerBankAccount account);
    Task<CustomerBankAccount?> FindByIdAsync(Guid customerId, Guid accountId, CancellationToken ct = default);
    Task<IReadOnlyList<CustomerBankAccount>> ListByCustomerAsync(Guid customerId, CancellationToken ct = default);
    Task<CustomerBankAccount?> FindPrimaryActiveAsync(Guid customerId, CancellationToken ct = default);
    Task<bool> ExistsActiveByHashAsync(Guid customerId, string accountNumberHash, CancellationToken ct = default);
    Task<int> CountNonInactiveAsync(Guid customerId, CancellationToken ct = default);
}

internal interface IConsentTextRepository
{
    Task<IReadOnlyList<ConsentText>> GetCurrentAllAsync(CancellationToken ct = default);
    Task<ConsentText?> FindCurrentByPurposeAsync(ConsentPurpose purpose, CancellationToken ct = default);
}

internal interface ICustomerConsentRepository
{
    void Add(CustomerConsent consent);
    Task<IReadOnlyList<CustomerConsent>> ListByCustomerAsync(Guid customerId, CancellationToken ct = default);
    Task<CustomerConsent?> FindActiveAsync(Guid customerId, ConsentPurpose purpose, CancellationToken ct = default);
}
