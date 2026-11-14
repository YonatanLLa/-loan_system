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

    Task<bool> IsUserSessionValidAsync(Guid userId, string? expectedRole, CancellationToken ct = default) =>
        IsUserActiveAsync(userId, ct);
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
        if (_cache.TryGetValue(ActiveCacheKey(userId), out bool isActive))
        {
            return isActive;
        }

        var state = await LoadAndCacheAsync(userId, ct);
        return state.IsActive;
    }

    public async Task<bool> IsUserSessionValidAsync(Guid userId, string? expectedRole, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(ActiveCacheKey(userId), out bool cachedActive) && !cachedActive)
        {
            return false;
        }

        if (!_cache.TryGetValue(StateCacheKey(userId), out UserAuthState? state) || state is null)
        {
            state = await LoadAndCacheAsync(userId, ct);
        }

        if (!state.IsActive)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(expectedRole)
            && !string.Equals(expectedRole, state.Role, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    public void InvalidateUser(Guid userId)
    {
        _cache.Set(ActiveCacheKey(userId), false, CacheDuration);
        _cache.Remove(StateCacheKey(userId));
    }

    public void ActivateUser(Guid userId)
    {
        _cache.Set(ActiveCacheKey(userId), true, CacheDuration);
        _cache.Remove(StateCacheKey(userId));
    }

    public void EvictUser(Guid userId)
    {
        _cache.Remove(ActiveCacheKey(userId));
        _cache.Remove(StateCacheKey(userId));
    }

    private async Task<UserAuthState> LoadAndCacheAsync(Guid userId, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SecurityDbContext>();

        var userRow = await db.Users
            .Where(u => u.Id == userId)
            .Select(u => new { u.Status, u.Role })
            .FirstOrDefaultAsync(ct);

        var state = userRow is null
            ? new UserAuthState(false, null)
            : new UserAuthState(userRow.Status == UserStatus.ACTIVE, userRow.Role.ToString());

        _cache.Set(ActiveCacheKey(userId), state.IsActive, CacheDuration);
        _cache.Set(StateCacheKey(userId), state, CacheDuration);
        return state;
    }

    private static string ActiveCacheKey(Guid userId) => $"user_active:{userId}";
    private static string StateCacheKey(Guid userId) => $"user_state:{userId}";

    private sealed record UserAuthState(bool IsActive, string? Role);
}
