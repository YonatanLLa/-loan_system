using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using LoanSystem.Modules.Security.Adapters.Outbound.Persistence;
using LoanSystem.Modules.Security.Core.Domain;
using LoanSystem.Modules.Security.Core.Ports.Outbound;

namespace LoanSystem.Modules.Security.Adapters.Outbound.Providers;

internal interface IUserStatusService
{
    Task<bool> IsUserActiveAsync(Guid userId, CancellationToken ct = default);
}

internal sealed class UserStatusService : IUserStatusService, IUserStatusTracker
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(2);
    private readonly IMemoryCache _cache;
    private readonly IServiceScopeFactory _scopeFactory;

    public UserStatusService(IMemoryCache cache, IServiceScopeFactory scopeFactory)
    {
        _cache = cache;
        _scopeFactory = scopeFactory;
    }

    public async Task<bool> IsUserActiveAsync(Guid userId, CancellationToken ct = default)
    {
        var cacheKey = $"user_active:{userId}";
        if (_cache.TryGetValue(cacheKey, out bool isActive))
        {
            return isActive;
        }

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SecurityDbContext>();

        var status = await db.Users
            .Where(u => u.Id == userId)
            .Select(u => (UserStatus?)u.Status)
            .FirstOrDefaultAsync(ct);

        var active = status == UserStatus.ACTIVE;
        _cache.Set(cacheKey, active, CacheDuration);
        return active;
    }

    public void InvalidateUser(Guid userId)
    {
        _cache.Set($"user_active:{userId}", false, CacheDuration);
    }

    public void ActivateUser(Guid userId)
    {
        _cache.Set($"user_active:{userId}", true, CacheDuration);
    }
}
