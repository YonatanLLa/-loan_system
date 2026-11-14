using LoanSystem.Common.Core.Domain;
using LoanSystem.Modules.Customers.Core.Contracts.Consents;

namespace LoanSystem.Modules.Customers.Core.Contracts;

public sealed record RegisterCustomerRequest(
    string PersonType,              // NATURAL | JURIDICA
    string DocType,                 // DNI | CE | RUC | PASAPORTE
    string DocNumber,
    string FullName,
    DateOnly? BirthDate,
    string Email,
    string PhonePrimary,
    string? PhoneSecondary,
    string Address,
    string District,
    decimal MonthlyNetIncome,
    string? Occupation,
    string? EmployerName,
    int WorkSeniorityMonths,
    IReadOnlyList<ConsentAcceptance> Consents);

public sealed record ContactPatch(string? Email, string? PhonePrimary, string? PhoneSecondary);

public sealed record AddressPatch(string? Address, string? District);

public sealed record SocioeconomicProfilePatch(
    decimal? MonthlyNetIncome,
    string? Occupation,
    string? EmployerName,
    int? WorkSeniorityMonths);

public sealed record UpdateCustomerRequest(
    ContactPatch? Contact,
    AddressPatch? Address,
    SocioeconomicProfilePatch? Profile,
    int? ExpectedVersion = null);

public sealed record UpdateCustomerCommand(
    Guid CustomerId,
    UpdateCustomerRequest Request,
    int? ExpectedVersion);

public sealed record SearchCustomersRequest(
    string? Q = null,
    string? KycStatus = null,
    string? PersonType = null,
    DateOnly? CreatedFrom = null,
    DateOnly? CreatedTo = null,
    int Page = 1,
    int PageSize = 20);

public sealed record GetCustomerHistoryRequest(
    Guid CustomerId,
    int Page = 1,
    int PageSize = 20);

public sealed record CustomerResponse(
    Guid Id,
    string PersonType,
    string DocType,
    string DocNumber,
    string FullName,
    DateOnly? BirthDate,
    string? Email,
    bool EmailVerified,
    string? PhonePrimary,
    bool PhoneVerified,
    string? PhoneSecondary,
    string? Address,
    string? District,
    decimal MonthlyNetIncome,
    string? Occupation,
    string? EmployerName,
    int WorkSeniorityMonths,
    string KycStatus,
    DateTimeOffset? KycExpiresAt,
    bool IsPep,
    string AmlRiskLevel,
    decimal CreditBalance,
    int Version,
    DateTimeOffset CreatedAt);

public sealed record CustomerListItem(
    Guid Id,
    string PersonType,
    string DocType,
    string DocNumber,
    string FullName,
    string KycStatus,
    string AmlRiskLevel,
    bool IsPep,
    DateTimeOffset CreatedAt);

public sealed record CustomerChangeEntry(
    DateTimeOffset ChangedAt,
    Guid? ChangedBy,
    string? ChangedByEmail,
    string Action,
    IReadOnlyList<FieldChange> Changes,
    string? Ip,
    string? UserAgent);

public sealed record FieldChange(string Field, string? OldValue, string? NewValue);

public sealed record CustomerLoanSummary(
    Guid LoanId,
    string LoanNumber,
    string ProductName,
    string Status,
    string Currency,
    decimal PrincipalBalance,
    decimal InterestBalance,
    decimal LateFeeBalance,
    int DaysPastDue,
    int MaxDaysPastDue,
    DateOnly? NextDueDate,
    DateTimeOffset DisbursedAt);

public sealed record CustomerApplicationSummary(
    Guid ApplicationId,
    string ApplicationNumber,
    string ProductName,
    decimal RequestedAmount,
    string Currency,
    int TermMonths,
    string Status,
    DateTimeOffset SubmittedAt);

public sealed record ConsolidatedDebt(
    Money Principal,
    Money Interest,
    Money LateFees,
    Money Total);

public sealed record CustomerOverviewResponse(
    CustomerResponse Customer,
    IReadOnlyList<CustomerLoanSummary> ActiveLoans,
    IReadOnlyList<CustomerLoanSummary> ClosedLoans,
    ConsolidatedDebt Debt,
    int CurrentMaxDaysPastDue,
    int HistoricalMaxDaysPastDue,
    bool HasActiveDelinquency,
    IReadOnlyList<CustomerApplicationSummary> Applications,
    decimal CreditBalance);
