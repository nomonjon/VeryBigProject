using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace GrpcServer.Services.Caching;

// Cache-aside over IDistributedCache. Every Redis call is best-effort: a cache
// outage degrades to a direct database read instead of failing the request.
public class RedisCacheService(IDistributedCache cache, ILogger<RedisCacheService> logger) : ICacheService
{
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(1);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<T?> GetOrSetAsync<T>(string key, Func<Task<T?>> factory, TimeSpan? ttl = null)
    {
        try
        {
            var cached = await cache.GetStringAsync(key);
            if (cached is not null)
                return JsonSerializer.Deserialize<T>(cached, JsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Cache read failed for {Key}; falling back to source.", key);
        }

        var value = await factory();
        if (value is null)
            return value;

        try
        {
            await cache.SetStringAsync(
                key,
                JsonSerializer.Serialize(value, JsonOptions),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl ?? DefaultTtl });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Cache write failed for {Key}.", key);
        }

        return value;
    }

    public async Task RemoveAsync(params string[] keys)
    {
        foreach (var key in keys)
        {
            try
            {
                await cache.RemoveAsync(key);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Cache eviction failed for {Key}.", key);
            }
        }
    }
}
