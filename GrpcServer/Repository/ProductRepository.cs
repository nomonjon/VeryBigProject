using GrpcServer.Data;
using GrpcServer.Interfaces;
using GrpcServer.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace GrpcServer.Repository;

public class ProductRepository : IProductRepository
{
    private readonly AppDbContext context;

    public ProductRepository(AppDbContext context)
    {
        this.context = context;
    }

    public async Task<Product> CreateAsync(Product product)
    {
        context.Products.Add(product);
        await context.SaveChangesAsync();
        return product;
    }

    public async Task<Product?> GetByIdAsync(Guid id)
    {
        var product = context.Products.Where(p => p.Id == id);

        product.Where(p => p.Name != null);

        return await product.SingleOrDefaultAsync();
    }

    public async Task<List<Product>> GetAllAsync()
    {
        return await context.Products.ToListAsync();
    }

    public IQueryable<Product> GetWhereAsync2(Expression<Func<Product, bool>> predicate)
    {
        return  context.Products.Where(predicate);
    }

    public async Task<List<Product>> GetWhereAsync(Expression<Func<Product, bool>> predicate, int take)
    {
        return await context.Products.Where(predicate).Take(take).ToListAsync();
    }

    public async Task<Product?> UpdateAsync(Product product)
    {
        if (product.Id == Guid.Empty)
            return null;

        var existing = await context.Products.FindAsync(product.Id);
        if (existing is null)
            return null;

        // Copy only user-editable fields. StatusColor is owned by the rule sweep,
        // so an edit must not reset it (FindAsync returns the sweep's own tracked
        // instance, so its StatusColor change still persists here).
        existing.Name = product.Name;
        existing.Quantity = product.Quantity;
        existing.Price = product.Price;

        await context.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        if (id == Guid.Empty)
            return false;

        var product = await GetByIdAsync(id);

        context.Products.Remove(product!);
        await context.SaveChangesAsync();
        return true;
    }
}