using Microsoft.Extensions.Logging;
using Moq;
using TaskTracker.Services;

namespace TaskTracker.Tests.Services;

/// <summary>
/// OrderService only writes log lines — it exists to exercise the Serilog → RabbitMQ →
/// LogPlatform pipeline. Its behaviour *is* the logging, so here (and only here) the
/// logger is mocked and asserted on.
///
/// <c>ILogger.LogInformation</c> is an extension method and cannot be set up directly;
/// the interface method underneath is <c>Log&lt;TState&gt;</c>, which is what the helper
/// below matches. This is why asserting on logs is usually a bad idea — do it when the
/// log is the contract, not to prove a method ran.
/// </summary>
public class OrderServiceTests
{
    private readonly Mock<ILogger<OrderService>> _logger = new();
    private readonly OrderService _sut;

    public OrderServiceTests() => _sut = new OrderService(_logger.Object);

    [Fact]
    public void CreateOrder_WritesOneLineAtEachSeverity()
    {
        _sut.CreateOrder("ORD-1");

        VerifyLogged(LogLevel.Information, Times.Once());
        VerifyLogged(LogLevel.Warning, Times.Once());
        VerifyLogged(LogLevel.Error, Times.Once());
    }

    [Fact]
    public void CreateOrder_IncludesTheOrderIdInEveryLine()
    {
        _sut.CreateOrder("ORD-42");

        _logger.Verify(l => l.Log(
            It.IsAny<LogLevel>(),
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains("ORD-42")),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Exactly(3));
    }

    private void VerifyLogged(LogLevel level, Times times)
        => _logger.Verify(l => l.Log(
            level,
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), times);
}
