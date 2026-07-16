using GrpcServer.Models;

namespace GrpcServer.Interfaces;

public interface IProductRuleRepository
{
    Task<ProductRule> CreateAsync(ProductRule rule);
    Task<ProductRule?> GetByIdAsync(Guid id);
    Task<List<ProductRule>> GetAllAsync();
    Task<List<ProductRule>> GetActiveAsync();
    Task<ProductRule?> UpdateAsync(ProductRule rule);
    Task<bool> DeleteAsync(Guid id);
}
