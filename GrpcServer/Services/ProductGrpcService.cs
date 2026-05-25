using Grpc.Core;
using GrpcServer.ApiServices;
using GrpcServer.Dtos;
using GrpcServer.Interfaces;
using Microsoft.Extensions.Http;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

namespace GrpcServer.Services;

public class ProductGrpcService(IProductService productService, IProductRepository productRepository, IHttpClientFactory httpClientFactory)
    : ProductService.ProductServiceBase  // <- авто-сгенерировано из .proto
{
    public override async Task<GetAllResponse> GetAll(
        GetAllRequest request, ServerCallContext context)
    {
        var products = await productRepository.GetAllAsync();

        var response = new GetAllResponse();
        response.Products.AddRange(products.Select(p => new ProductResponse
        {
            Id = p.Id.ToString(),
            Name = p.Name,
            Quantity = p.Quantity,
            Price = (double)p.Price
        }));

        return response;
    }

    public override async Task<ProductResponse> GetById(
        GetByIdRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.Id, out var id))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid ID format"));

        var product = await productService.GetProductByIdAsync(id);

        if (product is null)
            throw new RpcException(new Status(StatusCode.NotFound, $"Product {id} not found"));

        return new ProductResponse
        {
            Id = product.Id.ToString(),
            Name = product.Name,
            Quantity = product.Quantity,
            Price = (double)product.Price
        };
    }

    public override async Task<ProductResponse> Create(
        CreateProductRequest request, ServerCallContext context)
    {
        var client = httpClientFactory.CreateClient("PriceRandomizer");

        var priceRequestDto = new
        {
            Name = request.Name,
            Quantity = request.Quantity,
            Price = request.Price
        };

        //Code to convert dto to jston and box it into Htttpcontent
        var content = new StringContent(JsonSerializer.Serialize(priceRequestDto), Encoding.UTF8, "application/json");

        var response = await client.PostAsync("api/Random/random-price", content);

        var dto = new CreateUpdateProductDto();
        if (response.IsSuccessStatusCode)
        {
            var randomized = await response.Content.ReadFromJsonAsync<CreateUpdateProductDto>();
            dto.Name = randomized!.Name;
            dto.Quantity = randomized!.Quantity;
            dto.Price = (decimal)randomized!.Price;
        }
        else
        {
            dto.Name = request.Name;
            dto.Quantity = request.Quantity;
            dto.Price = (decimal)request.Price;
        }

        Console.WriteLine($"Randomizer status: {response.StatusCode}");
        Console.WriteLine($"Randomizer response: {await response.Content.ReadAsStringAsync()}");

        var product = await productService.CreateProductAsync(dto);

        return new ProductResponse
        {
            Id = product.Id.ToString(),
            Name = product.Name,
            Quantity = product.Quantity,
            Price = (double)product.Price
        };
    }

    public override async Task<ProductResponse> Update(
        UpdateProductRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.Id, out var id))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid ID format"));

        var dto = new CreateUpdateProductDto
        {
            Name = request.Name,
            Quantity = request.Quantity,
            Price = (decimal)request.Price
        };

        var product = await productService.UpdateProductAsync(id, dto);

        if (product is null)
            throw new RpcException(new Status(StatusCode.NotFound, $"Product {id} not found"));

        return new ProductResponse
        {
            Id = product.Id.ToString(),
            Name = product.Name,
            Quantity = product.Quantity,
            Price = (double)product.Price
        };
    }

    public override async Task<DeleteProductResponse> Delete(
        DeleteProductRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.Id, out var id))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid ID format"));

        var result = await productService.DeleteProductAsync(id);

        return new DeleteProductResponse { Success = result };
    }
}
