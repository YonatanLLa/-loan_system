using LoanSystem.Common.Adapters.Persistence;
using LoanSystem.Modules.Loans.Core.Ports.Outbound;
using Microsoft.EntityFrameworkCore;

namespace LoanSystem.Modules.Loans.Adapters.Outbound.Persistence;

internal sealed class LoansDbContext : ModuleDbContext, ILoansUnitOfWork, ILoansOutbox
{
    public LoansDbContext(DbContextOptions<LoansDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // Configured entity mappings owned by Loans module
    }
}
