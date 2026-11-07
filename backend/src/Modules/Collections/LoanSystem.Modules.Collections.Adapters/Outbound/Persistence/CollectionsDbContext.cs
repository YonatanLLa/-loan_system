using LoanSystem.Common.Adapters.Persistence;
using LoanSystem.Modules.Collections.Core.Ports.Outbound;
using Microsoft.EntityFrameworkCore;

namespace LoanSystem.Modules.Collections.Adapters.Outbound.Persistence;

internal sealed class CollectionsDbContext : ModuleDbContext, ICollectionsUnitOfWork, ICollectionsOutbox
{
    public CollectionsDbContext(DbContextOptions<CollectionsDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // Configured entity mappings owned by Collections module
    }
}
