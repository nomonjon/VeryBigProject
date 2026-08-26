using GrpcServer.Mapper;
using GrpcServer.Models;
using GrpcServer.Tests.TestKit;

namespace GrpcServer.Tests.Mapper;

public class ProductRuleMapperTests
{
    [Fact]
    public void ToProductRule_CopiesEveryEditableField()
    {
        var dto = Make.RuleDto(name: "Out of stock", expression: "Quantity == 0", color: ProductColors.Red, isActive: false);

        var rule = dto.ToProductRule();

        Assert.Equal("Out of stock", rule.Name);
        Assert.Equal("Quantity == 0", rule.Expression);
        Assert.Equal(ProductColors.Red, rule.Color);
        Assert.False(rule.IsActive);
    }

    [Fact]
    public void ToProductRule_LeavesIdEmpty_WhenNoIdIsSupplied()
        => Assert.Equal(Guid.Empty, Make.RuleDto().ToProductRule().Id);

    [Fact]
    public void ToProductRule_StampsTheSuppliedId()
    {
        var id = Guid.NewGuid();

        Assert.Equal(id, Make.RuleDto().ToProductRule(id).Id);
    }

    [Fact]
    public void ToProductRule_LeavesCreatedAtUnset_BecauseTheServiceOwnsThatTimestamp()
        => Assert.Equal(default, Make.RuleDto().ToProductRule().CreatedAt);

    [Fact]
    public void ToProductRuleDto_CopiesEveryFieldIncludingCreatedAt()
    {
        var createdAt = new DateTime(2026, 1, 15, 10, 30, 0, DateTimeKind.Utc);
        var rule = Make.Rule(
            name: "Low stock",
            expression: "Quantity < 5",
            color: ProductColors.Orange,
            isActive: true,
            createdAt: createdAt);

        var dto = rule.ToProductRuleDto();

        Assert.Equal(rule.Id, dto.Id);
        Assert.Equal("Low stock", dto.Name);
        Assert.Equal("Quantity < 5", dto.Expression);
        Assert.Equal(ProductColors.Orange, dto.Color);
        Assert.True(dto.IsActive);
        Assert.Equal(createdAt, dto.CreatedAt);
    }

    [Fact]
    public void ToProductRule_ThenToProductRuleDto_RoundTripsTheEditableFields()
    {
        var id = Guid.NewGuid();
        var dto = Make.RuleDto(name: "Cheap", expression: "Price < 10", color: ProductColors.Green, isActive: false);

        var result = dto.ToProductRule(id).ToProductRuleDto();

        Assert.Equal(id, result.Id);
        Assert.Equal(dto.Name, result.Name);
        Assert.Equal(dto.Expression, result.Expression);
        Assert.Equal(dto.Color, result.Color);
        Assert.Equal(dto.IsActive, result.IsActive);
    }
}
