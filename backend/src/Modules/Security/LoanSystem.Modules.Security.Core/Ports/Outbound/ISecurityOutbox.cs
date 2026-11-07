using LoanSystem.Common.Core.Ports.Outbound;

namespace LoanSystem.Modules.Security.Core.Ports.Outbound;

/// <summary>Transactional outbox of the Security module: events commit together with the module's unit of work.</summary>
public interface ISecurityOutbox : IOutbox;
