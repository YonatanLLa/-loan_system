using System.Net;
using LoanSystem.Common.Core.Application;
using LoanSystem.Common.Core.Domain;
using LoanSystem.Common.Core.Ports.Outbound;

namespace LoanSystem.Modules.Customers.Core.Domain;

internal sealed class CustomerConsent : Entity<Guid>
{
    public Guid CustomerId { get; private set; }
    public Guid ConsentTextId { get; private set; }
    public ConsentPurpose Purpose { get; private set; }
    public DateTimeOffset GrantedAt { get; private set; }
    public IPAddress? GrantedIp { get; private set; }
    public string? UserAgent { get; private set; }
    public string Channel { get; private set; } = "BACKOFFICE";
    public DateTimeOffset? RevokedAt { get; private set; }
    public IPAddress? RevokedIp { get; private set; }

    public ConsentText? ConsentText { get; private set; }

    private CustomerConsent() { }

    public static CustomerConsent Grant(
        Guid customerId,
        ConsentText text,
        IPAddress? grantedIp,
        string? userAgent,
        string channel,
        IClock clock) =>
        new()
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            ConsentTextId = text.Id,
            Purpose = text.Purpose,
            GrantedAt = clock.UtcNow,
            GrantedIp = grantedIp,
            UserAgent = userAgent,
            Channel = channel,
            RevokedAt = null,
            RevokedIp = null,
            ConsentText = text
        };

    public Result Revoke(IPAddress? revokedIp, IClock clock)
    {
        if (Purpose != ConsentPurpose.MARKETING)
        {
            return CustomerErrors.ConsentNotRevocable(Purpose.ToString());
        }

        if (RevokedAt.HasValue)
        {
            return Result.Success();
        }

        RevokedAt = clock.UtcNow;
        RevokedIp = revokedIp;
        return Result.Success();
    }
}
