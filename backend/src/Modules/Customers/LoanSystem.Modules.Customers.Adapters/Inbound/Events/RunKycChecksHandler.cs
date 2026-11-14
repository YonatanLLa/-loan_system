using LoanSystem.Common.Core.Contracts;
using LoanSystem.Modules.Customers.Core.Contracts.IntegrationEvents;
using LoanSystem.Modules.Customers.Core.Ports.Inbound;
using Microsoft.Extensions.Logging;

namespace LoanSystem.Modules.Customers.Adapters.Inbound.Events;

internal sealed class RunKycChecksHandler : IIntegrationEventHandler<CustomerKycSubmittedIntegrationEvent>
{
    private readonly IRunKycChecksUseCase _useCase;
    private readonly ILogger<RunKycChecksHandler> _logger;

    public RunKycChecksHandler(IRunKycChecksUseCase useCase, ILogger<RunKycChecksHandler> logger)
    {
        _useCase = useCase;
        _logger = logger;
    }

    public async Task HandleAsync(CustomerKycSubmittedIntegrationEvent @event, CancellationToken ct = default)
    {
        _logger.LogInformation("Running automated KYC verification checks for customer {CustomerId}.", @event.CustomerId);
        await _useCase.ExecuteForSubmissionAsync(@event.CustomerId, ct);
    }
}
