namespace GrpcServer.Services.Caching;

public interface ICacheService
{
    /// <summary>
    /// Returns the cached value for <paramref name="key"/>, or invokes
    /// <paramref name="factory"/> and caches its result. Null results are not cached.
    /// </summary>
    Task<T?> GetOrSetAsync<T>(string key, Func<Task<T?>> factory, TimeSpan? ttl = null);

    Task RemoveAsync(params string[] keys);
}
