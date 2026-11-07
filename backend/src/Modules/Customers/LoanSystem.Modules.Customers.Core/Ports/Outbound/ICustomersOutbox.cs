using LoanSystem.Common.Core.Ports.Outbound;

namespace LoanSystem.Modules.Customers.Core.Ports.Outbound;

/// <summary>Transactional outbox of the Customers module: events commit together with the module's unit of work.</summary>
public interface ICustomersOutbox : IOutbox;
