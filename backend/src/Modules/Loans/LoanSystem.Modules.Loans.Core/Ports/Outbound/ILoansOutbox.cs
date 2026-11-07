using LoanSystem.Common.Core.Ports.Outbound;

namespace LoanSystem.Modules.Loans.Core.Ports.Outbound;

/// <summary>Transactional outbox of the Loans module: events commit together with the module's unit of work.</summary>
public interface ILoansOutbox : IOutbox;
