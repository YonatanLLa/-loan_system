using LoanSystem.Common.Core.Ports.Outbound;

namespace LoanSystem.Modules.Origination.Core.Ports.Outbound;

/// <summary>Transactional outbox of the Origination module: events commit together with the module's unit of work.</summary>
public interface IOriginationOutbox : IOutbox;
