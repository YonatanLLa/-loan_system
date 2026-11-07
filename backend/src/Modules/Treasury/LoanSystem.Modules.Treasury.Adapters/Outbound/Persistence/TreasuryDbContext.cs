using LoanSystem.Common.Adapters.Persistence;
using LoanSystem.Modules.Treasury.Core.Ports.Outbound;
using Microsoft.EntityFrameworkCore;

namespace LoanSystem.Modules.Treasury.Adapters.Outbound.Persistence;

internal sealed class TreasuryDbContext : ModuleDbContext, ITreasuryUnitOfWork, ITreasuryOutbox
{
    public TreasuryDbContext(DbContextOptions<TreasuryDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // Configured entity mappings owned by Treasury module
    }
}
