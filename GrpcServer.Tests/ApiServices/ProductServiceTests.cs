using GrpcServer.Dtos;
using GrpcServer.Interfaces;
using GrpcServer.Mapper;
using GrpcServer.Models;
using GrpcServer.Services.Caching;
using GrpcServer.Tests.TestKit;
using Moq;

namespace GrpcServer.Tests.ApiServices;

// The .proto also generates a static `GrpcServer.ProductService` (the gRPC stub base).
// This namespace sits *under* GrpcServer, and C# resolves names from the innermost
// enclosing namespace outwards, so that generated type wins over any using directive
// placed above the namespace. Declaring the alias *inside* the namespace settles it.
using ProductService = GrpcServer.ApiServices.ProductService;

/// <summary>
/// ProductService is a cache-aside layer over the repository. Two things are worth
/// testing here and nothing else: which repository call is made, and which cache keys
/// are invalidated afterwards. A stale cache key is the bug this class actually ships.
/// </summary>
public class ProductServiceTests : TestBase
{
    private readonly Mock<IProductRepository> _repository = new();
    private readonly FakeCacheService _cache = new();
    private readonly ProductService _sut;

    public ProductServiceTests() => _sut = new ProductService(_repository.Object, _cache);

    // ---------- CreateProductAsync ----------

    [Fact]
    public async Task CreateProductAsync_PersistsTheMappedProduct()
    {
        var dto = Make.ProductDto(name: "Keyboard", quantity: 5, price: 49.99m);
        Product? persisted = null;
        _repository.Setup(r => r.CreateAsync(It.IsAny<Product>()))
                   .Callback<Product>(p => persisted = p)
                   .ReturnsAsync((Product p) => p);

        await _sut.CreateProductAsync(dto);

        Assert.NotNull(persisted);
        Assert.Equal("Keyboard", persisted!.Name);
        Assert.Equal(5, persisted.Quantity);
        Assert.Equal(49.99m, persisted.Price);
    }

    [Fact]
    public async Task CreateProductAsync_ReturnsTheDtoWithTheIdTheDatabaseAssigned()
    {
        var assignedId = Guid.NewGuid();
        _repository.Setup(r => r.CreateAsync(It.IsAny<Product>()))
                   .ReturnsAsync((Product p) => { p.Id = assignedId; return p; });

        var result = await _sut.CreateProductAsync(Make.ProductDto());

        Assert.Equal(assignedId, result.Id);
    }

    [Fact]
    public async Task CreateProductAsync_InvalidatesTheProductList()
    {
        _repository.Setup(r => r.CreateAsync(It.IsAny<Product>())).ReturnsAsync((Product p) => p);

        await _sut.CreateProductAsync(Make.ProductDto());

        Assert.Equal([CacheKeys.ProductList], _cache.RemovedKeys);
    }

    // ---------- GetAllProductsAsync ----------

    [Fact]
    public async Task GetAllProductsAsync_ReadsThroughTheProductListKey()
    {
        _repository.Setup(r => r.GetAllAsync()).ReturnsAsync([Make.Product()]);

        await _sut.GetAllProductsAsync();

        Assert.Equal([CacheKeys.ProductList], _cache.FactoryCalls);
    }

    [Fact]
    public async Task GetAllProductsAsync_MapsEveryProduct()
    {
        var products = new List<Product> { Make.Product(name: "A"), Make.Product(name: "B") };
        _repository.Setup(r => r.GetAllAsync()).ReturnsAsync(products);

        var result = await _sut.GetAllProductsAsync();

        Assert.Equal(["A", "B"], result.Select(p => p.Name));
    }

    [Fact]
    public async Task GetAllProductsAsync_ServesFromCache_WithoutTouchingTheRepository()
    {
        _cache.Seed(CacheKeys.ProductList, new List<ProductDto> { Make.Product(name: "Cached").ToProductDto() });

        var result = await _sut.GetAllProductsAsync();

        Assert.Equal("Cached", Assert.Single(result).Name);
        _repository.Verify(r => r.GetAllAsync(), Times.Never);
    }

