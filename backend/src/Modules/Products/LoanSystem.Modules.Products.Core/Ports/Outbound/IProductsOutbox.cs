using LoanSystem.Common.Core.Ports.Outbound;

namespace LoanSystem.Modules.Products.Core.Ports.Outbound;

/// <summary>Transactional outbox of the Products module: events commit together with the module's unit of work.</summary>
public interface IProductsOutbox : IOutbox;
