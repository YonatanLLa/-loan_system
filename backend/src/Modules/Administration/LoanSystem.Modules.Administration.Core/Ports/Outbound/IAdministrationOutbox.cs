using LoanSystem.Common.Core.Ports.Outbound;

namespace LoanSystem.Modules.Administration.Core.Ports.Outbound;

/// <summary>Transactional outbox of the Administration module: events commit together with the module's unit of work.</summary>
public interface IAdministrationOutbox : IOutbox;
