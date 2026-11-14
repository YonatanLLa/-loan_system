using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LoanSystem.Modules.Security.Adapters.Outbound.Providers;

/// <summary>
/// Reports the operational health of the RS256 signing key ring without exposing secret key material (BE-SEC-10).
/// </summary>
internal sealed class JwtKeyRingHealthCheck : IHealthCheck
{
    private readonly IJwtKeyRing _keyRing;

    public JwtKeyRingHealthCheck(IJwtKeyRing keyRing)
    {
        _keyRing = keyRing;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var status = _keyRing.GetStatus();
            var data = new Dictionary<string, object>
            {
                ["validationKeyCount"] = status.ValidationKeyCount
            };

            if (status.ActiveKid is not null)
            {
                data["activeKid"] = status.ActiveKid;
            }

            if (status.ActiveKeyExpiresAt is not null)
            {
                data["activeKeyExpiresAt"] = status.ActiveKeyExpiresAt.Value;
            }

            if (status.NextKeyStartsAt is not null)
            {
                data["nextKeyStartsAt"] = status.NextKeyStartsAt.Value;
            }

            var result = status.State switch
            {
                JwtHealthState.Healthy => HealthCheckResult.Healthy(status.Description, data),
                JwtHealthState.Degraded => HealthCheckResult.Degraded(status.Description, data: data),
                _ => HealthCheckResult.Unhealthy(status.Description, data: data)
            };

            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("JWT key ring failed health inspection.", ex));
        }
    }
}
