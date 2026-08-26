using GrpcServer.Dtos;
using GrpcServer.Interfaces;
using GrpcServer.Mapper;
using GrpcServer.Models;
using GrpcServer.Services.Caching;

namespace GrpcServer.ApiServices;

public class ProductService(IProductRepository productRepository, ICacheService cache) : IProductService
{
    // Short TTL: ProductRuleWorker repaints StatusColor out of band and evicts
    // what it touches, so this only bounds staleness from writes we miss.
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);

    public async Task<ProductDto> CreateProductAsync(CreateUpdateProductDto productDto)
    {
        var product = productDto.ToProduct();

        await productRepository.CreateAsync(product);
        await cache.RemoveAsync(CacheKeys.ProductList);
        return product.ToProductDto();
    }

    public async Task<bool> DeleteProductAsync(Guid id)
    {
        var result = await productRepository.DeleteAsync(id);

        if (result)
            await cache.RemoveAsync(CacheKeys.ProductList, CacheKeys.Product(id));

        return result;
    }

    public async Task<List<ProductDto>> GetAllProductsAsync()
    {
        var products = await cache.GetOrSetAsync(
            CacheKeys.ProductList,
            async () =>
            {
                var all = await productRepository.GetAllAsync();
                return all.Select(p => p.ToProductDto()).ToList();
            },
            Ttl);

        return products ?? [];
    }

    public async Task<ProductDto?> GetProductByIdAsync(Guid id)
    {
        return await cache.GetOrSetAsync(
            CacheKeys.Product(id),
            async () =>
            {
                var product = await productRepository.GetByIdAsync(id);
                return product?.ToProductDto();
            },
            Ttl);
    }

    public async Task<ProductDto?> UpdateProductAsync(Guid id, CreateUpdateProductDto productDto)
    {
        var product = productDto.ToProduct(id);
        var result = await productRepository.UpdateAsync(product);
        if (result is null)
            return null;

        await cache.RemoveAsync(CacheKeys.ProductList, CacheKeys.Product(id));

        return result.ToProductDto();
    }
}
