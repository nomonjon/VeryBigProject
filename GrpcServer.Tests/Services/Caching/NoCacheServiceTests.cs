using GrpcServer.Services.Caching;

namespace GrpcServer.Tests.Services.Caching;

public class NoCacheServiceTests
{
    private readonly NoCacheService _sut = new();

    [Fact]
    public async Task GetOrSetAsync_AlwaysInvokesTheFactory()
    {
        var calls = 0;

        await _sut.GetOrSetAsync<string>("key", () => { calls++; return Task.FromResult<string?>("value"); });
        await _sut.GetOrSetAsync<string>("key", () => { calls++; return Task.FromResult<string?>("value"); });
        await _sut.GetOrSetAsync<string>("key", () => { calls++; return Task.FromResult<string?>("value"); });

        Assert.Equal(3, calls);
    }

    [Fact]
    public async Task GetOrSetAsync_ReturnsWhateverTheFactoryProduced()
    {
        var result = await _sut.GetOrSetAsync<string>("key", () => Task.FromResult<string?>("value"));

        Assert.Equal("value", result);
    }

    [Fact]
    public async Task GetOrSetAsync_PropagatesNull()
    {
        var result = await _sut.GetOrSetAsync<string>("key", () => Task.FromResult<string?>(null));

        Assert.Null(result);
    }

    [Fact]
    public async Task RemoveAsync_IsANoOp()
    {
        var exception = await Record.ExceptionAsync(() => _sut.RemoveAsync("a", "b"));

        Assert.Null(exception);
    }

    [Fact]
    public async Task RemoveAsync_AcceptsNoKeys()
    {
        var exception = await Record.ExceptionAsync(() => _sut.RemoveAsync());

        Assert.Null(exception);
    }
}
