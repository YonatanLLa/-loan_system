namespace LoanSystem.Common.Core.Application;

public static class SecurityPolicies
{
    public const string Admin = "ROLE_ADMIN";
    public const string Analyst = "ROLE_ANALYST";
    public const string Operator = "ROLE_OPERATOR";
    public const string Collector = "ROLE_COLLECTOR";
    public const string Auditor = "ROLE_AUDITOR";
    public const string Client = "ROLE_CLIENT";

    // Combined policies matching endpoint authorization matrices (e.g. Customers §9)
    public const string CustomersRead = "CustomersRead";
    public const string CustomersReadOrClient = "CustomersReadOrClient";
    public const string CustomersWrite = "CustomersWrite";
    public const string CustomersWriteOrClient = "CustomersWriteOrClient";
    public const string CustomersAuditRead = "CustomersAuditRead";
    public const string CustomersDocsReadOrClient = "CustomersDocsReadOrClient";
    public const string CustomersAccountsManageOrClient = "CustomersAccountsManageOrClient";
}
