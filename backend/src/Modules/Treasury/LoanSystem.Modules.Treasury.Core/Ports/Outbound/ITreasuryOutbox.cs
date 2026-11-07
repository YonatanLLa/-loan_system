using LoanSystem.Common.Core.Ports.Outbound;

namespace LoanSystem.Modules.Treasury.Core.Ports.Outbound;

/// <summary>Transactional outbox of the Treasury module: events commit together with the module's unit of work.</summary>
public interface ITreasuryOutbox : IOutbox;
