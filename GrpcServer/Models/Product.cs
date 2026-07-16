namespace GrpcServer.Models;

public class Product
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public double Quantity { get; set; }

    public decimal Price { get; set; }
    public DateTime? LastCheckedTime {get;set;}

    // Worst matching active rule's color, or ProductColors.Default when no rule
    // matches. Set by the rule sweep (ProductRuleWorker), not by users.
    public string StatusColor { get; set; } = ProductColors.Default;
}
