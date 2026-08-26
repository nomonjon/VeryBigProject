using GrpcServer.Services.Caching;
using GrpcServer.Tests.TestKit;
using Microsoft.Extensions.Caching.Memory;

namespace GrpcServer.Tests.Services.Caching;

/// <summary>
/// Uses a real <see cref="MemoryCache"/> rather than a mocked <c>IMemoryCache</c>.
///
/// Mocking <c>IMemoryCache</c> means stubbing <c>TryGetValue</c> and <c>CreateEntry</c>,
/// which is a lot of setup that only proves the service calls the API you told it to.
/// The real implementation is in-process, allocation-only and deterministic, so it is
/// both a truer and a shorter test.
/// </summary>
public class MemoryCacheServiceTests : IDisposable
{
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly MemoryCacheService _sut;

    public MemoryCacheServiceTests() => _sut = new MemoryCacheService(_cache);

    public void Dispose() => _cache.Dispose();

    [Fact]
    public async Task GetOrSetAsync_InvokesFactory_OnMiss()
    {
        var calls = 0;

        var result = await _sut.GetOrSetAsync<string>("key", () => { calls++; return Task.FromResult<string?>("value"); });

        Assert.Equal("value", result);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task GetOrSetAsync_SkipsFactory_OnHit()
    {
        var calls = 0;
        Func<Task<string?>> factory = () => { calls++; return Task.FromResult<string?>("value"); };

        await _sut.GetOrSetAsync("key", factory);
        var second = await _sut.GetOrSetAsync("key", factory);

        Assert.Equal("value", second);
        Assert.Equal(1, calls); // the second call was served from the cache
    }

    [Fact]
    public async Task GetOrSetAsync_KeepsKeysIndependent()
    {
        await _sut.GetOrSetAsync<string>("a", () => Task.FromResult<string?>("first"));

        var result = await _sut.GetOrSetAsync<string>("b", () => Task.FromResult<string?>("second"));

        Assert.Equal("second", result);
    }

    [Fact]
    public async Task GetOrSetAsync_DoesNotCacheNull_SoAMissIsRetriedNextTime()
    {
        var calls = 0;
        Func<Task<string?>> factory = () => { calls++; return Task.FromResult<string?>(null); };

        await _sut.GetOrSetAsync("key", factory);
        await _sut.GetOrSetAsync("key", factory);

        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task GetOrSetAsync_CachesCollections_ByReference()
    {
        var stored = new List<string> { "a" };

        await _sut.GetOrSetAsync<List<string>>("key", () => Task.FromResult<List<string>?>(stored));
        var second = await _sut.GetOrSetAsync<List<string>>("key", () => Task.FromResult<List<string>?>(new List<string> { "b" }));

        // In-process caching hands back the *same instance*, unlike Redis which
        // round-trips through JSON. Callers must therefore not mutate what they read.
        Assert.Same(stored, second);
    }

    [Fact]
    public async Task RemoveAsync_EvictsTheKey_SoTheFactoryRunsAgain()
    {
        var calls = 0;
        Func<Task<string?>> factory = () => { calls++; return Task.FromResult<string?>("value"); };

        await _sut.GetOrSetAsync("key", factory);
        await _sut.RemoveAsync("key");
        await _sut.GetOrSetAsync("key", factory);

        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task RemoveAsync_EvictsEveryKeyItIsGiven()
    {
        await _sut.GetOrSetAsync<string>("a", () => Task.FromResult<string?>("1"));
        await _sut.GetOrSetAsync<string>("b", () => Task.FromResult<string?>("2"));

        await _sut.RemoveAsync("a", "b");

        Assert.False(_cache.TryGetValue("a", out _));
        Assert.False(_cache.TryGetValue("b", out _));
    }

    [Fact]
    public async Task RemoveAsync_LeavesUnrelatedKeysAlone()
    {
        await _sut.GetOrSetAsync<string>("keep", () => Task.FromResult<string?>("value"));

        await _sut.RemoveAsync("other");

        Assert.True(_cache.TryGetValue("keep", out _));
    }

    [Fact]
    public async Task RemoveAsync_AcceptsNoKeys()
    {
        var exception = await Record.ExceptionAsync(() => _sut.RemoveAsync());

        Assert.Null(exception);
    }

    [Fact]
    public async Task GetOrSetAsync_ExpiresTheEntry_AfterTheSuppliedTtl()
    {
        var (clock, sut, _) = CreateWithFakeClock();
        var calls = 0;
        Func<Task<string?>> factory = () => { calls++; return Task.FromResult<string?>("value"); };

        await sut.GetOrSetAsync("key", factory, TimeSpan.FromSeconds(60));
        clock.Advance(TimeSpan.FromSeconds(59));
        await sut.GetOrSetAsync("key", factory);   // still inside the TTL -> hit

        Assert.Equal(1, calls);

        clock.Advance(TimeSpan.FromSeconds(2));
        await sut.GetOrSetAsync("key", factory);   // TTL lapsed -> miss

        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task GetOrSetAsync_FallsBackToAOneMinuteTtl_WhenNoneIsGiven()
    {
        var (clock, sut, _) = CreateWithFakeClock();
        var calls = 0;
        Func<Task<string?>> factory = () => { calls++; return Task.FromResult<string?>("value"); };

        await sut.GetOrSetAsync("key", factory);
        clock.Advance(TimeSpan.FromSeconds(61));
        await sut.GetOrSetAsync("key", factory);

        Assert.Equal(2, calls);
    }

    private static (FakeClock Clock, MemoryCacheService Sut, MemoryCache Cache) CreateWithFakeClock()
    {
        var clock = new FakeClock();
        var cache = new MemoryCache(new MemoryCacheOptions { Clock = clock });
        return (clock, new MemoryCacheService(cache), cache);
    }
}
