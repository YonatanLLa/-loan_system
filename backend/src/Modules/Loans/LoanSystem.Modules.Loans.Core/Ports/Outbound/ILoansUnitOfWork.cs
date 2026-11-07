using LoanSystem.Common.Core.Ports.Outbound;

namespace LoanSystem.Modules.Loans.Core.Ports.Outbound;

/// <summary>Unit of work scoped to the Loans module's persistence.</summary>
public interface ILoansUnitOfWork : IUnitOfWork;
