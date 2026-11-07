using LoanSystem.Common.Adapters.Persistence;
using LoanSystem.Modules.Products.Core.Ports.Outbound;
using Microsoft.EntityFrameworkCore;

namespace LoanSystem.Modules.Products.Adapters.Outbound.Persistence;

internal sealed class ProductsDbContext : ModuleDbContext, IProductsUnitOfWork, IProductsOutbox
{
    public ProductsDbContext(DbContextOptions<ProductsDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // Configured entity mappings owned by Products module
    }
}
