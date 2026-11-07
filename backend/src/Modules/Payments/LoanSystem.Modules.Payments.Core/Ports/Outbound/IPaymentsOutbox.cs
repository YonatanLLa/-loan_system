using LoanSystem.Common.Core.Ports.Outbound;

namespace LoanSystem.Modules.Payments.Core.Ports.Outbound;

/// <summary>Transactional outbox of the Payments module: events commit together with the module's unit of work.</summary>
public interface IPaymentsOutbox : IOutbox;
