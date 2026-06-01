using RabbitMQ.Client;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Formatting.Compact;
using System.Text;

namespace GrpcServer.Services;

/// <summary>
/// Serilog sink that publishes every log event to the shared "logs" fanout exchange.
/// </summary>
public sealed class RabbitMqLogSink : ILogEventSink, IDisposable
{
    private readonly IConnection _connection;
    private readonly ITextFormatter _formatter = new RenderedCompactJsonFormatter();

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

            // CreateChannelAsync is not awaitable here (sync Emit), so we use sync factory
            using var channel = _connection.CreateChannelAsync().GetAwaiter().GetResult();

            channel.QueueDeclareAsync(
                queue: "logs.GrpcServer",
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null).GetAwaiter().GetResult();

            var props = new BasicProperties { Persistent = true };

            channel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: "logs.GrpcServer",
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