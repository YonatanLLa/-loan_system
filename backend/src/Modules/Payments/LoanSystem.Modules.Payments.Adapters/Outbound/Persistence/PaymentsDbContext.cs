using LoanSystem.Common.Adapters.Persistence;
using LoanSystem.Modules.Payments.Core.Ports.Outbound;
using Microsoft.EntityFrameworkCore;

namespace LoanSystem.Modules.Payments.Adapters.Outbound.Persistence;

internal sealed class PaymentsDbContext : ModuleDbContext, IPaymentsUnitOfWork, IPaymentsOutbox
{
    public PaymentsDbContext(DbContextOptions<PaymentsDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // Configured entity mappings owned by Payments module
    }
}
