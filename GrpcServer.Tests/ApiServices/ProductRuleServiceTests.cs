using System.Linq.Expressions;
using GrpcServer.ApiServices;
using GrpcServer.Dtos;
using GrpcServer.Interfaces;
using GrpcServer.Models;
using GrpcServer.Services.Caching;
using GrpcServer.Tests.TestKit;
using Moq;

namespace GrpcServer.Tests.ApiServices;

public class ProductRuleServiceTests : TestBase
{
    private readonly Mock<IProductRuleRepository> _rules = new();
    private readonly Mock<IProductRepository> _products = new();
    private readonly FakeCacheService _cache = new();
    private readonly ProductRuleService _sut;

    public ProductRuleServiceTests() => _sut = new ProductRuleService(_rules.Object, _products.Object, _cache);

    // ---------- CreateRuleAsync ----------

    [Fact]
    public async Task CreateRuleAsync_PersistsTheRule_WhenExpressionAndColorAreValid()
    {
        var dto = Make.RuleDto(name: "Expensive", expression: "Price > 100 && Quantity < 5", color: ProductColors.Red);
        _rules.Setup(r => r.CreateAsync(It.IsAny<ProductRule>())).ReturnsAsync((ProductRule r) => r);

        var result = await _sut.CreateRuleAsync(dto);

        Assert.Equal("Expensive", result.Name);
        Assert.Equal("Price > 100 && Quantity < 5", result.Expression);
        Assert.Equal(ProductColors.Red, result.Color);
        _rules.Verify(r => r.CreateAsync(It.IsAny<ProductRule>()), Times.Once);
    }

    [Fact]
    public async Task CreateRuleAsync_StampsCreatedAtInUtc()
    {
        var before = DateTime.UtcNow;
        _rules.Setup(r => r.CreateAsync(It.IsAny<ProductRule>())).ReturnsAsync((ProductRule r) => r);

        var result = await _sut.CreateRuleAsync(Make.RuleDto());

        Assert.InRange(result.CreatedAt, before, DateTime.UtcNow);
    }

    [Theory]
    [InlineData("Price >>> 100")]
    [InlineData("NonExistingProperty == 5")]
    [InlineData("Price >")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateRuleAsync_Rejects_InvalidExpressions(string expression)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _sut.CreateRuleAsync(Make.RuleDto(expression: expression)));

