using GrpcServer.Dtos;
using GrpcServer.Interfaces;
using GrpcServer.Mapper;
using GrpcServer.Models;
using System.Linq.Dynamic.Core;
using static GrpcServer.Validator.RulesValidator;

namespace GrpcServer.ApiServices;

public class ProductRuleService(IProductRuleRepository ruleRepository, IProductRepository productRepository) : IProductRuleService
{
    public async Task<ProductRuleDto> CreateRuleAsync(CreateUpdateProductRuleDto ruleDto)
    {
        ValidateExpression(ruleDto.Expression);
        ValidateColor(ruleDto.Color);

        var rule = ruleDto.ToProductRule();
        rule.CreatedAt = DateTime.UtcNow;

        await ruleRepository.CreateAsync(rule);
        return rule.ToProductRuleDto();
    }

    public async Task<ProductRuleDto?> GetRuleByIdAsync(Guid id)
    {
        var rule = await ruleRepository.GetByIdAsync(id);

        if (rule is null)
            return null;

        return rule.ToProductRuleDto();
    }

    public async Task<List<ProductRuleDto>> GetAllRulesAsync()
    {
        var rules = await ruleRepository.GetAllAsync();
        return rules.Select(r => r.ToProductRuleDto()).ToList();
    }

    public async Task<ProductRuleDto?> UpdateRuleAsync(Guid id, CreateUpdateProductRuleDto ruleDto)
    {
        ValidateExpression(ruleDto.Expression);
        ValidateColor(ruleDto.Color);

        var rule = ruleDto.ToProductRule(id);
        var result = await ruleRepository.UpdateAsync(rule);
        if (result is null)
            return null;

        return result.ToProductRuleDto();
    }

    public async Task<bool> DeleteRuleAsync(Guid id)
    {
        return await ruleRepository.DeleteAsync(id);
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

        var rules = await ruleRepository.GetActiveAsync();

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
        var rules = await ruleRepository.GetActiveAsync();
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
            changed++;
        }

        return changed;
    }

}
