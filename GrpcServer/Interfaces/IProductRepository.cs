using GrpcServer.Models;

namespace GrpcServer.Interfaces;

public interface IProductRepository
{
    Task<Product> CreateAsync(Product product);
    Task<Product?> GetByIdAsync(Guid id);
    Task<List<Product>> GetAllAsync();
    Task<Product?> UpdateAsync(Product product);
    Task<bool> DeleteAsync(Guid id);
}
