using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace LoanSystem.Common.Adapters.Http.Idempotency;

public static class IdempotencyServiceCollectionExtensions
{
    /// <summary>
    /// Registers the idempotency store: Redis when ConnectionStrings:Redis is set, otherwise an
    /// in-memory store (single instance only). Returns true when Redis is used.
    /// </summary>
    public static bool AddIdempotency(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<IdempotencyOptions>(configuration.GetSection(IdempotencyOptions.SectionName));

        var redisConnection = configuration.GetConnectionString("Redis");
        if (string.IsNullOrWhiteSpace(redisConnection))
        {
            services.AddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>();
            return false;
        }

        var redisOptions = ConfigurationOptions.Parse(redisConnection);
        // Do not crash the API at startup if Redis is down; the multiplexer reconnects in the background
        redisOptions.AbortOnConnectFail = false;

        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisOptions));
        services.AddSingleton<IIdempotencyStore, RedisIdempotencyStore>();
        return true;
    }
}
