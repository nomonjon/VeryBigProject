namespace GrpcServer.Models;

public class Product
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public double Quantity { get; set; }

    public decimal Price { get; set; }

}
