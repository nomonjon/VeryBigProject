using Grpc.Core;
using GrpcServer;

namespace TaskTracker.Services;

public class ProductApiService(ProductService.ProductServiceClient client)
{
    public async Task<List<ProductDto>> GetAllAsync()
    {
        var response = await client.GetAllAsync(new GetAllRequest());
        return response.Products.Select(p => new ProductDto
        {
            Id = Guid.Parse(p.Id),
            Name = p.Name,
            Quantity = p.Quantity,
            Price = (decimal)p.Price
        }).ToList();
    }

    public async Task<ProductDto?> GetByIdAsync(Guid id)
    {

        var response = await client.GetByIdAsync(new GetByIdRequest { Id = id.ToString() });
        return new ProductDto
        {
            Id = Guid.Parse(response.Id),
            Name = response.Name,
            Quantity = response.Quantity,
            Price = (decimal)response.Price
        };

    }

    public async Task<ProductDto> CreateAsync(CreateUpdateProductDto dto)
    {
        var response = await client.CreateAsync(new CreateProductRequest
        {
            Name = dto.Name,
            Quantity = dto.Quantity,
            Price = (double)dto.Price
        });

        return new ProductDto
        {
            Id = Guid.Parse(response.Id),
            Name = response.Name,
            Quantity = response.Quantity,
            Price = (decimal)response.Price
        };
    }

    public async Task<ProductDto?> UpdateAsync(Guid id, CreateUpdateProductDto dto)
    {

        var response = await client.UpdateAsync(new UpdateProductRequest
        {
            Id = id.ToString(),
            Name = dto.Name,
            Quantity = dto.Quantity,
            Price = (double)dto.Price
        });

        return new ProductDto
        {
            Id = Guid.Parse(response.Id),
            Name = response.Name,
            Quantity = response.Quantity,
            Price = (decimal)response.Price
        };

    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var response = await client.DeleteAsync(new DeleteProductRequest { Id = id.ToString() });
        return response.Success;
    }
}

public class CreateUpdateProductDto
{
    public string Name { get; set; }
    public double Quantity { get; set; }
    public decimal Price { get; set; }
}

public class ProductDto
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public double Quantity { get; set; }
    public decimal Price { get; set; }
}