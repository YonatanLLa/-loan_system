using LoanSystem.Common.Adapters.Http;
using LoanSystem.Common.Adapters.Http.Idempotency;
using LoanSystem.Common.Core.Application;
using LoanSystem.Common.Core.Contracts;
using LoanSystem.Modules.Customers.Core.Contracts;
using LoanSystem.Modules.Customers.Core.Ports.Inbound;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace LoanSystem.Modules.Customers.Adapters.Inbound.Http;

public static class CustomersEndpoints
{
    public static IEndpointRouteBuilder MapCustomersEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/customers")
            .WithTags("Customers");

        group.MapGet("/ping", () => Results.Ok(new { Module = "Customers", Status = "Ready" }))
            .RequireAuthorization(SecurityPolicies.Admin)
            .WithName("CustomersPing");

        group.MapPost("/", async (
            RegisterCustomerRequest request,
            IRegisterCustomerUseCase useCase,
            CancellationToken ct) =>
        {
            var result = await useCase.ExecuteAsync(request, ct);
            return result.ToCreatedResult(c => $"/api/customers/{c.Id}");
        })
        .RequireAuthorization(SecurityPolicies.CustomersWrite)
        .RequireIdempotencyKey()
        .WithName("RegisterCustomer")
        .WithSummary("Register a new natural or legal customer with identity validation and initial consents (BE-CUS-01)")
        .Produces<CustomerResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapGet("/", async (
            [AsParameters] SearchCustomersRequest request,
            ISearchCustomersUseCase useCase,
            CancellationToken ct) =>
        {
            var result = await useCase.ExecuteAsync(request, ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(SecurityPolicies.CustomersRead)
        .WithName("SearchCustomers")
        .WithSummary("Search and paginate customers by document, name, email, phone or KYC status (BE-CUS-02)")
        .Produces<PagedResult<CustomerListItem>>(StatusCodes.Status200OK);

        group.MapGet("/{id:guid}", async (
            Guid id,
            IGetCustomerUseCase useCase,
            CancellationToken ct) =>
        {
            var result = await useCase.ExecuteAsync(id, ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(SecurityPolicies.CustomersReadOrClient)
        .RequireCustomerOwnershipOrStaff(
            SecurityPolicies.Admin,
            SecurityPolicies.Analyst,
            SecurityPolicies.Operator,
            SecurityPolicies.Auditor,
            SecurityPolicies.Collector)
        .WithName("GetCustomer")
        .WithSummary("Get a customer's profile with role-based PII masking (BE-CUS-02)")
        .Produces<CustomerResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPatch("/{id:guid}", async (
            Guid id,
            UpdateCustomerRequest request,
            HttpContext http,
            IUpdateCustomerUseCase useCase,
            CancellationToken ct) =>
        {
            if (!HttpEndpointExtensions.TryParseIfMatchVersion(http.Request, out var ifMatchVersion))
            {
                return HttpEndpointExtensions.ValidationProblem("Invalid If-Match ETag version header.");
            }

            var expectedVersion = ifMatchVersion ?? request.ExpectedVersion;
            var result = await useCase.ExecuteAsync(new UpdateCustomerCommand(id, request, expectedVersion), ct);
            if (result.IsSuccess)
            {
                http.Response.Headers.ETag = $"\"{result.Value.Version}\"";
            }

            return result.ToHttpResult();
        })
        .RequireAuthorization(SecurityPolicies.CustomersWriteOrClient)
        .RequireCustomerOwnershipOrStaff(
            SecurityPolicies.Admin,
            SecurityPolicies.Analyst,
            SecurityPolicies.Operator)
        .WithName("UpdateCustomer")
        .WithSummary("Update contact, address or socioeconomic profile with optimistic concurrency control (BE-CUS-02)")
        .Produces<CustomerResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/{id:guid}/history", async (
            Guid id,
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            IGetCustomerHistoryUseCase useCase,
            CancellationToken ct) =>
        {
            var effectivePage = page is null or 0 ? 1 : page.Value;
            var effectiveSize = pageSize is null or 0 ? 20 : pageSize.Value;
            var result = await useCase.ExecuteAsync(new GetCustomerHistoryRequest(id, effectivePage, effectiveSize), ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(SecurityPolicies.CustomersAuditRead)
        .WithName("GetCustomerHistory")
        .WithSummary("Get field-level change audit history for a customer (BE-CUS-02)")
        .Produces<PagedResult<CustomerChangeEntry>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}/overview", async (
            Guid id,
            IGetCustomerOverviewUseCase useCase,
            CancellationToken ct) =>
        {
            var result = await useCase.ExecuteAsync(id, ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(SecurityPolicies.CustomersReadOrClient)
        .RequireCustomerOwnershipOrStaff(
            SecurityPolicies.Admin,
            SecurityPolicies.Analyst,
            SecurityPolicies.Operator,
            SecurityPolicies.Auditor,
            SecurityPolicies.Collector)
        .WithName("GetCustomerOverview")
        .WithSummary("Get 360-degree consolidated customer overview across loans, applications and debt (BE-CUS-08)")
        .Produces<CustomerOverviewResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapCustomerDocumentsAndKycEndpoints();
        group.MapCustomerBankAccountsAndOtpEndpoints();
        app.MapConsentEndpoints(group);

        return app;
    }
}
