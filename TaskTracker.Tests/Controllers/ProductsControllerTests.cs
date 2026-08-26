using Grpc.Core;
using GrpcServer;
using Microsoft.AspNetCore.Mvc;
using Moq;
using TaskTracker.Controllers;
using TaskTracker.Services;
using TaskTracker.Tests.TestKit;

namespace TaskTracker.Tests.Controllers;

/// <summary>
/// ProductsController takes the concrete <see cref="ProductApiService"/>, so the seam
/// is one level lower: the mocked gRPC client. Same pattern as AuthControllerTests.
/// </summary>
public class ProductsControllerTests
{
    private readonly Mock<ProductService.ProductServiceClient> _client = new();
    private readonly ProductsController _sut;

    public ProductsControllerTests() => _sut = new ProductsController(new ProductApiService(_client.Object));

    [Fact]
    public async Task GetAll_Returns200_WithTheCatalog()
    {
        var response = new GetAllResponse();
        response.Products.Add(NewProductResponse(name: "Laptop"));
        _client.Setup(c => c.GetAllAsync(It.IsAny<GetAllRequest>(), null, null, default))
               .Returns(GrpcCall.Returning(response));

        var ok = Assert.IsType<OkObjectResult>(await _sut.GetAll());

        var products = Assert.IsAssignableFrom<IEnumerable<ProductDto>>(ok.Value);
        Assert.Equal("Laptop", Assert.Single(products).Name);
    }

    [Fact]
    public async Task GetById_Returns200_WithTheProduct()
    {
        var id = Guid.NewGuid();
        _client.Setup(c => c.GetByIdAsync(It.IsAny<GetByIdRequest>(), null, null, default))
               .Returns(GrpcCall.Returning(NewProductResponse(id)));

        var ok = Assert.IsType<OkObjectResult>(await _sut.GetById(id));

        Assert.Equal(id, Assert.IsType<ProductDto>(ok.Value).Id);
    }

    [Fact]
    public async Task Create_Returns201_PointingAtTheNewProduct()
    {
        var id = Guid.NewGuid();
        _client.Setup(c => c.CreateAsync(It.IsAny<CreateProductRequest>(), null, null, default))
               .Returns(GrpcCall.Returning(NewProductResponse(id)));

        var created = Assert.IsType<CreatedAtActionResult>(
            await _sut.Create(new CreateUpdateProductDto { Name = "Laptop" }));

        Assert.Equal(nameof(ProductsController.GetById), created.ActionName);
        Assert.Equal(id, created.RouteValues!["id"]);
    }

    [Fact]
    public async Task Update_Returns200_WithTheUpdatedProduct()
    {
        var id = Guid.NewGuid();
        _client.Setup(c => c.UpdateAsync(It.IsAny<UpdateProductRequest>(), null, null, default))
               .Returns(GrpcCall.Returning(NewProductResponse(id, name: "Renamed")));

        var ok = Assert.IsType<OkObjectResult>(await _sut.Update(id, new CreateUpdateProductDto { Name = "Renamed" }));

        Assert.Equal("Renamed", Assert.IsType<ProductDto>(ok.Value).Name);
    }

    [Fact]
    public async Task Delete_Returns204_WhenTheProductWasDeleted()
    {
        _client.Setup(c => c.DeleteAsync(It.IsAny<DeleteProductRequest>(), null, null, default))
               .Returns(GrpcCall.Returning(new DeleteProductResponse { Success = true }));

        Assert.IsType<NoContentResult>(await _sut.Delete(Guid.NewGuid()));
    }

    [Fact]
    public async Task Delete_Returns404_WhenTheProductWasNotThere()
    {
        _client.Setup(c => c.DeleteAsync(It.IsAny<DeleteProductRequest>(), null, null, default))
               .Returns(GrpcCall.Returning(new DeleteProductResponse { Success = false }));

        Assert.IsType<NotFoundResult>(await _sut.Delete(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetById_PropagatesAnRpcException_WhenTheProductIsMissing()
    {
        // ProductApiService never returns null, so the controller's NotFound branch is
        // unreachable — a missing product surfaces as the gRPC NotFound status instead,
        // which nothing here translates into a 404. Pinned so the gap stays visible.
        _client.Setup(c => c.GetByIdAsync(It.IsAny<GetByIdRequest>(), null, null, default))
               .Throws(new RpcException(new Status(StatusCode.NotFound, "Product not found")));

        await Assert.ThrowsAsync<RpcException>(() => _sut.GetById(Guid.NewGuid()));
    }

    private static ProductResponse NewProductResponse(Guid? id = null, string name = "Laptop") => new()
    {
        Id = (id ?? Guid.NewGuid()).ToString(),
        Name = name,
        Quantity = 1,
        Price = 10,
        StatusColor = "green"
    };
}