    [Fact]
    public async Task GetAllProductsAsync_ReturnsEmptyList_RatherThanNull()
    {
        _cache.Seed<List<ProductDto>>(CacheKeys.ProductList, null);

        var result = await _sut.GetAllProductsAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAllProductsAsync_ReturnsEmptyList_WhenTheRepositoryHasNothing()
    {
        _repository.Setup(r => r.GetAllAsync()).ReturnsAsync([]);

        Assert.Empty(await _sut.GetAllProductsAsync());
    }

    // ---------- GetProductByIdAsync ----------

    [Fact]
    public async Task GetProductByIdAsync_ReadsThroughThePerProductKey()
    {
        var product = Make.Product();
        _repository.Setup(r => r.GetByIdAsync(product.Id)).ReturnsAsync(product);

        await _sut.GetProductByIdAsync(product.Id);

        Assert.Equal([CacheKeys.Product(product.Id)], _cache.FactoryCalls);
    }

    [Fact]
    public async Task GetProductByIdAsync_MapsTheProduct()
    {
        var product = Make.Product(name: "Monitor", statusColor: ProductColors.Red);
        _repository.Setup(r => r.GetByIdAsync(product.Id)).ReturnsAsync(product);

        var result = await _sut.GetProductByIdAsync(product.Id);

        Assert.NotNull(result);
        Assert.Equal(product.Id, result!.Id);
        Assert.Equal("Monitor", result.Name);
        Assert.Equal(ProductColors.Red, result.StatusColor);
    }

    [Fact]
    public async Task GetProductByIdAsync_ReturnsNull_WhenTheProductDoesNotExist()
    {
        _repository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Product?)null);

        Assert.Null(await _sut.GetProductByIdAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetProductByIdAsync_ServesFromCache_WithoutTouchingTheRepository()
    {
        var id = Guid.NewGuid();
        _cache.Seed(CacheKeys.Product(id), Make.Product(id: id, name: "Cached").ToProductDto());

        var result = await _sut.GetProductByIdAsync(id);

        Assert.Equal("Cached", result!.Name);
        _repository.Verify(r => r.GetByIdAsync(It.IsAny<Guid>()), Times.Never);
    }

    // ---------- UpdateProductAsync ----------

    [Fact]
    public async Task UpdateProductAsync_SendsTheRouteIdToTheRepository_NotAFreshOne()
    {
        var id = Guid.NewGuid();
        Product? sent = null;
        _repository.Setup(r => r.UpdateAsync(It.IsAny<Product>()))
                   .Callback<Product>(p => sent = p)
                   .ReturnsAsync((Product p) => p);

        await _sut.UpdateProductAsync(id, Make.ProductDto());

        Assert.Equal(id, sent!.Id);
    }

    [Fact]
    public async Task UpdateProductAsync_ReturnsTheRepositoryResult_NotTheSubmittedValues()
    {
        var id = Guid.NewGuid();
        // The repository copies only user-editable fields and keeps StatusColor,
        // so the response has to come from what the repository returned.
        _repository.Setup(r => r.UpdateAsync(It.IsAny<Product>()))
                   .ReturnsAsync(Make.Product(id: id, name: "Stored", statusColor: ProductColors.Red));

        var result = await _sut.UpdateProductAsync(id, Make.ProductDto(name: "Submitted"));

        Assert.Equal("Stored", result!.Name);
        Assert.Equal(ProductColors.Red, result.StatusColor);
    }

    [Fact]
    public async Task UpdateProductAsync_InvalidatesBothTheListAndTheProduct()
    {
        var id = Guid.NewGuid();
        _repository.Setup(r => r.UpdateAsync(It.IsAny<Product>())).ReturnsAsync((Product p) => p);

        await _sut.UpdateProductAsync(id, Make.ProductDto());

        Assert.Equal([CacheKeys.ProductList, CacheKeys.Product(id)], _cache.RemovedKeys);
    }

    [Fact]
    public async Task UpdateProductAsync_ReturnsNull_WhenTheProductDoesNotExist()
    {
        _repository.Setup(r => r.UpdateAsync(It.IsAny<Product>())).ReturnsAsync((Product?)null);

        Assert.Null(await _sut.UpdateProductAsync(Guid.NewGuid(), Make.ProductDto()));
    }

    [Fact]
    public async Task UpdateProductAsync_DoesNotInvalidateAnything_WhenTheUpdateMissed()
    {
        _repository.Setup(r => r.UpdateAsync(It.IsAny<Product>())).ReturnsAsync((Product?)null);

        await _sut.UpdateProductAsync(Guid.NewGuid(), Make.ProductDto());

        Assert.Empty(_cache.RemovedKeys);
    }

    // ---------- DeleteProductAsync ----------

    [Fact]
    public async Task DeleteProductAsync_ReturnsTrue_AndInvalidatesBothKeys()
    {
        var id = Guid.NewGuid();
        _repository.Setup(r => r.DeleteAsync(id)).ReturnsAsync(true);

        var result = await _sut.DeleteProductAsync(id);

        Assert.True(result);
        Assert.Equal([CacheKeys.ProductList, CacheKeys.Product(id)], _cache.RemovedKeys);
    }

    [Fact]
    public async Task DeleteProductAsync_ReturnsFalse_AndInvalidatesNothing_WhenTheProductWasNotThere()
    {
        _repository.Setup(r => r.DeleteAsync(It.IsAny<Guid>())).ReturnsAsync(false);

        var result = await _sut.DeleteProductAsync(Guid.NewGuid());

        Assert.False(result);
        Assert.Empty(_cache.RemovedKeys);
    }
}
