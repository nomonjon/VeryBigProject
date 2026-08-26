using Grpc.Core;
using GrpcServer;
using Moq;
using TaskTracker.Services;
using TaskTracker.Tests.TestKit;

namespace TaskTracker.Tests.Services;

/// <summary>
/// ProductApiService is TaskTracker's gRPC client for the product catalog. The
/// generated <c>ProductServiceClient</c> declares its methods virtual and exposes a
/// protected parameterless constructor specifically so it can be mocked — no channel,
/// no server, no port.
/// </summary>
public class ProductApiServiceTests
{
    private readonly Mock<ProductService.ProductServiceClient> _client = new();
    private readonly ProductApiService _sut;

    public ProductApiServiceTests() => _sut = new ProductApiService(_client.Object);

    [Fact]
    public async Task GetAllAsync_MapsEveryProduct()
    {
        var id = Guid.NewGuid();
        var response = new GetAllResponse();
        response.Products.Add(new ProductResponse
        {
            Id = id.ToString(), Name = "Laptop", Quantity = 3, Price = 999.5, StatusColor = "red"
        });
        _client.Setup(c => c.GetAllAsync(It.IsAny<GetAllRequest>(), null, null, default))
               .Returns(GrpcCall.Returning(response));

        var products = await _sut.GetAllAsync();

        var product = Assert.Single(products);
        Assert.Equal(id, product.Id);
        Assert.Equal("Laptop", product.Name);
        Assert.Equal(3, product.Quantity);
        Assert.Equal(999.5m, product.Price);
        Assert.Equal("red", product.StatusColor);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAnEmptyList_WhenTheCatalogIsEmpty()
    {
        _client.Setup(c => c.GetAllAsync(It.IsAny<GetAllRequest>(), null, null, default))
               .Returns(GrpcCall.Returning(new GetAllResponse()));

        Assert.Empty(await _sut.GetAllAsync());
    }

    [Fact]
    public async Task GetByIdAsync_SendsTheIdAsAString_AndMapsTheResponse()
    {
        var id = Guid.NewGuid();
        GetByIdRequest? sent = null;
        _client.Setup(c => c.GetByIdAsync(It.IsAny<GetByIdRequest>(), null, null, default))
               .Callback<GetByIdRequest, Metadata, DateTime?, CancellationToken>((r, _, _, _) => sent = r)
               .Returns(GrpcCall.Returning(new ProductResponse
               {
                   Id = id.ToString(), Name = "Laptop", Quantity = 1, Price = 10, StatusColor = "green"
               }));

        var product = await _sut.GetByIdAsync(id);

        Assert.Equal(id.ToString(), sent!.Id);
        Assert.Equal(id, product!.Id);
        Assert.Equal("Laptop", product.Name);
    }

    [Fact]
    public async Task CreateAsync_ForwardsTheSubmittedFields()
    {
        CreateProductRequest? sent = null;
        _client.Setup(c => c.CreateAsync(It.IsAny<CreateProductRequest>(), null, null, default))
               .Callback<CreateProductRequest, Metadata, DateTime?, CancellationToken>((r, _, _, _) => sent = r)
               .Returns(GrpcCall.Returning(NewProductResponse()));

        await _sut.CreateAsync(new CreateUpdateProductDto { Name = "Laptop", Quantity = 2, Price = 99.5m });

        Assert.Equal("Laptop", sent!.Name);
        Assert.Equal(2, sent.Quantity);
        Assert.Equal(99.5, sent.Price);
    }

    [Fact]
    public async Task CreateAsync_MapsTheCreatedProductBack()
    {
        var id = Guid.NewGuid();
        _client.Setup(c => c.CreateAsync(It.IsAny<CreateProductRequest>(), null, null, default))
               .Returns(GrpcCall.Returning(new ProductResponse
               {
                   Id = id.ToString(), Name = "Laptop", Quantity = 2, Price = 99.5, StatusColor = "green"
               }));

        var product = await _sut.CreateAsync(new CreateUpdateProductDto { Name = "Laptop" });

        Assert.Equal(id, product.Id);
        Assert.Equal(99.5m, product.Price);
    }

    [Fact]
    public async Task UpdateAsync_SendsTheRouteIdAlongsideTheBody()
    {
        var id = Guid.NewGuid();
        UpdateProductRequest? sent = null;
        _client.Setup(c => c.UpdateAsync(It.IsAny<UpdateProductRequest>(), null, null, default))
               .Callback<UpdateProductRequest, Metadata, DateTime?, CancellationToken>((r, _, _, _) => sent = r)
               .Returns(GrpcCall.Returning(NewProductResponse(id)));

        await _sut.UpdateAsync(id, new CreateUpdateProductDto { Name = "Renamed", Quantity = 4, Price = 12m });

        Assert.Equal(id.ToString(), sent!.Id);
        Assert.Equal("Renamed", sent.Name);
        Assert.Equal(4, sent.Quantity);
        Assert.Equal(12, sent.Price);
    }

    [Fact]
    public async Task DeleteAsync_ReturnsTheSuccessFlagFromTheServer()
    {
        _client.Setup(c => c.DeleteAsync(It.IsAny<DeleteProductRequest>(), null, null, default))
               .Returns(GrpcCall.Returning(new DeleteProductResponse { Success = true }));

        Assert.True(await _sut.DeleteAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFalse_WhenTheServerReportsNoDeletion()
    {
        _client.Setup(c => c.DeleteAsync(It.IsAny<DeleteProductRequest>(), null, null, default))
               .Returns(GrpcCall.Returning(new DeleteProductResponse { Success = false }));

        Assert.False(await _sut.DeleteAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task DeleteAsync_SendsTheIdAsAString()
    {
        var id = Guid.NewGuid();
        DeleteProductRequest? sent = null;
        _client.Setup(c => c.DeleteAsync(It.IsAny<DeleteProductRequest>(), null, null, default))
               .Callback<DeleteProductRequest, Metadata, DateTime?, CancellationToken>((r, _, _, _) => sent = r)
               .Returns(GrpcCall.Returning(new DeleteProductResponse { Success = true }));

        await _sut.DeleteAsync(id);

        Assert.Equal(id.ToString(), sent!.Id);
    }

    private static ProductResponse NewProductResponse(Guid? id = null) => new()
    {
        Id = (id ?? Guid.NewGuid()).ToString(),
        Name = "Laptop",
        Quantity = 1,
        Price = 10,
        StatusColor = "green"
    };
}
