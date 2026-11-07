using LoanSystem.Common.Core.Ports.Outbound;

namespace LoanSystem.Modules.Integrations.Core.Ports.Outbound;

/// <summary>Transactional outbox of the Integrations module: events commit together with the module's unit of work.</summary>
public interface IIntegrationsOutbox : IOutbox;
