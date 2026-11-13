namespace LoanSystem.Common.Core.Domain.Approvals;

/// <summary>Canonical operation types governed by four-eyes approval (BE-SEC-09).</summary>
public static class ApprovalOperationTypes
{
    public const string PaymentReversal = "PAYMENT_REVERSAL";
    public const string LoanWriteOff = "LOAN_WRITE_OFF";
    public const string ManualAdjustment = "MANUAL_ADJUSTMENT";
    public const string ProductRateChange = "PRODUCT_RATE_CHANGE";
    public const string LoanRestructure = "LOAN_RESTRUCTURE";
    public const string UserRoleChange = "USER_ROLE_CHANGE";

    public static readonly IReadOnlyList<string> All =
    [
        PaymentReversal,
        LoanWriteOff,
        ManualAdjustment,
        ProductRateChange,
        LoanRestructure,
        UserRoleChange
    ];
}
