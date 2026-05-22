using GrpcServer.Data;
using GrpcServer.Interfaces;
using GrpcServer.Models;
using Microsoft.EntityFrameworkCore;

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
        return await context.Products.FindAsync(id);
    }

    public async Task<List<Product>> GetAllAsync()
    {
        return await context.Products.ToListAsync();
    }

    public async Task<Product?> UpdateAsync(Product product)
    {
        if (product.Id == Guid.Empty)
            return null;

        context.Products.Update(product);
        await context.SaveChangesAsync();
        return product;
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