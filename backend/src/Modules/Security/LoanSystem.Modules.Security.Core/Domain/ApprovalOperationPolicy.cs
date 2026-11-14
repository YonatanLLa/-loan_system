using LoanSystem.Common.Core.Domain.Approvals;

namespace LoanSystem.Modules.Security.Core.Domain;

/// <summary>Maps each four-eyes operation type to the permission required to approve or reject it (BE-SEC-09, R3).</summary>
internal static class ApprovalOperationPolicy
{
    public static string? GetApprovalPermission(string operationType) =>
        operationType switch
        {
            ApprovalOperationTypes.PaymentReversal => "approvals.payments.reverse",
            ApprovalOperationTypes.LoanWriteOff => "approvals.loans.write_off",
            ApprovalOperationTypes.ManualAdjustment => "approvals.loans.adjust",
            ApprovalOperationTypes.ProductRateChange => "approvals.products.rates",
            ApprovalOperationTypes.LoanRestructure => "approvals.loans.restructure",
            ApprovalOperationTypes.UserRoleChange => "approvals.users.role",
            _ => null
        };
}
