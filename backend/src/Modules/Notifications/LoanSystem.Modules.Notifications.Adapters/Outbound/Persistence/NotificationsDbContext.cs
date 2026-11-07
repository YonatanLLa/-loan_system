using LoanSystem.Common.Adapters.Persistence;
using LoanSystem.Modules.Notifications.Core.Ports.Outbound;
using Microsoft.EntityFrameworkCore;

namespace LoanSystem.Modules.Notifications.Adapters.Outbound.Persistence;

internal sealed class NotificationsDbContext : ModuleDbContext, INotificationsUnitOfWork, INotificationsOutbox
{
    public NotificationsDbContext(DbContextOptions<NotificationsDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // Configured entity mappings owned by Notifications module
    }
}
