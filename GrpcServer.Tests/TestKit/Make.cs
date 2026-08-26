using GrpcServer.Dtos;
using GrpcServer.Models;

namespace GrpcServer.Tests.TestKit;

/// <summary>
/// Object mother for the GrpcServer domain.
///
/// AutoFixture is great when the values do not matter ("give me *a* product").
/// It is bad when they do: a test that says <c>Quantity &lt; 5</c> must show that
/// number in the test body. These factories give every field a sane default and
/// let a test override only the one or two fields it actually reasons about.
/// </summary>
public static class Make
{
    public static Product Product(
        Guid? id = null,
        string name = "Laptop",
        double quantity = 10,
        decimal price = 150m,
        string statusColor = ProductColors.Green,
        DateTime? lastCheckedTime = null) => new()
        {
            Id = id ?? Guid.NewGuid(),
            Name = name,
            Quantity = quantity,
            Price = price,
            StatusColor = statusColor,
            LastCheckedTime = lastCheckedTime
        };

    public static ProductRule Rule(
        Guid? id = null,
        string name = "Low stock",
        string expression = "Quantity < 5",
        string color = ProductColors.Orange,
        bool isActive = true,
        DateTime createdAt = default) => new()
        {
            Id = id ?? Guid.NewGuid(),
            Name = name,
            Expression = expression,
            Color = color,
            IsActive = isActive,
            CreatedAt = createdAt
        };

    public static CreateUpdateProductDto ProductDto(
        string name = "Laptop",
        double quantity = 10,
        decimal price = 150m) => new()
        {
            Name = name,
            Quantity = quantity,
            Price = price
        };

    public static CreateUpdateProductRuleDto RuleDto(
        string name = "Low stock",
        string expression = "Quantity < 5",
        string color = ProductColors.Orange,
        bool isActive = true) => new()
        {
            Name = name,
            Expression = expression,
            Color = color,
            IsActive = isActive
        };
}
