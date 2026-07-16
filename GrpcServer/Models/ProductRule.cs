namespace GrpcServer.Models;

public class ProductRule
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public string Expression { get; set; } = null!;

    // Color painted onto every product this rule matches (ProductColors).
    public string Color { get; set; } = ProductColors.Orange;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }
}
