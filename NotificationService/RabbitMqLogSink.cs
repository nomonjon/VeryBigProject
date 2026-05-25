using RabbitMQ.Client;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Compact;
using System.Text;

namespace NotificationService;

/// <summary>
/// Publishes Serilog events to the shared "logs" fanout exchange in RabbitMQ.
/// </summary>
public sealed class RabbitMqLogSink : ILogEventSink, IDisposable
{
    private readonly IConnection _connection;
    private readonly RenderedCompactJsonFormatter _formatter = new();

    public RabbitMqLogSink(IConnection connection)
    {
        _connection = connection;
    }

    public void Emit(LogEvent logEvent)
    {
        try
        {
            using var sw = new StringWriter();
            _formatter.Format(logEvent, sw);
            var body = Encoding.UTF8.GetBytes(sw.ToString());

            using var channel = _connection.CreateChannelAsync().GetAwaiter().GetResult();

            channel.ExchangeDeclareAsync(
                exchange: "logs",
                type: "fanout",
                durable: true,
                autoDelete: false).GetAwaiter().GetResult();

            var props = new BasicProperties { Persistent = true };

            channel.BasicPublishAsync(
                exchange: "logs",
                routingKey: "",
                mandatory: false,
                basicProperties: props,
                body: body).GetAwaiter().GetResult();
        }
        catch
        {
            // Never throw from a log sink
        }
    }

    public void Dispose() => _connection.Dispose();
}