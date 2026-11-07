using LoanSystem.Common.Adapters.Persistence;
using LoanSystem.Modules.Administration.Core.Ports.Outbound;
using Microsoft.EntityFrameworkCore;

namespace LoanSystem.Modules.Administration.Adapters.Outbound.Persistence;

internal sealed class AdministrationDbContext : ModuleDbContext, IAdministrationUnitOfWork, IAdministrationOutbox
{
    public AdministrationDbContext(DbContextOptions<AdministrationDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // Configured entity mappings owned by Administration module
    }
}
