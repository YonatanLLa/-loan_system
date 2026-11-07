using LoanSystem.Common.Adapters.Persistence;
using LoanSystem.Modules.Integrations.Core.Ports.Outbound;
using Microsoft.EntityFrameworkCore;

namespace LoanSystem.Modules.Integrations.Adapters.Outbound.Persistence;

internal sealed class IntegrationsDbContext : ModuleDbContext, IIntegrationsUnitOfWork, IIntegrationsOutbox
{
    public IntegrationsDbContext(DbContextOptions<IntegrationsDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // Configured entity mappings owned by Integrations module
    }
}
