using GrpcServer.Dtos;
using GrpcServer.Interfaces;
using GrpcServer.Mapper;
using GrpcServer.Models;

namespace GrpcServer.ApiServices;

public class ProductService(IProductRepository productRepository) : IProductService
{
    public async Task<ProductDto> CreateProductAsync(CreateUpdateProductDto productDto)
    {
        var product = productDto.ToProduct();

        await productRepository.CreateAsync(product);
        return product.ToProductDto();
    }

    public async Task<bool> DeleteProductAsync(Guid id)
    {
        var result = await productRepository.DeleteAsync(id);
        
        return result;
    }

    public async Task<List<ProductDto>> GetAllProductsAsync()
    {
        var products = await productRepository.GetAllAsync();
        return products.Select(p => p.ToProductDto()).ToList();
    }

    public async Task<ProductDto?> GetProductByIdAsync(Guid id)
    {
        var product = await productRepository.GetByIdAsync(id);

        if (product is null)
            return null;

        return product.ToProductDto();
    }

    public async Task<ProductDto?> UpdateProductAsync(Guid id,CreateUpdateProductDto productDto)
    {
        var product = productDto.ToProduct(id);
        var result = await productRepository.UpdateAsync(product);
        if (result is null)
            return null;

        return result.ToProductDto();
    }
}
