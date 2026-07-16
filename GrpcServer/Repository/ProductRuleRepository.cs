using GrpcServer.Data;
using GrpcServer.Interfaces;
using GrpcServer.Models;
using Microsoft.EntityFrameworkCore;

namespace GrpcServer.Repository;

public class ProductRuleRepository : IProductRuleRepository
{
    private readonly AppDbContext context;

    public ProductRuleRepository(AppDbContext context)
    {
        this.context = context;
    }

    public async Task<ProductRule> CreateAsync(ProductRule rule)
    {
        context.ProductRules.Add(rule);
        await context.SaveChangesAsync();
        return rule;
    }

    public async Task<ProductRule?> GetByIdAsync(Guid id)
    {
        return await context.ProductRules.FindAsync(id);
    }

    public async Task<List<ProductRule>> GetAllAsync()
    {
        return await context.ProductRules.ToListAsync();
    }

    public async Task<List<ProductRule>> GetActiveAsync()
    {
        return await context.ProductRules.Where(r => r.IsActive).ToListAsync();
    }

    public async Task<ProductRule?> UpdateAsync(ProductRule rule)
    {
        if (rule.Id == Guid.Empty)
            return null;

        var existing = await context.ProductRules.FindAsync(rule.Id);
        if (existing is null)
            return null;

        existing.Name = rule.Name;
        existing.Expression = rule.Expression;
        existing.Color = rule.Color;
        existing.IsActive = rule.IsActive;

        await context.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var rule = await context.ProductRules.FindAsync(id);
        if (rule is null)
            return false;

        context.ProductRules.Remove(rule);
        await context.SaveChangesAsync();
        return true;
    }
}
