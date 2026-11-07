using LoanSystem.Common.Core.Ports.Outbound;

namespace LoanSystem.Modules.Reports.Core.Ports.Outbound;

/// <summary>Transactional outbox of the Reports module: events commit together with the module's unit of work.</summary>
public interface IReportsOutbox : IOutbox;
