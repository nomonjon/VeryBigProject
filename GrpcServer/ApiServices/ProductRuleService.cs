using GrpcServer.Dtos;
using GrpcServer.Interfaces;
using GrpcServer.Mapper;
using GrpcServer.Models;
using GrpcServer.Services.Caching;
using System.Linq.Dynamic.Core;
using static GrpcServer.Validator.RulesValidator;

namespace GrpcServer.ApiServices;

public class ProductRuleService(
    IProductRuleRepository ruleRepository,
    IProductRepository productRepository,
    ICacheService cache) : IProductRuleService
{
    // Rules are read on every 15s sweep and every evaluate, but only change via
    // the CRUD methods below — which evict — so the TTL is just a safety net.
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

    public async Task<ProductRuleDto> CreateRuleAsync(CreateUpdateProductRuleDto ruleDto)
    {
        ValidateExpression(ruleDto.Expression);
        ValidateColor(ruleDto.Color);

        var rule = ruleDto.ToProductRule();
        rule.CreatedAt = DateTime.UtcNow;

        await ruleRepository.CreateAsync(rule);
        await InvalidateRuleListsAsync();
        return rule.ToProductRuleDto();
    }

    public async Task<ProductRuleDto?> GetRuleByIdAsync(Guid id)
    {
        return await cache.GetOrSetAsync(
            CacheKeys.Rule(id),
            async () =>
            {
                var rule = await ruleRepository.GetByIdAsync(id);
                return rule?.ToProductRuleDto();
            },
            Ttl);
    }

    public async Task<List<ProductRuleDto>> GetAllRulesAsync()
    {
        var rules = await cache.GetOrSetAsync(
            CacheKeys.RuleList,
            async () =>
            {
                var all = await ruleRepository.GetAllAsync();
                return all.Select(r => r.ToProductRuleDto()).ToList();
            },
            Ttl);

        return rules ?? [];
    }

    public async Task<ProductRuleDto?> UpdateRuleAsync(Guid id, CreateUpdateProductRuleDto ruleDto)
    {
        ValidateExpression(ruleDto.Expression);
        ValidateColor(ruleDto.Color);

        var rule = ruleDto.ToProductRule(id);
        var result = await ruleRepository.UpdateAsync(rule);
        if (result is null)
            return null;

        await InvalidateRuleListsAsync(id);

        return result.ToProductRuleDto();
    }

    public async Task<bool> DeleteRuleAsync(Guid id)
    {
        var deleted = await ruleRepository.DeleteAsync(id);

        if (deleted)
            await InvalidateRuleListsAsync(id);

        return deleted;
    }

    public async Task<List<ProductDto>?> GetMatchingProductsAsync(Guid ruleId)
    {
        var rule = await ruleRepository.GetByIdAsync(ruleId);
        if (rule is null)
            return null;

        var predicate = ToPredicate(rule.Expression);
        var products = await productRepository.GetWhereAsync(predicate);
        return products.Select(p => p.ToProductDto()).ToList();
    }

    public async Task<List<ProductRuleMatchDto>?> EvaluateProductAsync(Guid productId)
    {
        var product = await productRepository.GetByIdAsync(productId);
        if (product is null)
            return null;

        var rules = await GetActiveRulesAsync();

        return rules.Select(rule =>
        {
            var predicate = ToPredicate(rule.Expression).Compile();

            return new ProductRuleMatchDto
            {
                RuleId = rule.Id,
                RuleName = rule.Name,
                Expression = rule.Expression,
                Color = rule.Color,
                IsMatch = predicate(product)
            };
        }).ToList();
    }

    // Paints every product with the color of the most severe active rule it
    // matches, or ProductColors.Default when nothing matches. Returns the number
    // of products whose color actually changed.
    public async Task<int> ApplyActiveRulesAsync()
    {
        var rules = await GetActiveRulesAsync();
        var compiled = rules
            .Select(r => (Matches: ToPredicate(r.Expression).Compile(), r.Color))
            .ToList();

        var checkCooldown = DateTime.Now.AddMinutes(-10);
        int take = 10;

        // var products = await productRepository.GetWhereAsync(p => p.LastCheckedTime <= checkCooldown , take);
        var baseQuery = productRepository.GetWhereAsync2(p => p.LastCheckedTime <= checkCooldown);
        var products = baseQuery.Take(take).ToList();
        var changed = 0;

        foreach (var product in products)
        {
            var color = compiled
                .Where(rule => rule.Matches(product))
                .Select(rule => rule.Color)
                .DefaultIfEmpty(ProductColors.Default)
                .MaxBy(ProductColors.Rank)!;

            if (product.StatusColor == color)
                continue;

            product.StatusColor = color;
            await productRepository.UpdateAsync(product);
            // The sweep writes StatusColor behind ProductService's back, so the
            // product cache must be evicted here or reads serve the old color.
            await cache.RemoveAsync(CacheKeys.Product(product.Id));
            changed++;
        }

        if (changed > 0)
            await cache.RemoveAsync(CacheKeys.ProductList);

        return changed;
    }

    private async Task<List<ProductRule>> GetActiveRulesAsync()
    {
        var rules = await cache.GetOrSetAsync(
            CacheKeys.ActiveRuleList,
            async () => await ruleRepository.GetActiveAsync(),
            Ttl);

        return rules ?? [];
    }

    private Task InvalidateRuleListsAsync(Guid? id = null) =>
        id is null
            ? cache.RemoveAsync(CacheKeys.RuleList, CacheKeys.ActiveRuleList)
            : cache.RemoveAsync(CacheKeys.RuleList, CacheKeys.ActiveRuleList, CacheKeys.Rule(id.Value));
}
