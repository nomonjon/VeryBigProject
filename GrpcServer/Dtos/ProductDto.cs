namespace GrpcServer.Dtos;

public record ProductDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public double Quantity { get; set; }

    public decimal Price { get; set; }
}

public record CreateUpdateProductDto
{
    public string Name { get; set; } = null!;
    public double Quantity { get; set; }
    public decimal Price { get; set; }
}