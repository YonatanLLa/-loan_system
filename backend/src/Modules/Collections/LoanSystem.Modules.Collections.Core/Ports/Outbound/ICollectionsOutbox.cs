using LoanSystem.Common.Core.Ports.Outbound;

namespace LoanSystem.Modules.Collections.Core.Ports.Outbound;

/// <summary>Transactional outbox of the Collections module: events commit together with the module's unit of work.</summary>
public interface ICollectionsOutbox : IOutbox;
