using LoanSystem.Common.Core.Application;
using Microsoft.AspNetCore.Authorization;

namespace LoanSystem.Common.Adapters.Http;

/// <summary>
/// Registers all shared role and composite authorization policies for WebApi and integration tests.
/// </summary>
public static class AuthorizationPolicyExtensions
{
    public static AuthorizationOptions AddLoanSystemAuthorizationPolicies(this AuthorizationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.AddPolicy(SecurityPolicies.Admin, p => p.RequireRole(SecurityPolicies.Admin));
        options.AddPolicy(SecurityPolicies.Analyst, p => p.RequireRole(SecurityPolicies.Admin, SecurityPolicies.Analyst));
        options.AddPolicy(SecurityPolicies.Operator, p => p.RequireRole(SecurityPolicies.Admin, SecurityPolicies.Operator));
        options.AddPolicy(SecurityPolicies.Collector, p => p.RequireRole(SecurityPolicies.Admin, SecurityPolicies.Collector));
        options.AddPolicy(SecurityPolicies.Auditor, p => p.RequireRole(SecurityPolicies.Admin, SecurityPolicies.Auditor));
        options.AddPolicy(SecurityPolicies.Client, p => p.RequireRole(SecurityPolicies.Client));

        options.AddPolicy(SecurityPolicies.CustomersRead, p => p.RequireRole(
            SecurityPolicies.Admin, SecurityPolicies.Analyst, SecurityPolicies.Operator, SecurityPolicies.Collector, SecurityPolicies.Auditor));
        options.AddPolicy(SecurityPolicies.CustomersReadOrClient, p => p.RequireRole(
            SecurityPolicies.Admin, SecurityPolicies.Analyst, SecurityPolicies.Operator, SecurityPolicies.Collector, SecurityPolicies.Auditor, SecurityPolicies.Client));
        options.AddPolicy(SecurityPolicies.CustomersWrite, p => p.RequireRole(
            SecurityPolicies.Admin, SecurityPolicies.Analyst, SecurityPolicies.Operator));
        options.AddPolicy(SecurityPolicies.CustomersWriteOrClient, p => p.RequireRole(
            SecurityPolicies.Admin, SecurityPolicies.Analyst, SecurityPolicies.Operator, SecurityPolicies.Client));
        options.AddPolicy(SecurityPolicies.CustomersAuditRead, p => p.RequireRole(
            SecurityPolicies.Admin, SecurityPolicies.Analyst, SecurityPolicies.Auditor));
        options.AddPolicy(SecurityPolicies.CustomersDocsReadOrClient, p => p.RequireRole(
            SecurityPolicies.Admin, SecurityPolicies.Analyst, SecurityPolicies.Operator, SecurityPolicies.Auditor, SecurityPolicies.Client));
        options.AddPolicy(SecurityPolicies.CustomersAccountsManageOrClient, p => p.RequireRole(
            SecurityPolicies.Admin, SecurityPolicies.Operator, SecurityPolicies.Client));

        options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();

        return options;
    }
}
