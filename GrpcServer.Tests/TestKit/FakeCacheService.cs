using GrpcServer.Services.Caching;

namespace GrpcServer.Tests.TestKit;

/// <summary>
/// Hand-written test double for <see cref="ICacheService"/>.
///
/// Why a fake instead of <c>Mock&lt;ICacheService&gt;</c>: <c>GetOrSetAsync</c> takes a
/// factory delegate that the service under test expects to be invoked. Setting that
/// up on a mock (<c>Returns((string _, Func&lt;Task&lt;T&gt;&gt; f, TimeSpan? _) =&gt; f())</c>)
/// has to be repeated for every generic T and reads terribly. A fake states the
/// behaviour once.
///
/// Default behaviour is "always miss", so service tests exercise the repository path.
/// <see cref="Seed"/> makes a key a hit so the caching branch can be tested too.
/// </summary>
public sealed class FakeCacheService : ICacheService
{
    private readonly Dictionary<string, object?> _seeded = new();

    /// <summary>Keys passed to <see cref="RemoveAsync"/>, in call order.</summary>
    public List<string> RemovedKeys { get; } = [];

    /// <summary>Keys whose factory was actually executed (i.e. a cache miss).</summary>
    public List<string> FactoryCalls { get; } = [];

    /// <summary>Number of separate <see cref="RemoveAsync"/> invocations.</summary>
    public int RemoveCallCount { get; private set; }

    /// <summary>Makes <paramref name="key"/> a cache hit returning <paramref name="value"/>.</summary>
    public FakeCacheService Seed<T>(string key, T? value)
    {
        _seeded[key] = value;
        return this;
    }

    public async Task<T?> GetOrSetAsync<T>(string key, Func<Task<T?>> factory, TimeSpan? ttl = null)
    {
        if (_seeded.TryGetValue(key, out var hit))
            return (T?)hit;

        FactoryCalls.Add(key);
        return await factory();
    }

    public Task RemoveAsync(params string[] keys)
    {
        RemoveCallCount++;
        RemovedKeys.AddRange(keys);
        return Task.CompletedTask;
    }
}
