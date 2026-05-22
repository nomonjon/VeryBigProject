using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Formatting.Compact;
using TaskTracker.Services;

namespace TaskTracker.Services;

public class RabbitMqLogSink(RabbitMqPublisher publisher) : ILogEventSink
{
    private readonly ITextFormatter _formatter = new RenderedCompactJsonFormatter();

    public void Emit(LogEvent logEvent)
    {
        using var sw = new StringWriter();
        _formatter.Format(logEvent, sw);
        publisher.PublishLogAsync(sw.ToString()).GetAwaiter().GetResult();
    }
}