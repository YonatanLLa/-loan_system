using LoanSystem.Common.Adapters.Persistence;
using LoanSystem.Modules.Security.Core.Ports.Outbound;
using Microsoft.EntityFrameworkCore;

namespace LoanSystem.Modules.Security.Adapters.Outbound.Persistence;

internal sealed class SecurityDbContext : ModuleDbContext, ISecurityUnitOfWork, ISecurityOutbox
{
    public SecurityDbContext(DbContextOptions<SecurityDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // Configured entity mappings owned by Security module
    }
}
