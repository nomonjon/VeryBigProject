namespace GrpcServer.Services.Caching;

// No-op cache: every read hits the factory, every eviction is a no-op.
// Wired in when Cache:Provider=None to measure the uncached baseline.
public class NoCacheService : ICacheService
{
    public Task<T?> GetOrSetAsync<T>(string key, Func<Task<T?>> factory, TimeSpan? ttl = null) => factory();

    public Task RemoveAsync(params string[] keys) => Task.CompletedTask;
}
