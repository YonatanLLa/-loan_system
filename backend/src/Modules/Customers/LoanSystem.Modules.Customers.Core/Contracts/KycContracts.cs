namespace LoanSystem.Modules.Customers.Core.Contracts;

public sealed record UploadCustomerDocumentRequest(
    Guid CustomerId,
    string Category,
    string? OriginalName,
    string ContentType,
    long SizeBytes,
    Stream Content);

public sealed record CustomerDocumentResponse(
    Guid Id,
    Guid CustomerId,
    string Category,
    string Sha256,
    string MimeType,
    long SizeBytes,
    string? OriginalName,
    bool IsCurrent,
    DateTimeOffset UploadedAt,
    DateTimeOffset? ReplacedAt);

public sealed record DocumentSignedUrlResponse(
    string Url,
    DateTimeOffset ExpiresAt);

public sealed record LocalDocumentContent(
    Stream Stream,
    string ContentType,
    string FileName);

public sealed record ListCustomerDocumentsRequest(
    Guid CustomerId,
    bool IncludeHistory = false);

public sealed record GetDocumentSignedUrlRequest(
    Guid CustomerId,
    Guid DocumentId);

public sealed record ApproveKycRequest(string Notes);

public sealed record ApproveKycCommand(Guid CustomerId, string Notes);

public sealed record RejectKycRequest(string Reason);

public sealed record RejectKycCommand(Guid CustomerId, string Reason);

public sealed record KycVerificationResponse(
    Guid Id,
    string CheckType,
    string Provider,
    string Result,
    decimal? Score,
    string? ExternalReference,
    string? ErrorMessage,
    DateTimeOffset RequestedAt,
    DateTimeOffset? CompletedAt);

public sealed record CustomerKycStatusResponse(
    Guid CustomerId,
    string KycStatus,
    bool IsSubmittedForReview,
    DateTimeOffset? KycSubmittedAt,
    DateTimeOffset? KycVerifiedAt,
    DateTimeOffset? KycExpiresAt,
    string? KycRejectedReason,
    string? KycNotes,
    bool IsPep,
    string AmlRiskLevel,
    IReadOnlyList<string> RequiredDocuments,
    IReadOnlyList<string> MissingDocuments,
    IReadOnlyList<CustomerDocumentResponse> CurrentDocuments,
    IReadOnlyList<KycVerificationResponse> Verifications);
