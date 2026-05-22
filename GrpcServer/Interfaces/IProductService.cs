using GrpcServer.Dtos;

namespace GrpcServer.Interfaces;

public interface IProductService
{
    Task<ProductDto> CreateProductAsync(CreateUpdateProductDto productDto);
    Task<ProductDto?> GetProductByIdAsync(Guid id);
    Task<List<ProductDto>> GetAllProductsAsync();
    Task<ProductDto?> UpdateProductAsync(Guid id, CreateUpdateProductDto productDto);
    Task<bool> DeleteProductAsync(Guid id);
}