using GrpcServer.Services.Caching;

namespace GrpcServer.Tests.Services.Caching;

/// <summary>
/// These look like tests of constants, and mostly they are. They earn their place
/// because the key strings are a wire contract: Redis entries written by one deploy
/// are read by the next, and a rename that "just tidies things up" silently orphans
/// every live cache entry. Pinning the literal makes that a failing test instead.
/// </summary>
public class CacheKeysTests
{
    [Fact]
    public void ProductList_UsesTheDocumentedKey()
        => Assert.Equal("products:all", CacheKeys.ProductList);

    [Fact]
    public void RuleList_UsesTheDocumentedKey()
        => Assert.Equal("rules:all", CacheKeys.RuleList);

    [Fact]
    public void ActiveRuleList_UsesTheDocumentedKey()
        => Assert.Equal("rules:active", CacheKeys.ActiveRuleList);

    [Fact]
    public void Product_NamespacesTheIdUnderProduct()
    {
        var id = Guid.Parse("d18f5e92-7f99-4a0b-8d8a-36b0c26eb390");

        Assert.Equal("product:d18f5e92-7f99-4a0b-8d8a-36b0c26eb390", CacheKeys.Product(id));
    }

    [Fact]
    public void Rule_NamespacesTheIdUnderRule()
    {
        var id = Guid.Parse("d18f5e92-7f99-4a0b-8d8a-36b0c26eb390");

        Assert.Equal("rule:d18f5e92-7f99-4a0b-8d8a-36b0c26eb390", CacheKeys.Rule(id));
    }

    [Fact]
    public void Product_And_Rule_NeverCollide_ForTheSameId()
    {
        var id = Guid.NewGuid();

        Assert.NotEqual(CacheKeys.Product(id), CacheKeys.Rule(id));
    }

    [Fact]
    public void Product_ReturnsDistinctKeys_ForDistinctIds()
        => Assert.NotEqual(CacheKeys.Product(Guid.NewGuid()), CacheKeys.Product(Guid.NewGuid()));

    [Fact]
    public void ListKeys_AreAllDistinct()
    {
        var keys = new[] { CacheKeys.ProductList, CacheKeys.RuleList, CacheKeys.ActiveRuleList };

        Assert.Equal(keys.Length, keys.Distinct().Count());
    }
}
