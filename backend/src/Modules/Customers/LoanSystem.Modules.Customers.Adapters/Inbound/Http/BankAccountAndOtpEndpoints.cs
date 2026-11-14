using LoanSystem.Common.Adapters.Http;
using LoanSystem.Common.Adapters.Http.Idempotency;
using LoanSystem.Common.Core.Application;
using LoanSystem.Modules.Customers.Core.Contracts;
using LoanSystem.Modules.Customers.Core.Ports.Inbound;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LoanSystem.Modules.Customers.Adapters.Inbound.Http;

internal static class BankAccountAndOtpEndpoints
{
    public static void MapCustomerBankAccountsAndOtpEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/{id:guid}/bank-accounts", async (
            Guid id,
            RegisterBankAccountRequest request,
            IRegisterBankAccountUseCase useCase,
            CancellationToken ct) =>
        {
            var result = await useCase.ExecuteAsync(new RegisterBankAccountCommand(id, request), ct);
            return result.ToCreatedResult(a => $"/api/customers/{id}/bank-accounts");
        })
        .RequireAuthorization(SecurityPolicies.CustomersAccountsManageOrClient)
        .RequireCustomerOwnershipOrStaff(SecurityPolicies.Admin, SecurityPolicies.Operator)
        .RequireIdempotencyKey()
        .WithName("RegisterBankAccount")
        .WithSummary("Register a bank account (CCI), digital wallet or tokenized card with encryption at rest (BE-CUS-09)")
        .Produces<BankAccountResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapGet("/{id:guid}/bank-accounts", async (
            Guid id,
            IListBankAccountsUseCase useCase,
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
        .WithName("ListBankAccounts")
        .WithSummary("List customer bank accounts returning only masked details and last4 (BE-CUS-09)")
        .Produces<IReadOnlyList<BankAccountResponse>>(StatusCodes.Status200OK);

        group.MapPost("/{id:guid}/bank-accounts/{accountId:guid}/verify", async (
            Guid id,
            Guid accountId,
            VerifyBankAccountRequest request,
            IVerifyBankAccountUseCase useCase,
            CancellationToken ct) =>
        {
            var result = await useCase.ExecuteAsync(new VerifyBankAccountCommand(id, accountId, request), ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(SecurityPolicies.Operator)
        .WithName("VerifyBankAccount")
        .WithSummary("Verify bank account ownership against customer name and activate it (BE-CUS-10)")
        .Produces<BankAccountResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapDelete("/{id:guid}/bank-accounts/{accountId:guid}", async (
            Guid id,
            Guid accountId,
            IDeactivateBankAccountUseCase useCase,
            CancellationToken ct) =>
        {
            var result = await useCase.ExecuteAsync(new DeactivateBankAccountCommand(id, accountId), ct);
            return result.ToNoContentResult();
        })
        .RequireAuthorization(SecurityPolicies.CustomersAccountsManageOrClient)
        .RequireCustomerOwnershipOrStaff(SecurityPolicies.Admin, SecurityPolicies.Operator)
        .WithName("DeactivateBankAccount")
        .WithSummary("Logically deactivate a customer bank account (BE-CUS-10)")
        .Produces(StatusCodes.Status204NoContent);

        group.MapPut("/{id:guid}/bank-accounts/{accountId:guid}/primary", async (
            Guid id,
            Guid accountId,
            ISetPrimaryBankAccountUseCase useCase,
            CancellationToken ct) =>
        {
            var result = await useCase.ExecuteAsync(new SetPrimaryBankAccountCommand(id, accountId), ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(SecurityPolicies.CustomersAccountsManageOrClient)
        .RequireCustomerOwnershipOrStaff(SecurityPolicies.Admin, SecurityPolicies.Operator)
        .RequireIdempotencyKey()
        .WithName("SetPrimaryBankAccount")
        .WithSummary("Assign primary disbursement account (issues OTP challenge when replacing an existing primary) (BE-CUS-10)")
        .Produces<SetPrimaryBankAccountResult>(StatusCodes.Status200OK);

        group.MapPost("/{id:guid}/bank-accounts/{accountId:guid}/primary/confirm", async (
            Guid id,
            Guid accountId,
            ConfirmPrimaryBankAccountRequest request,
            IConfirmPrimaryBankAccountUseCase useCase,
            CancellationToken ct) =>
        {
            var result = await useCase.ExecuteAsync(
                new ConfirmPrimaryBankAccountCommand(id, accountId, request.OtpRequestId, request.Code),
                ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(SecurityPolicies.CustomersAccountsManageOrClient)
        .RequireCustomerOwnershipOrStaff(SecurityPolicies.Admin, SecurityPolicies.Operator)
        .WithName("ConfirmPrimaryBankAccount")
        .WithSummary("Confirm primary disbursement account change with OTP verification and apply cooling period (BE-CUS-10)")
        .Produces<BankAccountResponse>(StatusCodes.Status200OK);

        group.MapPost("/{id:guid}/contact/verify/start", async (
            Guid id,
            StartContactVerificationRequest request,
            IStartContactVerificationUseCase useCase,
            CancellationToken ct) =>
        {
            var result = await useCase.ExecuteAsync(new StartContactVerificationCommand(id, request.Channel), ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(SecurityPolicies.CustomersWriteOrClient)
        .RequireCustomerOwnershipOrStaff(
            SecurityPolicies.Admin,
            SecurityPolicies.Analyst,
            SecurityPolicies.Operator)
        .WithName("StartContactVerification")
        .WithSummary("Start OTP contact verification for email or primary phone (BE-CUS-11)")
        .Produces<OtpIssued>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
        .ProducesProblem(StatusCodes.Status423Locked);

        group.MapPost("/{id:guid}/contact/verify/confirm", async (
            Guid id,
            ConfirmContactVerificationRequest request,
            IConfirmContactVerificationUseCase useCase,
            CancellationToken ct) =>
        {
            var result = await useCase.ExecuteAsync(
                new ConfirmContactVerificationCommand(id, request.OtpRequestId, request.Code),
                ct);
            return result.ToNoContentResult();
        })
        .RequireAuthorization(SecurityPolicies.CustomersWriteOrClient)
        .RequireCustomerOwnershipOrStaff(
            SecurityPolicies.Admin,
            SecurityPolicies.Analyst,
            SecurityPolicies.Operator)
        .WithName("ConfirmContactVerification")
        .WithSummary("Confirm OTP code and mark customer email or phone as verified (BE-CUS-11)")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
        .ProducesProblem(StatusCodes.Status423Locked);
    }
}
