namespace LoanSystem.Modules.Customers.Core.Application;

internal sealed class CustomersOptions
{
    public const string SectionName = "Customers";

    public KycOptions Kyc { get; init; } = new();
    public DocumentsOptions Documents { get; init; } = new();
    public StorageOptions Storage { get; init; } = new();
    public IdentityVerifierOptions IdentityVerifier { get; init; } = new();
    public BankAccountsOptions BankAccounts { get; init; } = new();
    public OtpOptions Otp { get; init; } = new();
}

internal sealed class KycOptions
{
    public int ValidityMonths { get; init; } = 12;
    public int ExpiringSoonDays { get; init; } = 30;
    public decimal IncomeProofThreshold { get; init; } = 3000.00m;
}

internal sealed class DocumentsOptions
{
    public int MaxFileSizeMb { get; init; } = 10;
    public int SignedUrlTtlSeconds { get; init; } = 300;
    public string[] AllowedTypes { get; init; } =
    [
        "image/jpeg",
        "image/png",
        "image/webp",
        "application/pdf"
    ];

    public long MaxFileSizeBytes => checked((long)MaxFileSizeMb * 1024L * 1024L);
}

internal sealed class StorageOptions
{
    public string Provider { get; init; } = "Local";
    public string LocalPath { get; init; } = "./.storage";
    public string Bucket { get; init; } = string.Empty;
    public string Region { get; init; } = string.Empty;
    public string ServiceUrl { get; init; } = string.Empty;
    public bool ForcePathStyle { get; init; } = true;
}

internal sealed class IdentityVerifierOptions
{
    public string Provider { get; init; } = "Fake";
    public int TimeoutSeconds { get; init; } = 15;
}

internal sealed class BankAccountsOptions
{
    public int CoolingPeriodHours { get; init; } = 24;
    public int MaxActivePerCustomer { get; init; } = 5;
}

internal sealed class OtpOptions
{
    public int Length { get; init; } = 6;
    public int TtlSeconds { get; init; } = 300;
    public int MaxAttempts { get; init; } = 5;
    public int ResendCooldownSeconds { get; init; } = 60;
    public int MaxSendsPerHour { get; init; } = 5;
}