        // Validation must happen before any write — a rejected rule leaves no trace.
        _rules.Verify(r => r.CreateAsync(It.IsAny<ProductRule>()), Times.Never);
        Assert.Empty(_cache.RemovedKeys);
    }

    [Theory]
    [InlineData("purple")]
    [InlineData("")]
    public async Task CreateRuleAsync_Rejects_UnknownColors(string color)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _sut.CreateRuleAsync(Make.RuleDto(color: color)));

        _rules.Verify(r => r.CreateAsync(It.IsAny<ProductRule>()), Times.Never);
    }

    [Fact]
    public async Task CreateRuleAsync_InvalidatesBothRuleLists()
    {
        _rules.Setup(r => r.CreateAsync(It.IsAny<ProductRule>())).ReturnsAsync((ProductRule r) => r);

        await _sut.CreateRuleAsync(Make.RuleDto());

        Assert.Equal([CacheKeys.RuleList, CacheKeys.ActiveRuleList], _cache.RemovedKeys);
    }

    // ---------- GetRuleByIdAsync ----------

    [Fact]
    public async Task GetRuleByIdAsync_ReadsThroughThePerRuleKey()
    {
        var rule = Make.Rule();
        _rules.Setup(r => r.GetByIdAsync(rule.Id)).ReturnsAsync(rule);

        await _sut.GetRuleByIdAsync(rule.Id);

        Assert.Equal([CacheKeys.Rule(rule.Id)], _cache.FactoryCalls);
    }

    [Fact]
    public async Task GetRuleByIdAsync_MapsTheRule()
    {
        var rule = Make.Rule(name: "Low stock", expression: "Quantity < 5");
        _rules.Setup(r => r.GetByIdAsync(rule.Id)).ReturnsAsync(rule);

        var result = await _sut.GetRuleByIdAsync(rule.Id);

        Assert.Equal(rule.Id, result!.Id);
        Assert.Equal("Low stock", result.Name);
        Assert.Equal("Quantity < 5", result.Expression);
    }

    [Fact]
    public async Task GetRuleByIdAsync_ReturnsNull_WhenTheRuleDoesNotExist()
    {
        _rules.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((ProductRule?)null);

        Assert.Null(await _sut.GetRuleByIdAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetRuleByIdAsync_ServesFromCache_WithoutTouchingTheRepository()
    {
        var id = Guid.NewGuid();
        _cache.Seed(CacheKeys.Rule(id), new ProductRuleDto { Id = id, Name = "Cached" });

        var result = await _sut.GetRuleByIdAsync(id);

        Assert.Equal("Cached", result!.Name);
        _rules.Verify(r => r.GetByIdAsync(It.IsAny<Guid>()), Times.Never);
    }

    // ---------- GetAllRulesAsync ----------

    [Fact]
    public async Task GetAllRulesAsync_MapsEveryRule_ThroughTheRuleListKey()
    {
        _rules.Setup(r => r.GetAllAsync()).ReturnsAsync([Make.Rule(name: "A"), Make.Rule(name: "B")]);

        var result = await _sut.GetAllRulesAsync();

        Assert.Equal(["A", "B"], result.Select(r => r.Name));
        Assert.Equal([CacheKeys.RuleList], _cache.FactoryCalls);
    }

    [Fact]
    public async Task GetAllRulesAsync_ReturnsEmptyList_RatherThanNull()
    {
        _cache.Seed<List<ProductRuleDto>>(CacheKeys.RuleList, null);

        Assert.Empty(await _sut.GetAllRulesAsync());
    }

    // ---------- UpdateRuleAsync ----------

    [Fact]
    public async Task UpdateRuleAsync_SendsTheRouteIdToTheRepository()
    {
        var id = Guid.NewGuid();
        ProductRule? sent = null;
        _rules.Setup(r => r.UpdateAsync(It.IsAny<ProductRule>()))
              .Callback<ProductRule>(r => sent = r)
              .ReturnsAsync((ProductRule r) => r);

        await _sut.UpdateRuleAsync(id, Make.RuleDto());

        Assert.Equal(id, sent!.Id);
    }

    [Theory]
    [InlineData("Price >")]
    [InlineData("")]
    public async Task UpdateRuleAsync_Rejects_InvalidExpressions(string expression)
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _sut.UpdateRuleAsync(Guid.NewGuid(), Make.RuleDto(expression: expression)));

        _rules.Verify(r => r.UpdateAsync(It.IsAny<ProductRule>()), Times.Never);
    }

    [Fact]
    public async Task UpdateRuleAsync_Rejects_UnknownColors()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _sut.UpdateRuleAsync(Guid.NewGuid(), Make.RuleDto(color: "purple")));

        _rules.Verify(r => r.UpdateAsync(It.IsAny<ProductRule>()), Times.Never);
    }

    [Fact]
    public async Task UpdateRuleAsync_InvalidatesBothListsAndTheRuleItself()
    {
        var id = Guid.NewGuid();
        _rules.Setup(r => r.UpdateAsync(It.IsAny<ProductRule>())).ReturnsAsync((ProductRule r) => r);

        await _sut.UpdateRuleAsync(id, Make.RuleDto());

        Assert.Equal([CacheKeys.RuleList, CacheKeys.ActiveRuleList, CacheKeys.Rule(id)], _cache.RemovedKeys);
    }

    [Fact]
    public async Task UpdateRuleAsync_ReturnsNull_AndInvalidatesNothing_WhenTheRuleDoesNotExist()
    {
        _rules.Setup(r => r.UpdateAsync(It.IsAny<ProductRule>())).ReturnsAsync((ProductRule?)null);

        var result = await _sut.UpdateRuleAsync(Guid.NewGuid(), Make.RuleDto());

        Assert.Null(result);
        Assert.Empty(_cache.RemovedKeys);
    }

    // ---------- DeleteRuleAsync ----------

    [Fact]
    public async Task DeleteRuleAsync_InvalidatesBothListsAndTheRule_WhenSomethingWasDeleted()
    {
        var id = Guid.NewGuid();
        _rules.Setup(r => r.DeleteAsync(id)).ReturnsAsync(true);

        var result = await _sut.DeleteRuleAsync(id);

        Assert.True(result);
        Assert.Equal([CacheKeys.RuleList, CacheKeys.ActiveRuleList, CacheKeys.Rule(id)], _cache.RemovedKeys);
    }

    [Fact]
    public async Task DeleteRuleAsync_InvalidatesNothing_WhenTheRuleWasNotThere()
    {
        _rules.Setup(r => r.DeleteAsync(It.IsAny<Guid>())).ReturnsAsync(false);

        var result = await _sut.DeleteRuleAsync(Guid.NewGuid());

        Assert.False(result);
        Assert.Empty(_cache.RemovedKeys);
    }

    // ---------- GetMatchingProductsAsync ----------

    [Fact]
    public async Task GetMatchingProductsAsync_ReturnsNull_WhenTheRuleDoesNotExist()
    {
        _rules.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((ProductRule?)null);

        Assert.Null(await _sut.GetMatchingProductsAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetMatchingProductsAsync_MapsWhateverTheRepositoryMatched()
    {
        var rule = Make.Rule(expression: "Price > 100");
        _rules.Setup(r => r.GetByIdAsync(rule.Id)).ReturnsAsync(rule);
        _products.Setup(p => p.GetWhereAsync(It.IsAny<Expression<Func<Product, bool>>>(), It.IsAny<int>()))
                 .ReturnsAsync([Make.Product(name: "A"), Make.Product(name: "B")]);

        var result = await _sut.GetMatchingProductsAsync(rule.Id);

        Assert.Equal(["A", "B"], result!.Select(p => p.Name));
    }

    [Fact]
    public async Task GetMatchingProductsAsync_TranslatesTheStoredExpression_IntoTheRepositoryPredicate()
    {
        var rule = Make.Rule(expression: "Price > 100");
        Expression<Func<Product, bool>>? captured = null;
        _rules.Setup(r => r.GetByIdAsync(rule.Id)).ReturnsAsync(rule);
        _products.Setup(p => p.GetWhereAsync(It.IsAny<Expression<Func<Product, bool>>>(), It.IsAny<int>()))
                 .Callback<Expression<Func<Product, bool>>, int>((e, _) => captured = e)
                 .ReturnsAsync([]);

        await _sut.GetMatchingProductsAsync(rule.Id);

        // Asserting "a predicate was passed" proves nothing. Compile it and check
        // it actually encodes the rule — that is the behaviour the endpoint sells.
        var predicate = captured!.Compile();
        Assert.True(predicate(Make.Product(price: 150m)));
        Assert.False(predicate(Make.Product(price: 50m)));
    }

    // ---------- EvaluateProductAsync ----------

    [Fact]
    public async Task EvaluateProductAsync_ReturnsNull_WhenTheProductDoesNotExist()
    {
        _products.Setup(p => p.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Product?)null);

        Assert.Null(await _sut.EvaluateProductAsync(Guid.NewGuid()));
    }

    [Theory]
    [InlineData("Price > 100", true)]
    [InlineData("Price < 100", false)]
    [InlineData("Quantity == 10 && Price >= 150", true)]
    [InlineData("Name.Contains(\"Lap\")", true)]
    [InlineData("Name.StartsWith(\"Phone\")", false)]
    public async Task EvaluateProductAsync_ReportsWhetherEachRuleMatches(string expression, bool expectedMatch)
    {
        var product = Make.Product(name: "Laptop", quantity: 10, price: 150m);
        var rule = Make.Rule(expression: expression);
        _products.Setup(p => p.GetByIdAsync(product.Id)).ReturnsAsync(product);
        _rules.Setup(r => r.GetActiveAsync()).ReturnsAsync([rule]);

        var result = await _sut.EvaluateProductAsync(product.Id);

        var match = Assert.Single(result!);
        Assert.Equal(rule.Id, match.RuleId);
        Assert.Equal(expectedMatch, match.IsMatch);
    }

    [Fact]
    public async Task EvaluateProductAsync_ReturnsOneRowPerActiveRule_CarryingTheRuleMetadata()
    {
        var product = Make.Product(quantity: 2);
        var lowStock = Make.Rule(name: "Low stock", expression: "Quantity < 5", color: ProductColors.Orange);
        var expensive = Make.Rule(name: "Expensive", expression: "Price > 1000", color: ProductColors.Red);
        _products.Setup(p => p.GetByIdAsync(product.Id)).ReturnsAsync(product);
        _rules.Setup(r => r.GetActiveAsync()).ReturnsAsync([lowStock, expensive]);

        var result = await _sut.EvaluateProductAsync(product.Id);

        Assert.Equal(2, result!.Count);
        Assert.Equal("Low stock", result[0].RuleName);
        Assert.Equal(ProductColors.Orange, result[0].Color);
        Assert.True(result[0].IsMatch);
        Assert.Equal("Expensive", result[1].RuleName);
        Assert.False(result[1].IsMatch);
    }

    [Fact]
    public async Task EvaluateProductAsync_ReturnsEmptyList_WhenNoRuleIsActive()
    {
        var product = Make.Product();
        _products.Setup(p => p.GetByIdAsync(product.Id)).ReturnsAsync(product);
        _rules.Setup(r => r.GetActiveAsync()).ReturnsAsync([]);

        Assert.Empty(await _sut.EvaluateProductAsync(product.Id) ?? []);
    }

    [Fact]
    public async Task EvaluateProductAsync_ReadsActiveRules_ThroughTheActiveRuleListKey()
    {
        var product = Make.Product();
        _products.Setup(p => p.GetByIdAsync(product.Id)).ReturnsAsync(product);
        _rules.Setup(r => r.GetActiveAsync()).ReturnsAsync([]);

        await _sut.EvaluateProductAsync(product.Id);

        Assert.Equal([CacheKeys.ActiveRuleList], _cache.FactoryCalls);
    }

    // ---------- ApplyActiveRulesAsync (the 15s sweep) ----------

    [Fact]
    public async Task ApplyActiveRulesAsync_PaintsAMatchingProduct_WithTheRuleColor()
    {
        var lowStock = Make.Product(name: "Apple", quantity: 2);
        var healthy = Make.Product(name: "Keyboard", quantity: 20);
        ArrangeSweep(rules: [Make.Rule(expression: "Quantity < 5", color: ProductColors.Red)],
                     products: [lowStock, healthy]);

        var changed = await _sut.ApplyActiveRulesAsync();

        Assert.Equal(1, changed);
        Assert.Equal(ProductColors.Red, lowStock.StatusColor);
        Assert.Equal(ProductColors.Green, healthy.StatusColor);
        _products.Verify(p => p.UpdateAsync(It.Is<Product>(x => x.Id == lowStock.Id)), Times.Once);
        _products.Verify(p => p.UpdateAsync(It.Is<Product>(x => x.Id == healthy.Id)), Times.Never);
    }

    [Fact]
    public async Task ApplyActiveRulesAsync_UsesTheMostSevereColor_WhenSeveralRulesMatch()
    {
        var product = Make.Product(quantity: 0);
        ArrangeSweep(
            rules:
            [
                Make.Rule(expression: "Quantity < 10", color: ProductColors.Orange),
                Make.Rule(expression: "Quantity == 0", color: ProductColors.Red)
            ],
            products: [product]);

        await _sut.ApplyActiveRulesAsync();

        Assert.Equal(ProductColors.Red, product.StatusColor);
    }

    [Fact]
    public async Task ApplyActiveRulesAsync_IsIndependentOfRuleOrder()
    {
        var product = Make.Product(quantity: 0);
        ArrangeSweep(
            rules:
            [
                Make.Rule(expression: "Quantity == 0", color: ProductColors.Red),
                Make.Rule(expression: "Quantity < 10", color: ProductColors.Orange)
            ],
            products: [product]);

        await _sut.ApplyActiveRulesAsync();

        Assert.Equal(ProductColors.Red, product.StatusColor);
    }

    [Fact]
    public async Task ApplyActiveRulesAsync_ResetsToTheDefaultColor_WhenNoRuleMatchesAnyMore()
    {
        var product = Make.Product(quantity: 5, statusColor: ProductColors.Red);
        ArrangeSweep(rules: [], products: [product]);

        var changed = await _sut.ApplyActiveRulesAsync();

        Assert.Equal(1, changed);
        Assert.Equal(ProductColors.Green, product.StatusColor);
    }

    [Fact]
    public async Task ApplyActiveRulesAsync_SkipsProductsThatAlreadyHaveTheRightColor()
    {
        var product = Make.Product(quantity: 2, statusColor: ProductColors.Red);
        ArrangeSweep(rules: [Make.Rule(expression: "Quantity < 5", color: ProductColors.Red)],
                     products: [product]);

        var changed = await _sut.ApplyActiveRulesAsync();

        Assert.Equal(0, changed);
        _products.Verify(p => p.UpdateAsync(It.IsAny<Product>()), Times.Never);
    }

    [Fact]
    public async Task ApplyActiveRulesAsync_EvictsEachRepaintedProduct_AndTheListOnce()
    {
        var first = Make.Product(quantity: 1);
        var second = Make.Product(quantity: 2);
        ArrangeSweep(rules: [Make.Rule(expression: "Quantity < 5", color: ProductColors.Red)],
                     products: [first, second]);

        await _sut.ApplyActiveRulesAsync();

        // Per-product eviction is what stops REST/gRPC reads serving the old color.
        Assert.Contains(CacheKeys.Product(first.Id), _cache.RemovedKeys);
        Assert.Contains(CacheKeys.Product(second.Id), _cache.RemovedKeys);
        Assert.Single(_cache.RemovedKeys, k => k == CacheKeys.ProductList);
    }

    [Fact]
    public async Task ApplyActiveRulesAsync_EvictsNothing_WhenNoColorChanged()
    {
        ArrangeSweep(rules: [], products: [Make.Product(statusColor: ProductColors.Green)]);

        await _sut.ApplyActiveRulesAsync();

        Assert.Empty(_cache.RemovedKeys);
    }

    [Fact]
    public async Task ApplyActiveRulesAsync_ProcessesAtMostTenProductsPerSweep()
    {
        var products = Enumerable.Range(0, 25).Select(_ => Make.Product(quantity: 1)).ToList();
        ArrangeSweep(rules: [Make.Rule(expression: "Quantity < 5", color: ProductColors.Red)], products: products);

        var changed = await _sut.ApplyActiveRulesAsync();

        // The sweep is a background job on a 15s timer; batching keeps one tick bounded.
        Assert.Equal(10, changed);
        _products.Verify(p => p.UpdateAsync(It.IsAny<Product>()), Times.Exactly(10));
    }

    [Fact]
    public async Task ApplyActiveRulesAsync_OnlyConsidersProductsLastCheckedOverTenMinutesAgo()
    {
        Expression<Func<Product, bool>>? captured = null;
        _rules.Setup(r => r.GetActiveAsync()).ReturnsAsync([]);
        _products.Setup(p => p.GetWhereAsync2(It.IsAny<Expression<Func<Product, bool>>>()))
                 .Callback<Expression<Func<Product, bool>>>(e => captured = e)
                 .Returns(new List<Product>().AsQueryable());

        await _sut.ApplyActiveRulesAsync();

        var predicate = captured!.Compile();
        Assert.True(predicate(Make.Product(lastCheckedTime: DateTime.Now.AddMinutes(-30))));
        Assert.False(predicate(Make.Product(lastCheckedTime: DateTime.Now.AddMinutes(-1))));
    }

    [Fact]
    public async Task ApplyActiveRulesAsync_NeverPicksUpProductsThatWereNeverChecked()
    {
        Expression<Func<Product, bool>>? captured = null;
        _rules.Setup(r => r.GetActiveAsync()).ReturnsAsync([]);
        _products.Setup(p => p.GetWhereAsync2(It.IsAny<Expression<Func<Product, bool>>>()))
                 .Callback<Expression<Func<Product, bool>>>(e => captured = e)
                 .Returns(new List<Product>().AsQueryable());

        await _sut.ApplyActiveRulesAsync();

        // KNOWN GAP, pinned deliberately: LastCheckedTime is nullable and the sweep
        // filters on `p.LastCheckedTime <= cutoff`. In both C# and SQL a NULL comparison
        // is false, so a freshly created product (LastCheckedTime == null) is never
        // swept and keeps the default color forever. This test documents today's
        // behaviour — flip it to Assert.True the day the filter is fixed.
        Assert.False(captured!.Compile()(Make.Product(lastCheckedTime: null)));
    }

    [Fact]
    public async Task ApplyActiveRulesAsync_ReturnsZero_WhenNothingIsDue()
    {
        ArrangeSweep(rules: [Make.Rule()], products: []);

        Assert.Equal(0, await _sut.ApplyActiveRulesAsync());
    }

    /// <summary>
    /// The sweep reads active rules through the cache and pulls its batch through
    /// <c>GetWhereAsync2</c>. Every sweep test needs both, so it lives in one place.
    /// </summary>
    private void ArrangeSweep(List<ProductRule> rules, List<Product> products)
    {
        _rules.Setup(r => r.GetActiveAsync()).ReturnsAsync(rules);
        _products.Setup(p => p.GetWhereAsync2(It.IsAny<Expression<Func<Product, bool>>>()))
                 .Returns(products.AsQueryable());
        _products.Setup(p => p.UpdateAsync(It.IsAny<Product>())).ReturnsAsync((Product p) => p);
    }
}
