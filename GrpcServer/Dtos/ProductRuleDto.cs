using GrpcServer.Models;

namespace GrpcServer.Dtos;

public record ProductRuleDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public string Expression { get; set; } = null!;

    public string Color { get; set; } = null!;

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }
}

public record CreateUpdateProductRuleDto
{
    public string Name { get; set; } = null!;

    public string Expression { get; set; } = null!;

    public string Color { get; set; } = ProductColors.Orange;

    public bool IsActive { get; set; } = true;
}

public record ProductRuleMatchDto
{
    public Guid RuleId { get; set; }

    public string RuleName { get; set; } = null!;

    public string Expression { get; set; } = null!;

    public string Color { get; set; } = null!;

    public bool IsMatch { get; set; }
}
