using GrpcServer.Dtos;

namespace GrpcServer.Interfaces;

public interface IProductRuleService
{
    Task<ProductRuleDto> CreateRuleAsync(CreateUpdateProductRuleDto ruleDto);
    Task<ProductRuleDto?> GetRuleByIdAsync(Guid id);
    Task<List<ProductRuleDto>> GetAllRulesAsync();
    Task<ProductRuleDto?> UpdateRuleAsync(Guid id, CreateUpdateProductRuleDto ruleDto);
    Task<bool> DeleteRuleAsync(Guid id);
    Task<List<ProductDto>?> GetMatchingProductsAsync(Guid ruleId);
    Task<List<ProductRuleMatchDto>?> EvaluateProductAsync(Guid productId);
    Task<int> ApplyActiveRulesAsync();
}
