namespace LoanSystem.Modules.Security.Core.Domain;

/// <summary>RBAC roles. Member names match the PostgreSQL <c>user_role</c> enum labels exactly.</summary>
internal enum SecurityRole
{
    ROLE_ADMIN,
    ROLE_ANALYST,
    ROLE_OPERATOR,
    ROLE_COLLECTOR,
    ROLE_AUDITOR,
    ROLE_CLIENT
}

/// <summary>Account status. Member names match the PostgreSQL <c>user_status_type</c> enum labels exactly.</summary>
internal enum UserStatus
{
    ACTIVE,
    INACTIVE,
    LOCKED
}
