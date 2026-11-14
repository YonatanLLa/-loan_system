using LoanSystem.Common.Adapters.Http;
using LoanSystem.Common.Core.Application;
using LoanSystem.Modules.Customers.Core.Contracts;
using LoanSystem.Modules.Customers.Core.Ports.Inbound;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace LoanSystem.Modules.Customers.Adapters.Inbound.Http;

internal static class KycAndConsentEndpoints
{
    private const long MaxMultipartRequestBytes = 25L * 1024L * 1024L;

    public static void MapCustomerDocumentsAndKycEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/{id:guid}/documents", async (
            Guid id,
            HttpRequest httpRequest,
            IUploadCustomerDocumentUseCase useCase,
            CancellationToken ct) =>
        {
            if (!httpRequest.HasFormContentType)
            {
                return HttpEndpointExtensions.ValidationProblem("Expected multipart/form-data request.");
            }

            var form = await httpRequest.ReadFormAsync(ct);
            var file = form.Files.GetFile("file") ?? (form.Files.Count > 0 ? form.Files[0] : null);
            var category = form["category"].ToString();

            if (file is null)
            {
                return HttpEndpointExtensions.ValidationProblem("Form file 'file' is required.");
            }

            if (string.IsNullOrWhiteSpace(category))
            {
                return HttpEndpointExtensions.ValidationProblem("Form field 'category' is required.");
            }

            await using var stream = file.OpenReadStream();
            var request = new UploadCustomerDocumentRequest(
                id,
                category,
                file.FileName,
                file.ContentType ?? "application/octet-stream",
                file.Length,
                stream);

            var result = await useCase.ExecuteAsync(request, ct);
            return result.ToCreatedResult(d => $"/api/customers/{id}/documents/{d.Id}");
        })
        .RequireAuthorization(SecurityPolicies.CustomersWriteOrClient)
        .RequireCustomerOwnershipOrStaff(
            SecurityPolicies.Admin,
            SecurityPolicies.Analyst,
            SecurityPolicies.Operator)
        .DisableAntiforgery()
        .WithMetadata(new RequestSizeLimitAttribute(MaxMultipartRequestBytes))
        .WithName("UploadCustomerDocument")
        .WithSummary("Upload a customer KYC document via streaming magic-byte verification (BE-CUS-03)")
        .Produces<CustomerDocumentResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}/documents", async (
            Guid id,
            [FromQuery] bool? includeHistory,
            IListCustomerDocumentsUseCase useCase,
            CancellationToken ct) =>
        {
            var result = await useCase.ExecuteAsync(
                new ListCustomerDocumentsRequest(id, includeHistory ?? false),
                ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(SecurityPolicies.CustomersDocsReadOrClient)
        .RequireCustomerOwnershipOrStaff(
            SecurityPolicies.Admin,
            SecurityPolicies.Analyst,
            SecurityPolicies.Operator,
            SecurityPolicies.Auditor)
        .WithName("ListCustomerDocuments")
        .WithSummary("List customer KYC documents (current or including replaced history) (BE-CUS-03)")
        .Produces<IReadOnlyList<CustomerDocumentResponse>>(StatusCodes.Status200OK);

        group.MapGet("/{id:guid}/documents/{docId:guid}/url", async (
            Guid id,
            Guid docId,
            IGetDocumentUrlUseCase useCase,
            CancellationToken ct) =>
        {
            var result = await useCase.ExecuteAsync(new GetDocumentSignedUrlRequest(id, docId), ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(SecurityPolicies.CustomersDocsReadOrClient)
        .RequireCustomerOwnershipOrStaff(
            SecurityPolicies.Admin,
            SecurityPolicies.Analyst,
            SecurityPolicies.Operator,
            SecurityPolicies.Auditor)
        .WithName("GetDocumentSignedUrl")
        .WithSummary("Generate a short-lived signed download URL and audit VIEW_DOCUMENT access (BE-CUS-03)")
        .Produces<DocumentSignedUrlResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/files/{token}", async (
            string token,
            IDownloadLocalDocumentUseCase useCase,
            CancellationToken ct) =>
        {
            var result = await useCase.ExecuteAsync(token, ct);
            if (result.IsFailure)
            {
                return result.ToHttpResult();
            }

            return Results.File(result.Value.Stream, result.Value.ContentType, enableRangeProcessing: true);
        })
        .AllowAnonymous()
        .WithName("DownloadLocalSignedDocument")
        .WithSummary("Stream a local KYC document authorized by a short-lived HMAC token (BE-CUS-03)");

        group.MapGet("/{id:guid}/kyc", async (
            Guid id,
            IGetCustomerKycUseCase useCase,
            CancellationToken ct) =>
        {
            var result = await useCase.ExecuteAsync(id, ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(SecurityPolicies.CustomersDocsReadOrClient)
        .RequireCustomerOwnershipOrStaff(
            SecurityPolicies.Admin,
            SecurityPolicies.Analyst,
            SecurityPolicies.Operator,
            SecurityPolicies.Auditor)
        .WithName("GetCustomerKycStatus")
        .WithSummary("Get current KYC status, missing documents and latest automated checks (BE-CUS-04)")
        .Produces<CustomerKycStatusResponse>(StatusCodes.Status200OK);

        group.MapPost("/{id:guid}/kyc/submit", async (
            Guid id,
            ISubmitKycUseCase useCase,
            CancellationToken ct) =>
        {
            var result = await useCase.ExecuteAsync(id, ct);
            return result.ToAcceptedResult();
        })
        .RequireAuthorization(SecurityPolicies.CustomersWriteOrClient)
        .RequireCustomerOwnershipOrStaff(
            SecurityPolicies.Admin,
            SecurityPolicies.Analyst,
            SecurityPolicies.Operator)
        .WithName("SubmitKyc")
        .WithSummary("Submit complete KYC dossier and enqueue automated verification checks (BE-CUS-04)")
        .Produces<CustomerKycStatusResponse>(StatusCodes.Status202Accepted)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/{id:guid}/kyc/approve", async (
            Guid id,
            ApproveKycRequest request,
            IApproveKycUseCase useCase,
            CancellationToken ct) =>
        {
            var result = await useCase.ExecuteAsync(new ApproveKycCommand(id, request.Notes), ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(SecurityPolicies.Analyst)
        .WithName("ApproveKyc")
        .WithSummary("Manually approve a customer's KYC (Admin required for PEP/HIGH-risk/already-verified) (BE-CUS-04)")
        .Produces<CustomerKycStatusResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/{id:guid}/kyc/reject", async (
            Guid id,
            RejectKycRequest request,
            IRejectKycUseCase useCase,
            CancellationToken ct) =>
        {
            var result = await useCase.ExecuteAsync(new RejectKycCommand(id, request.Reason), ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(SecurityPolicies.Analyst)
        .WithName("RejectKyc")
        .WithSummary("Reject a customer's KYC with a mandatory audit reason (BE-CUS-04)")
        .Produces<CustomerKycStatusResponse>(StatusCodes.Status200OK);

        group.MapPost("/{id:guid}/kyc/resubmit", async (
            Guid id,
            IResubmitKycUseCase useCase,
            CancellationToken ct) =>
        {
            var result = await useCase.ExecuteAsync(id, ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(SecurityPolicies.CustomersWriteOrClient)
        .RequireCustomerOwnershipOrStaff(
            SecurityPolicies.Admin,
            SecurityPolicies.Analyst,
            SecurityPolicies.Operator)
        .WithName("ResubmitKyc")
        .WithSummary("Move a REJECTED or EXPIRED KYC back to PENDING for re-submission (BE-CUS-04)")
        .Produces<CustomerKycStatusResponse>(StatusCodes.Status200OK);

        group.MapPost("/{id:guid}/kyc/retry-checks", async (
            Guid id,
            IRetryKycChecksUseCase useCase,
            CancellationToken ct) =>
        {
            var result = await useCase.ExecuteAsync(id, ct);
            return result.ToAcceptedResult();
        })
        .RequireAuthorization(SecurityPolicies.Analyst)
        .WithName("RetryKycChecks")
        .WithSummary("Retry automated KYC verification checks that ended in ERROR (BE-CUS-04)")
        .Produces<IReadOnlyList<KycVerificationResponse>>(StatusCodes.Status202Accepted);
    }

    public static void MapConsentEndpoints(this IEndpointRouteBuilder app, RouteGroupBuilder customerGroup)
    {
        app.MapGet("/api/consents/current", async (
            IGetCurrentConsentsUseCase useCase,
            CancellationToken ct) =>
        {
            var result = await useCase.ExecuteAsync(ct);
            return result.ToHttpResult();
        })
        .AllowAnonymous()
        .WithTags("Customers")
        .WithName("GetCurrentConsents")
        .WithSummary("Return current published versions and legal text hashes for LPDP consent purposes (BE-CUS-07)")
        .Produces<IReadOnlyList<CurrentConsentTextResponse>>(StatusCodes.Status200OK);

        customerGroup.MapGet("/{id:guid}/consents", async (
            Guid id,
            IListCustomerConsentsUseCase useCase,
            CancellationToken ct) =>
        {
            var result = await useCase.ExecuteAsync(id, ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(SecurityPolicies.CustomersDocsReadOrClient)
        .RequireCustomerOwnershipOrStaff(
            SecurityPolicies.Admin,
            SecurityPolicies.Analyst,
            SecurityPolicies.Operator,
            SecurityPolicies.Auditor)
        .WithName("ListCustomerConsents")
        .WithSummary("List granted and revoked LPDP consents for a customer (BE-CUS-07)")
        .Produces<IReadOnlyList<CustomerConsentResponse>>(StatusCodes.Status200OK);

        customerGroup.MapPost("/{id:guid}/consents", async (
            Guid id,
            GrantConsentRequest request,
            IGrantCustomerConsentUseCase useCase,
            CancellationToken ct) =>
        {
            var result = await useCase.ExecuteAsync(new GrantConsentCommand(id, request.Purpose, request.Version), ct);
            return result.ToCreatedResult(c => $"/api/customers/{id}/consents");
        })
        .RequireAuthorization(SecurityPolicies.CustomersWriteOrClient)
        .RequireCustomerOwnershipOrStaff(
            SecurityPolicies.Admin,
            SecurityPolicies.Analyst,
            SecurityPolicies.Operator)
        .WithName("GrantCustomerConsent")
        .WithSummary("Record explicit consent acceptance for a current legal text version (BE-CUS-07)")
        .Produces<CustomerConsentResponse>(StatusCodes.Status201Created);

        customerGroup.MapDelete("/{id:guid}/consents/{purpose}", async (
            Guid id,
            string purpose,
            IRevokeCustomerConsentUseCase useCase,
            CancellationToken ct) =>
        {
            var result = await useCase.ExecuteAsync(new RevokeConsentCommand(id, purpose), ct);
            return result.ToNoContentResult();
        })
        .RequireAuthorization(SecurityPolicies.CustomersWriteOrClient)
        .RequireCustomerOwnershipOrStaff(
            SecurityPolicies.Admin,
            SecurityPolicies.Analyst,
            SecurityPolicies.Operator)
        .WithName("RevokeCustomerConsent")
        .WithSummary("Revoke an optional consent purpose (e.g. MARKETING) and emit event (BE-CUS-07)")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
    }
}
