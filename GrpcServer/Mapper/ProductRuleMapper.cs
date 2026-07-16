using GrpcServer.Dtos;
using GrpcServer.Models;

namespace GrpcServer.Mapper;

public static class ProductRuleMapper
{
    public static ProductRule ToProductRule(this CreateUpdateProductRuleDto ruleDto, Guid id = default)
    {
        return new ProductRule
        {
            Id = id,
            Name = ruleDto.Name,
            Expression = ruleDto.Expression,
            Color = ruleDto.Color,
            IsActive = ruleDto.IsActive
        };
    }

    public static ProductRuleDto ToProductRuleDto(this ProductRule rule)
    {
        return new ProductRuleDto
        {
            Id = rule.Id,
            Name = rule.Name,
            Expression = rule.Expression,
            Color = rule.Color,
            IsActive = rule.IsActive,
            CreatedAt = rule.CreatedAt
        };
    }
}
