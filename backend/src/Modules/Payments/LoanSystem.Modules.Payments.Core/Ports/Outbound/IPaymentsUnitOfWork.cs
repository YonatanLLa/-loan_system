using LoanSystem.Common.Core.Ports.Outbound;

namespace LoanSystem.Modules.Payments.Core.Ports.Outbound;

/// <summary>Unit of work scoped to the Payments module's persistence.</summary>
public interface IPaymentsUnitOfWork : IUnitOfWork;
