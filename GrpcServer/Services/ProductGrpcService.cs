using Grpc.Core;
using GrpcServer.ApiServices;
using GrpcServer.Dtos;
using GrpcServer.Interfaces;
using Microsoft.Extensions.Http;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

namespace GrpcServer.Services;

public class ProductGrpcService(IProductService productService, IHttpClientFactory httpClientFactory)
    : ProductService.ProductServiceBase  // <- авто-сгенерировано из .proto
{
    public override async Task<GetAllResponse> GetAll(
        GetAllRequest request, ServerCallContext context)
    {
        // Route through the cached service so gRPC GetAll shares the same
        // products:all entry as the REST list instead of hitting the DB directly.
        var products = await productService.GetAllProductsAsync();

        var response = new GetAllResponse();
        response.Products.AddRange(products.Select(p => new ProductResponse
        {
            Id = p.Id.ToString(),
            Name = p.Name,
            Quantity = p.Quantity,
            Price = (double)p.Price,
            StatusColor = p.StatusColor
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
            Price = (double)product.Price,
            StatusColor = product.StatusColor
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

        // The randomizer is an optional external service (not always reachable,
        // e.g. running outside the shared Docker network). Fall back to the
        // submitted values on any failure instead of crashing the create.
        var dto = new CreateUpdateProductDto
        {
            Name = request.Name,
            Quantity = request.Quantity,
            Price = (decimal)request.Price
        };

        try
        {
            var response = await client.PostAsync("api/Random/random-price", content);
            Console.WriteLine($"Randomizer status: {response.StatusCode}");

            if (response.IsSuccessStatusCode)
            {
                var randomized = await response.Content.ReadFromJsonAsync<CreateUpdateProductDto>();
                if (randomized is not null)
                {
                    dto.Name = randomized.Name;
                    dto.Quantity = randomized.Quantity;
                    dto.Price = (decimal)randomized.Price;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Randomizer unavailable, using submitted values: {ex.Message}");
        }

        var product = await productService.CreateProductAsync(dto);

        return new ProductResponse
        {
            Id = product.Id.ToString(),
            Name = product.Name,
            Quantity = product.Quantity,
            Price = (double)product.Price,
            StatusColor = product.StatusColor
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
            Price = (double)product.Price,
            StatusColor = product.StatusColor
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
