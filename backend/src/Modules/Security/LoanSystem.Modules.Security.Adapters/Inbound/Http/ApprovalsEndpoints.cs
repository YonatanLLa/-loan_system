using LoanSystem.Common.Adapters.Http;
using LoanSystem.Common.Core.Contracts.Approvals;
using LoanSystem.Modules.Security.Core.Contracts;
using LoanSystem.Modules.Security.Core.Ports.Inbound;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LoanSystem.Modules.Security.Adapters.Inbound.Http;

internal static class ApprovalsEndpoints
{
    public static IEndpointRouteBuilder MapApprovalsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/approvals")
            .WithTags("Approvals")
            .RequireAuthorization();

        group.MapGet("", async ([AsParameters] ApprovalQuery query, IQueryApprovalsUseCase useCase, CancellationToken ct) =>
        {
            var result = await useCase.ExecuteAsync(query, ct);
            return result.ToHttpResult();
        })
        .WithName("QueryApprovals")
        .WithSummary("Query four-eyes approval requests (toDecide, mine, or all) (BE-SEC-09)");

        group.MapGet("/{id:guid}", async (Guid id, IGetApprovalUseCase useCase, CancellationToken ct) =>
        {
            var result = await useCase.ExecuteAsync(id, ct);
            return result.ToHttpResult();
        })
        .WithName("GetApproval")
        .WithSummary("Get details, target snapshot, and current target state for an approval request (BE-SEC-09)");

        group.MapPost("/{id:guid}/approve", async (Guid id, ApproveRequest? body, IApproveRequestUseCase useCase, CancellationToken ct) =>
        {
            var result = await useCase.ExecuteAsync(new ApproveApprovalCommand(id, body?.Comment, body?.MfaCode), ct);
            return result.ToHttpResult();
        })
        .WithName("ApproveRequest")
        .WithSummary("Approve and atomically execute a pending four-eyes request with step-up TOTP (BE-SEC-09)");

        group.MapPost("/{id:guid}/reject", async (Guid id, RejectRequest body, IRejectRequestUseCase useCase, CancellationToken ct) =>
        {
            var result = await useCase.ExecuteAsync(new RejectApprovalCommand(id, body.Comment), ct);
            return result.ToHttpResult();
        })
        .WithName("RejectRequest")
        .WithSummary("Reject a pending four-eyes approval request with mandatory comment (BE-SEC-09)");

        group.MapPost("/{id:guid}/cancel", async (Guid id, ICancelRequestUseCase useCase, CancellationToken ct) =>
        {
            var result = await useCase.ExecuteAsync(id, ct);
            return result.ToHttpResult();
        })
        .WithName("CancelApprovalRequest")
        .WithSummary("Cancel a pending four-eyes approval request by its original requester (BE-SEC-09)");

        return app;
    }
}
