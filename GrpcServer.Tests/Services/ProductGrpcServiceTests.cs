using System.Net;
using Grpc.Core;
using GrpcServer.Dtos;
using GrpcServer.Interfaces;
using GrpcServer.Mapper;
using GrpcServer.Models;
using GrpcServer.Services;
using GrpcServer.Tests.TestKit;
using Moq;

namespace GrpcServer.Tests.Services;

/// <summary>
/// The gRPC layer is a translator: proto message in, DTO out, and errors expressed as
/// <see cref="RpcException"/> status codes instead of HTTP codes. So these tests assert
/// on status codes and field mapping, never on business rules — those live in
/// <see cref="ApiServices.ProductServiceTests"/>.
/// </summary>
public class ProductGrpcServiceTests : TestBase
{
    private readonly Mock<IProductService> _productService = new();
    private readonly Mock<IHttpClientFactory> _httpClientFactory = new();
    private readonly ServerCallContext _context = TestServerCallContext.Create();

    private ProductGrpcService CreateSut() => new(_productService.Object, _httpClientFactory.Object);

    // ---------- GetAll ----------

    [Fact]
    public async Task GetAll_ReturnsEveryProduct_FromTheCachedService()
    {
        _productService.Setup(s => s.GetAllProductsAsync())
                       .ReturnsAsync([Make.Product(name: "A").ToProductDto(),
                                      Make.Product(name: "B").ToProductDto()]);

        var response = await CreateSut().GetAll(new GetAllRequest(), _context);

        Assert.Equal(["A", "B"], response.Products.Select(p => p.Name));
    }

    [Fact]
    public async Task GetAll_MapsEveryFieldOntoTheProtoMessage()
    {
        var product = Make.Product(name: "Laptop", quantity: 3, price: 999.5m, statusColor: ProductColors.Red);
        _productService.Setup(s => s.GetAllProductsAsync()).ReturnsAsync([product.ToProductDto()]);

        var response = await CreateSut().GetAll(new GetAllRequest(), _context);

        var message = Assert.Single(response.Products);
        Assert.Equal(product.Id.ToString(), message.Id);
        Assert.Equal("Laptop", message.Name);
        Assert.Equal(3, message.Quantity);
        Assert.Equal(999.5, message.Price);
        Assert.Equal(ProductColors.Red, message.StatusColor);
    }

    [Fact]
    public async Task GetAll_ReturnsAnEmptyResponse_WhenThereAreNoProducts()
    {
        _productService.Setup(s => s.GetAllProductsAsync()).ReturnsAsync([]);

        var response = await CreateSut().GetAll(new GetAllRequest(), _context);

        Assert.Empty(response.Products);
    }

    // ---------- GetById ----------

