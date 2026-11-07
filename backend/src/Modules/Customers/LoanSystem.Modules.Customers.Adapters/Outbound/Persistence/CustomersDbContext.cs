using LoanSystem.Common.Adapters.Persistence;
using LoanSystem.Modules.Customers.Core.Ports.Outbound;
using Microsoft.EntityFrameworkCore;

namespace LoanSystem.Modules.Customers.Adapters.Outbound.Persistence;

internal sealed class CustomersDbContext : ModuleDbContext, ICustomersUnitOfWork, ICustomersOutbox
{
    public CustomersDbContext(DbContextOptions<CustomersDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // Configured entity mappings owned by Customers module
    }
}
