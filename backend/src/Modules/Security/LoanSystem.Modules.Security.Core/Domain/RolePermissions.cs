namespace LoanSystem.Modules.Security.Core.Domain;

/// <summary>Maps each <see cref="SecurityRole"/> to its effective permission set (BE-SEC-04, BE-SEC-05).</summary>
internal static class RolePermissions
{
    private static readonly string[] AdminPermissions =
    [
        "dashboard.view",
        "customers.read",
        "applications.read",
        "loans.read",
        "payments.read",
        "payments.reverse",
        "treasury.read",
        "integrations.read",
        "collections.read",
        "reports.read",
        "audit.read",
        "products.manage",
        "admin.manage",
        "approvals.read",
        "approvals.payments.reverse",
        "approvals.loans.write_off",
        "approvals.loans.adjust",
        "approvals.products.rates",
        "approvals.loans.restructure",
        "approvals.users.role"
    ];

    private static readonly string[] AnalystPermissions =
    [
        "dashboard.view",
        "customers.read",
        "applications.read",
        "loans.read",
        "reports.read"
    ];

    private static readonly string[] OperatorPermissions =
    [
        "dashboard.view",
        "customers.read",
        "applications.read",
        "loans.read",
        "payments.read",
        "treasury.read",
        "integrations.read",
        "reports.read"
    ];

    private static readonly string[] CollectorPermissions =
    [
        "dashboard.view",
        "customers.read",
        "loans.read",
        "collections.read"
    ];

    private static readonly string[] AuditorPermissions =
    [
        "dashboard.view",
        "audit.read",
        "reports.read",
        "customers.read",
        "loans.read",
        "payments.read",
        "treasury.read",
        "approvals.read"
    ];

    private static readonly string[] ClientPermissions =
    [
        "client.portal"
    ];

    public static IReadOnlyCollection<string> GetForRole(SecurityRole role) =>
        role switch
        {
            SecurityRole.ROLE_ADMIN => AdminPermissions,
            SecurityRole.ROLE_ANALYST => AnalystPermissions,
            SecurityRole.ROLE_OPERATOR => OperatorPermissions,
            SecurityRole.ROLE_COLLECTOR => CollectorPermissions,
            SecurityRole.ROLE_AUDITOR => AuditorPermissions,
            SecurityRole.ROLE_CLIENT => ClientPermissions,
            _ => []
        };

    public static bool HasPermission(SecurityRole role, string permission) =>
        GetForRole(role).Contains(permission, StringComparer.Ordinal);
}