    [Fact]
    public async Task GetById_MapsTheProduct_WhenItExists()
    {
        var product = Make.Product(name: "Laptop", quantity: 10, price: 150.5m);
        _productService.Setup(s => s.GetProductByIdAsync(product.Id)).ReturnsAsync(product.ToProductDto());

        var response = await CreateSut().GetById(new GetByIdRequest { Id = product.Id.ToString() }, _context);

        Assert.Equal(product.Id.ToString(), response.Id);
        Assert.Equal("Laptop", response.Name);
        Assert.Equal(10, response.Quantity);
        Assert.Equal(150.5, response.Price);
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("")]
    [InlineData("12345")]
    public async Task GetById_ReportsInvalidArgument_ForAMalformedId(string id)
    {
        var exception = await Assert.ThrowsAsync<RpcException>(
            () => CreateSut().GetById(new GetByIdRequest { Id = id }, _context));

        Assert.Equal(StatusCode.InvalidArgument, exception.Status.StatusCode);
        _productService.Verify(s => s.GetProductByIdAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task GetById_ReportsNotFound_WhenTheProductIsMissing()
    {
        _productService.Setup(s => s.GetProductByIdAsync(It.IsAny<Guid>())).ReturnsAsync((ProductDto?)null);

        var exception = await Assert.ThrowsAsync<RpcException>(
            () => CreateSut().GetById(new GetByIdRequest { Id = Guid.NewGuid().ToString() }, _context));

        Assert.Equal(StatusCode.NotFound, exception.Status.StatusCode);
    }

    // ---------- Create ----------

    [Fact]
    public async Task Create_UsesThePriceRandomizerValues_WhenItAnswers()
    {
        var randomized = Make.ProductDto(name: "Randomized", quantity: 7, price: 42m);
        ArrangeRandomizer(StubHttpMessageHandler.ReturningJson(randomized));
        CaptureCreate();

        await CreateSut().Create(new CreateProductRequest { Name = "Submitted", Quantity = 1, Price = 5 }, _context);

        Assert.Equal("Randomized", _capturedCreate!.Name);
        Assert.Equal(7, _capturedCreate.Quantity);
        Assert.Equal(42m, _capturedCreate.Price);
    }

    [Fact]
    public async Task Create_FallsBackToTheSubmittedValues_WhenTheRandomizerReturnsAnErrorStatus()
    {
        ArrangeRandomizer(StubHttpMessageHandler.ReturningStatus(HttpStatusCode.ServiceUnavailable));
        CaptureCreate();

        await CreateSut().Create(new CreateProductRequest { Name = "Submitted", Quantity = 1, Price = 5 }, _context);

        Assert.Equal("Submitted", _capturedCreate!.Name);
        Assert.Equal(1, _capturedCreate.Quantity);
        Assert.Equal(5m, _capturedCreate.Price);
    }

    [Fact]
    public async Task Create_FallsBackToTheSubmittedValues_WhenTheRandomizerIsUnreachable()
    {
        // Running outside the Docker network, the hostname does not resolve at all.
        ArrangeRandomizer(StubHttpMessageHandler.Throwing(new HttpRequestException("no such host")));
        CaptureCreate();

        await CreateSut().Create(new CreateProductRequest { Name = "Submitted", Quantity = 1, Price = 5 }, _context);

        Assert.Equal("Submitted", _capturedCreate!.Name);
    }

    [Fact]
    public async Task Create_FallsBackToTheSubmittedValues_WhenTheRandomizerReturnsNullJson()
    {
        ArrangeRandomizer(StubHttpMessageHandler.ReturningJson<CreateUpdateProductDto?>(null));
        CaptureCreate();

        await CreateSut().Create(new CreateProductRequest { Name = "Submitted", Quantity = 1, Price = 5 }, _context);

        Assert.Equal("Submitted", _capturedCreate!.Name);
    }

    [Fact]
    public async Task Create_ReturnsTheStoredProduct()
    {
        ArrangeRandomizer(StubHttpMessageHandler.ReturningStatus(HttpStatusCode.ServiceUnavailable));
        var stored = Make.Product(name: "Stored", statusColor: ProductColors.Orange);
        _productService.Setup(s => s.CreateProductAsync(It.IsAny<CreateUpdateProductDto>()))
                       .ReturnsAsync(stored.ToProductDto());

        var response = await CreateSut().Create(new CreateProductRequest { Name = "Submitted" }, _context);

        Assert.Equal(stored.Id.ToString(), response.Id);
        Assert.Equal("Stored", response.Name);
        Assert.Equal(ProductColors.Orange, response.StatusColor);
    }

    [Fact]
    public async Task Create_AsksTheFactoryForThePriceRandomizerClient()
    {
        ArrangeRandomizer(StubHttpMessageHandler.ReturningStatus(HttpStatusCode.OK, "{}"));
        CaptureCreate();

        await CreateSut().Create(new CreateProductRequest { Name = "Submitted" }, _context);

        _httpClientFactory.Verify(f => f.CreateClient("PriceRandomizer"), Times.Once);
    }

    [Fact]
    public async Task Create_PostsToTheRandomPriceEndpoint()
    {
        var handler = StubHttpMessageHandler.ReturningStatus(HttpStatusCode.ServiceUnavailable);
        ArrangeRandomizer(handler);
        CaptureCreate();

        await CreateSut().Create(new CreateProductRequest { Name = "Submitted" }, _context);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.EndsWith("api/Random/random-price", request.RequestUri!.ToString());
    }

    // ---------- Update ----------

    [Fact]
    public async Task Update_MapsTheStoredProduct_WhenItExists()
    {
        var id = Guid.NewGuid();
        _productService.Setup(s => s.UpdateProductAsync(id, It.IsAny<CreateUpdateProductDto>()))
                       .ReturnsAsync(Make.Product(id: id, name: "Updated").ToProductDto());

        var response = await CreateSut().Update(
            new UpdateProductRequest { Id = id.ToString(), Name = "Updated", Quantity = 2, Price = 20 }, _context);

        Assert.Equal(id.ToString(), response.Id);
        Assert.Equal("Updated", response.Name);
    }

    [Fact]
    public async Task Update_ForwardsTheSubmittedFields()
    {
        var id = Guid.NewGuid();
        CreateUpdateProductDto? sent = null;
        _productService.Setup(s => s.UpdateProductAsync(id, It.IsAny<CreateUpdateProductDto>()))
                       .Callback<Guid, CreateUpdateProductDto>((_, d) => sent = d)
                       .ReturnsAsync(Make.Product(id: id).ToProductDto());

        await CreateSut().Update(
            new UpdateProductRequest { Id = id.ToString(), Name = "New", Quantity = 9, Price = 12.5 }, _context);

        Assert.Equal("New", sent!.Name);
        Assert.Equal(9, sent.Quantity);
        Assert.Equal(12.5m, sent.Price);
    }

    [Fact]
    public async Task Update_ReportsInvalidArgument_ForAMalformedId()
    {
        var exception = await Assert.ThrowsAsync<RpcException>(
            () => CreateSut().Update(new UpdateProductRequest { Id = "nope" }, _context));

        Assert.Equal(StatusCode.InvalidArgument, exception.Status.StatusCode);
    }

    [Fact]
    public async Task Update_ReportsNotFound_WhenTheProductIsMissing()
    {
        _productService.Setup(s => s.UpdateProductAsync(It.IsAny<Guid>(), It.IsAny<CreateUpdateProductDto>()))
                       .ReturnsAsync((ProductDto?)null);

        var exception = await Assert.ThrowsAsync<RpcException>(
            () => CreateSut().Update(new UpdateProductRequest { Id = Guid.NewGuid().ToString() }, _context));

        Assert.Equal(StatusCode.NotFound, exception.Status.StatusCode);
    }

    // ---------- Delete ----------

    [Fact]
    public async Task Delete_ReportsSuccess_WhenTheProductWasDeleted()
    {
        var id = Guid.NewGuid();
        _productService.Setup(s => s.DeleteProductAsync(id)).ReturnsAsync(true);

        var response = await CreateSut().Delete(new DeleteProductRequest { Id = id.ToString() }, _context);

        Assert.True(response.Success);
    }

    [Fact]
    public async Task Delete_ReportsFailure_WhenThereWasNothingToDelete()
    {
        _productService.Setup(s => s.DeleteProductAsync(It.IsAny<Guid>())).ReturnsAsync(false);

        var response = await CreateSut().Delete(new DeleteProductRequest { Id = Guid.NewGuid().ToString() }, _context);

        Assert.False(response.Success);
    }

    [Fact]
    public async Task Delete_ReportsInvalidArgument_ForAMalformedId()
    {
        var exception = await Assert.ThrowsAsync<RpcException>(
            () => CreateSut().Delete(new DeleteProductRequest { Id = "nope" }, _context));

        Assert.Equal(StatusCode.InvalidArgument, exception.Status.StatusCode);
        _productService.Verify(s => s.DeleteProductAsync(It.IsAny<Guid>()), Times.Never);
    }

    // ---------- helpers ----------

    private CreateUpdateProductDto? _capturedCreate;

    private void ArrangeRandomizer(StubHttpMessageHandler handler)
        => _httpClientFactory.Setup(f => f.CreateClient("PriceRandomizer")).Returns(handler.CreateClient());

    /// <summary>Records the DTO the gRPC layer finally handed to the product service.</summary>
    private void CaptureCreate()
        => _productService.Setup(s => s.CreateProductAsync(It.IsAny<CreateUpdateProductDto>()))
                          .Callback<CreateUpdateProductDto>(d => _capturedCreate = d)
                          .ReturnsAsync(Make.Product().ToProductDto());
}
