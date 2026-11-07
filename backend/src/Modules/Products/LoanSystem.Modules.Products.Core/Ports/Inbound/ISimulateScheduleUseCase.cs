using LoanSystem.Common.Core.Ports.Inbound;
using LoanSystem.Modules.Products.Core.Contracts;

namespace LoanSystem.Modules.Products.Core.Ports.Inbound;

public interface ISimulateScheduleUseCase : IUseCase<SimulateScheduleRequest, SimulateScheduleResponse>
{
}
