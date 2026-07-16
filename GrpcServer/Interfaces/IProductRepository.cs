using GrpcServer.Models;
using System.Linq.Expressions;

namespace GrpcServer.Interfaces;

public interface IProductRepository
{
    Task<Product> CreateAsync(Product product);
    Task<Product?> GetByIdAsync(Guid id);
    Task<List<Product>> GetAllAsync();
    Task<List<Product>> GetWhereAsync(Expression<Func<Product, bool>> predicate, int take = 10);
    IQueryable<Product> GetWhereAsync2(Expression<Func<Product, bool>> predicate);
    Task<Product?> UpdateAsync(Product product);
    Task<bool> DeleteAsync(Guid id);
}
