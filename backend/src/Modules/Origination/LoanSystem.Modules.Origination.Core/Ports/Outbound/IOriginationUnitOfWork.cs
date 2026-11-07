using LoanSystem.Common.Core.Ports.Outbound;

namespace LoanSystem.Modules.Origination.Core.Ports.Outbound;

/// <summary>Unit of work scoped to the Origination module's persistence.</summary>
public interface IOriginationUnitOfWork : IUnitOfWork;
