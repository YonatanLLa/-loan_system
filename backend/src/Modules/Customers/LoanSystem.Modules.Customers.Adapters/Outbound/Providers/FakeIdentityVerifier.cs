using LoanSystem.Modules.Customers.Core.Domain;
using LoanSystem.Modules.Customers.Core.Ports.Outbound;

namespace LoanSystem.Modules.Customers.Adapters.Outbound.Providers;

/// <summary>
/// Deterministic non-production KYC verification provider (§14).
/// Doc numbers ending in <c>0</c> fail identity/biometric checks; ending in <c>9</c> trigger PEP manual review.
/// </summary>
internal sealed class FakeIdentityVerifier : IIdentityVerifier
{
    public string ProviderName => "FAKE";

    public Task<KycCheckOutcome> VerifyRegistryAsync(IdentityRegistryQuery query, CancellationToken ct = default)
    {
        var extRef = $"FAKE-REG-{query.CustomerId:N}"[..24];
        if (query.DocNumber.EndsWith('0'))
        {
            return Task.FromResult(new KycCheckOutcome(
                KycCheckType.IDENTITY_REGISTRY,
                KycCheckResult.FAIL,
                0m,
                extRef,
                "{\"provider\":\"FAKE\",\"matched\":false,\"reason\":\"doc_ends_with_0\"}",
                "Identity registry record mismatch (deterministic test rule)."));
        }

        return Task.FromResult(new KycCheckOutcome(
            KycCheckType.IDENTITY_REGISTRY,
            KycCheckResult.PASS,
            96.50m,
            extRef,
            "{\"provider\":\"FAKE\",\"matched\":true}",
            null));
    }

    public Task<KycCheckOutcome> VerifyLivenessAsync(BiometricQuery query, CancellationToken ct = default)
    {
        var extRef = $"FAKE-BIO-{query.CustomerId:N}"[..24];
        if (query.DocNumber.EndsWith('0'))
        {
            return Task.FromResult(new KycCheckOutcome(
                KycCheckType.BIOMETRIC_LIVENESS,
                KycCheckResult.FAIL,
                25.00m,
                extRef,
                "{\"provider\":\"FAKE\",\"livenessPassed\":false}",
                "Biometric match below threshold (deterministic test rule)."));
        }

        return Task.FromResult(new KycCheckOutcome(
            KycCheckType.BIOMETRIC_LIVENESS,
            KycCheckResult.PASS,
            95.00m,
            extRef,
            "{\"provider\":\"FAKE\",\"livenessPassed\":true}",
            null));
    }

    public Task<KycCheckOutcome> ScreenWatchlistsAsync(WatchlistQuery query, CancellationToken ct = default)
    {
        var extRef = $"FAKE-PEP-{query.CustomerId:N}"[..24];
        if (query.DocNumber.EndsWith('9'))
        {
            return Task.FromResult(new KycCheckOutcome(
                KycCheckType.PEP_SCREENING,
                KycCheckResult.REVIEW,
                70.00m,
                extRef,
                "{\"provider\":\"FAKE\",\"isPep\":true,\"sanctionsHit\":false}",
                "PEP match requires analyst/admin review.",
                IsPepFlag: true));
        }

        return Task.FromResult(new KycCheckOutcome(
            KycCheckType.PEP_SCREENING,
            KycCheckResult.PASS,
            99.00m,
            extRef,
            "{\"provider\":\"FAKE\",\"isPep\":false,\"sanctionsHit\":false}",
            null));
    }
}
