namespace TaskTracker.Services;

public class OrderService
{
    private readonly ILogger<OrderService> _logger;

    public OrderService(ILogger<OrderService> logger)
        => _logger = logger;

    public void CreateOrder(string orderId)
    {
        _logger.LogInformation("Order {OrderId} created", orderId);
        _logger.LogWarning("Stock low for order {OrderId}", orderId);
        _logger.LogError("Failed to process {OrderId}", orderId);
    }
}