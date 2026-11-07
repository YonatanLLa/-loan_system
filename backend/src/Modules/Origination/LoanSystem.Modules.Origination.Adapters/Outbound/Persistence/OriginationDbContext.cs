using LoanSystem.Common.Adapters.Persistence;
using LoanSystem.Modules.Origination.Core.Ports.Outbound;
using Microsoft.EntityFrameworkCore;

namespace LoanSystem.Modules.Origination.Adapters.Outbound.Persistence;

internal sealed class OriginationDbContext : ModuleDbContext, IOriginationUnitOfWork, IOriginationOutbox
{
    public OriginationDbContext(DbContextOptions<OriginationDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // Configured entity mappings owned by Origination module
    }
}
