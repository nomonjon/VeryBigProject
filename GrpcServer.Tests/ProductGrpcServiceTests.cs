using AutoFixture;
using AutoFixture.AutoMoq;
using Grpc.Core;
using GrpcServer.ApiServices;
using GrpcServer.Dtos;
using GrpcServer.Interfaces;
using GrpcServer.Services;
using GrpcServer.Models;
using Moq;
using Moq.Protected;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace GrpcServer.Tests
{
    public class ProductGrpcServiceTests
    {
        private readonly IFixture _fixture;
        private readonly Mock<IProductService> _productServiceMock;
        private readonly Mock<IProductRepository> _productRepositoryMock;
        private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
        private readonly ProductGrpcService _sut;

        public ProductGrpcServiceTests()
        {
            _fixture = new Fixture().Customize(new AutoMoqCustomization());
            _productServiceMock = _fixture.Freeze<Mock<IProductService>>();
            _productRepositoryMock = _fixture.Freeze<Mock<IProductRepository>>();
            _httpClientFactoryMock = _fixture.Freeze<Mock<IHttpClientFactory>>();
            _sut = new ProductGrpcService(_productServiceMock.Object, _productRepositoryMock.Object, _httpClientFactoryMock.Object);
        }

        [Theory]
        [InlineData("a45055da-8c5d-4f11-9fa6-2003c004a6cb")]
        public async Task GetById_ShouldReturnProductResponse_WhenProductExists(string idStr)
        {
            // Arrange
            var id = Guid.Parse(idStr);
            var product = _fixture.Build<ProductDto>()
                                  .With(p => p.Id, id)
                                  .With(p => p.Name, "Test Product")
                                  .With(p => p.Quantity, 10)
                                  .With(p => p.Price, 100.50m)
                                  .Create();

            _productServiceMock
                .Setup(s => s.GetProductByIdAsync(id))
                .ReturnsAsync(product);

            var request = new GetByIdRequest { Id = idStr };
            var contextMock = new Mock<ServerCallContext>();

            // Act
            var result = await _sut.GetById(request, contextMock.Object);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(idStr, result.Id);
            Assert.Equal(product.Name, result.Name);
            Assert.Equal(product.Quantity, result.Quantity);
            Assert.Equal((double)product.Price, result.Price);
            _productServiceMock.Verify(s => s.GetProductByIdAsync(id), Times.Once);
        }

        [Fact]
        public async Task GetById_ShouldThrowRpcException_WhenInvalidId()
        {
            // Arrange
            var request = new GetByIdRequest { Id = "invalid-id" };
            var contextMock = new Mock<ServerCallContext>();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<RpcException>(() => _sut.GetById(request, contextMock.Object));
            Assert.Equal(StatusCode.InvalidArgument, ex.Status.StatusCode);
        }

        [Theory]
        [InlineData("a45055da-8c5d-4f11-9fa6-2003c004a6cb")]
        public async Task GetById_ShouldThrowRpcException_WhenProductNotFound(string idStr)
        {
            // Arrange
            var id = Guid.Parse(idStr);
            _productServiceMock
                .Setup(s => s.GetProductByIdAsync(id))
                .ReturnsAsync((ProductDto)null);

            var request = new GetByIdRequest { Id = idStr };
            var contextMock = new Mock<ServerCallContext>();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<RpcException>(() => _sut.GetById(request, contextMock.Object));
            Assert.Equal(StatusCode.NotFound, ex.Status.StatusCode);
        }

        [Fact]
        public async Task Delete_ShouldReturnSuccess_WhenProductDeleted()
        {
            // Arrange
            var idStr = Guid.NewGuid().ToString();
            var id = Guid.Parse(idStr);
            
            _productServiceMock
                .Setup(s => s.DeleteProductAsync(id))
                .ReturnsAsync(true);

            var request = new DeleteProductRequest { Id = idStr };
            var contextMock = new Mock<ServerCallContext>();

            // Act
            var result = await _sut.Delete(request, contextMock.Object);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Success);
            _productServiceMock.Verify(s => s.DeleteProductAsync(id), Times.Once);
        }

        [Fact]
        public async Task GetAll_ShouldReturnAllProducts()
        {
            // Arrange
            var products = System.Linq.Enumerable.ToList(_fixture.CreateMany<Product>(2));
            _productRepositoryMock
                .Setup(repo => repo.GetAllAsync())
                .ReturnsAsync(products);

            var request = new GetAllRequest();
            var contextMock = new Mock<ServerCallContext>();

            // Act
            var result = await _sut.GetAll(request, contextMock.Object);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Products.Count);
            _productRepositoryMock.Verify(repo => repo.GetAllAsync(), Times.Once);
        }

        [Fact]
        public async Task Update_ShouldReturnProductResponse_WhenProductExists()
        {
            // Arrange
            var id = Guid.NewGuid();
            var updateDto = _fixture.Create<CreateUpdateProductDto>();
            var productDto = _fixture.Build<ProductDto>()
                                     .With(p => p.Id, id)
                                     .With(p => p.Name, updateDto.Name)
                                     .With(p => p.Quantity, updateDto.Quantity)
                                     .With(p => p.Price, updateDto.Price)
                                     .Create();

            _productServiceMock
                .Setup(s => s.UpdateProductAsync(id, It.IsAny<CreateUpdateProductDto>()))
                .ReturnsAsync(productDto);

            var request = new UpdateProductRequest 
            { 
                Id = id.ToString(), 
                Name = updateDto.Name, 
                Quantity = updateDto.Quantity, 
                Price = (double)updateDto.Price 
            };
            var contextMock = new Mock<ServerCallContext>();

            // Act
            var result = await _sut.Update(request, contextMock.Object);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(id.ToString(), result.Id);
            Assert.Equal(updateDto.Name, result.Name);
            _productServiceMock.Verify(s => s.UpdateProductAsync(id, It.IsAny<CreateUpdateProductDto>()), Times.Once);
        }

        [Fact]
        public async Task Create_ShouldReturnProductResponse_WhenProductIsCreated()
        {
            // Arrange
            var request = new CreateProductRequest
            {
                Name = "Test Product",
                Quantity = 10,
                Price = 99.99
            };

            var expectedDto = new CreateUpdateProductDto
            {
                Name = "Random Product",
                Quantity = 10,
                Price = 120.00m
            };

            var handlerMock = new Mock<HttpMessageHandler>();
            var responseMsg = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(expectedDto), System.Text.Encoding.UTF8, "application/json")
            };

            handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMsg);

            var httpClient = new HttpClient(handlerMock.Object) { BaseAddress = new Uri("http://localhost") };

            _httpClientFactoryMock.Setup(x => x.CreateClient("PriceRandomizer")).Returns(httpClient);

            var createdProduct = _fixture.Build<ProductDto>()
                .With(x => x.Id, Guid.NewGuid())
                .With(x => x.Name, expectedDto.Name)
                .With(x => x.Quantity, expectedDto.Quantity)
                .With(x => x.Price, expectedDto.Price)
                .Create();

            _productServiceMock
                .Setup(s => s.CreateProductAsync(It.IsAny<CreateUpdateProductDto>()))
                .ReturnsAsync(createdProduct);

            var contextMock = new Mock<ServerCallContext>();

            // Act
            var result = await _sut.Create(request, contextMock.Object);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(createdProduct.Id.ToString(), result.Id);
            Assert.Equal(expectedDto.Name, result.Name);
            _httpClientFactoryMock.Verify(x => x.CreateClient("PriceRandomizer"), Times.Once);
            _productServiceMock.Verify(s => s.CreateProductAsync(It.IsAny<CreateUpdateProductDto>()), Times.Once);
        }
    }
}
