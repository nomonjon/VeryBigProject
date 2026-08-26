using GrpcServer.Controllers;
using GrpcServer.Dtos;
using GrpcServer.Interfaces;
using GrpcServer.Mapper;
using GrpcServer.Tests.TestKit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace GrpcServer.Tests.Controllers;

/// <summary>
/// A controller has one job: turn a service result into the right HTTP status code.
/// That is all these tests check. They call the action method directly — no
/// TestServer, no HTTP pipeline — because routing and model binding are ASP.NET's
/// code, not ours, and testing them here would only be slow.
///
/// The logger is <see cref="NullLogger{T}"/> rather than a mock: nothing asserts on
/// log output, so a mock would be pure noise.
/// </summary>
public class ProductControllerTests
{
    private readonly Mock<IProductService> _service = new();
    private readonly ProductController _sut;

    public ProductControllerTests() => _sut = new ProductController(_service.Object, NullLogger<ProductController>.Instance);

    [Fact]
    public async Task GetProducts_Returns200_WithTheList()
    {
        var products = new List<ProductDto> { Make.Product(name: "A").ToProductDto() };
        _service.Setup(s => s.GetAllProductsAsync()).ReturnsAsync(products);

        var result = await _sut.GetProducts();

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(products, ok.Value);
    }

    [Fact]
    public async Task GetProducts_Returns200_WithAnEmptyList()
    {
        _service.Setup(s => s.GetAllProductsAsync()).ReturnsAsync([]);

        var ok = Assert.IsType<OkObjectResult>(await _sut.GetProducts());

        Assert.Empty(Assert.IsAssignableFrom<IEnumerable<ProductDto>>(ok.Value));
    }

    [Fact]
    public async Task GetProduct_Returns200_WhenTheProductExists()
    {
        var product = Make.Product().ToProductDto();
        _service.Setup(s => s.GetProductByIdAsync(product.Id)).ReturnsAsync(product);

        var ok = Assert.IsType<OkObjectResult>(await _sut.GetProduct(product.Id));

        Assert.Same(product, ok.Value);
    }

    [Fact]
    public async Task GetProduct_Returns404_WhenTheProductIsMissing()
    {
        _service.Setup(s => s.GetProductByIdAsync(It.IsAny<Guid>())).ReturnsAsync((ProductDto?)null);

        Assert.IsType<NotFoundResult>(await _sut.GetProduct(Guid.NewGuid()));
    }

    [Fact]
    public async Task CreateProduct_Returns200_WithTheCreatedProduct()
    {
        var created = Make.Product(name: "New").ToProductDto();
        _service.Setup(s => s.CreateProductAsync(It.IsAny<CreateUpdateProductDto>())).ReturnsAsync(created);

        var ok = Assert.IsType<OkObjectResult>(await _sut.CreateProduct(Make.ProductDto()));

        Assert.Same(created, ok.Value);
    }

    [Fact]
    public async Task CreateProduct_ForwardsTheBodyToTheService()
    {
        var body = Make.ProductDto(name: "Body");
        _service.Setup(s => s.CreateProductAsync(It.IsAny<CreateUpdateProductDto>()))
                .ReturnsAsync(Make.Product().ToProductDto());

        await _sut.CreateProduct(body);

        _service.Verify(s => s.CreateProductAsync(body), Times.Once);
    }

    [Fact]
    public async Task UpdateProduct_Returns200_WhenTheProductExists()
    {
        var id = Guid.NewGuid();
        var updated = Make.Product(id: id).ToProductDto();
        _service.Setup(s => s.UpdateProductAsync(id, It.IsAny<CreateUpdateProductDto>())).ReturnsAsync(updated);

        var ok = Assert.IsType<OkObjectResult>(await _sut.UpdateProduct(id, Make.ProductDto()));

        Assert.Same(updated, ok.Value);
    }

    [Fact]
    public async Task UpdateProduct_Returns404_WhenTheProductIsMissing()
    {
        _service.Setup(s => s.UpdateProductAsync(It.IsAny<Guid>(), It.IsAny<CreateUpdateProductDto>()))
                .ReturnsAsync((ProductDto?)null);

        Assert.IsType<NotFoundResult>(await _sut.UpdateProduct(Guid.NewGuid(), Make.ProductDto()));
    }

    [Fact]
    public async Task DeleteProduct_Returns204_WhenTheProductWasDeleted()
    {
        _service.Setup(s => s.DeleteProductAsync(It.IsAny<Guid>())).ReturnsAsync(true);

        Assert.IsType<NoContentResult>(await _sut.DeleteProduct(Guid.NewGuid()));
    }

    [Fact]
    public async Task DeleteProduct_Returns404_WhenThereWasNothingToDelete()
    {
        _service.Setup(s => s.DeleteProductAsync(It.IsAny<Guid>())).ReturnsAsync(false);

        Assert.IsType<NotFoundResult>(await _sut.DeleteProduct(Guid.NewGuid()));
    }
}
