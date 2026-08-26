using Microsoft.Extensions.Caching.Memory;

namespace GrpcServer.Services.Caching;

// In-process cache-aside over IMemoryCache. Same contract as RedisCacheService
// but the store lives in the app's heap: no serialization, no network hop.
// Trade-off vs Redis — not shared across instances and lost on restart.
public class MemoryCacheService(IMemoryCache cache) : ICacheService
{
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(1);

    public async Task<T?> GetOrSetAsync<T>(string key, Func<Task<T?>> factory, TimeSpan? ttl = null)
    {
        if (cache.TryGetValue(key, out T? cached))
            return cached;

        var value = await factory();
        if (value is null)
            return value;

        cache.Set(key, value, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ttl ?? DefaultTtl
        });

        return value;
    }

    public Task RemoveAsync(params string[] keys)
    {
        foreach (var key in keys)
            cache.Remove(key);

        return Task.CompletedTask;
    }
}
