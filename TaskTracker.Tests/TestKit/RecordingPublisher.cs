using TaskTracker.Contracts;
using TaskTracker.Services;

namespace TaskTracker.Tests.TestKit;

/// <summary>
/// Stand-in for <see cref="RabbitMqPublisher"/> that keeps published events in a list.
///
/// The real publisher opens an AMQP channel per publish. A unit test must never do
/// that, so <c>PublishAsync</c> is virtual and this subclass overrides it. The base
/// constructor still needs an <c>IConnection</c>; null is fine because no inherited
/// code path is ever reached.
///
/// Compare with <c>Mock&lt;RabbitMqPublisher&gt;</c>: equivalent, but this reads as a
/// collection of events, which is what the assertions are actually about.
/// </summary>
public sealed class RecordingPublisher() : RabbitMqPublisher(null!)
{
    public List<TaskStatusChangedEvent> Published { get; } = [];

    public override Task PublishAsync(TaskStatusChangedEvent @event)
    {
        Published.Add(@event);
        return Task.CompletedTask;
    }

    public override Task PublishLogAsync(string logJson) => Task.CompletedTask;
}
