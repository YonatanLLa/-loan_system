using LoanSystem.Common.Adapters.Persistence;
using LoanSystem.Modules.Reports.Core.Ports.Outbound;
using Microsoft.EntityFrameworkCore;

namespace LoanSystem.Modules.Reports.Adapters.Outbound.Persistence;

internal sealed class ReportsDbContext : ModuleDbContext, IReportsUnitOfWork, IReportsOutbox
{
    public ReportsDbContext(DbContextOptions<ReportsDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // Configured entity mappings owned by Reports module
    }
}
