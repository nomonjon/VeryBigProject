using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;
using TaskTracker.Contracts;
using TaskTracker.MyOptions;

namespace TaskTracker.Services;

public class RabbitMqPublisher(IConnection connection) : IAsyncDisposable
{
    private const string QueueName = "task.status.changed";
    private const string LogsQueue = "logs.TaskTracker";

    public static async Task<RabbitMqPublisher> CreateAsync(IOptions<RabbitMqSettings> options)
    {
        var factory = new ConnectionFactory
        {
            HostName = options.Value.HostName,
            Port = options.Value.Port,
            UserName = options.Value.UserName,
            Password = options.Value.Password,
        };

        var connection = await factory.CreateConnectionAsync();
        var publisher = new RabbitMqPublisher(connection);
        await publisher.SetupAsync();
        return publisher;
    }

    private async Task SetupAsync()
    {
        await using var channel = await connection.CreateChannelAsync();

        await channel.QueueDeclareAsync(
            queue: QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        await channel.QueueDeclareAsync(
            queue: LogsQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);
    }

    // virtual so WorkTaskService tests can substitute a publisher that records events
    // instead of opening a channel to a broker. No behaviour change.
    public virtual async Task PublishAsync(TaskStatusChangedEvent @event)
    {
        await using var channel = await connection.CreateChannelAsync();

        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(@event));
        var props = new BasicProperties { Persistent = true };

        await channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: QueueName,
            mandatory: false,
            basicProperties: props,
            body: body);
    }

    public virtual async Task PublishLogAsync(string logJson)
    {
        await using var channel = await connection.CreateChannelAsync();

        var body = Encoding.UTF8.GetBytes(logJson);
        var props = new BasicProperties { Persistent = true };

        await channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: LogsQueue,
            mandatory: false,
            basicProperties: props,
            body: body);
    }

    public async ValueTask DisposeAsync()
    {
        await connection.CloseAsync();
    }
}
