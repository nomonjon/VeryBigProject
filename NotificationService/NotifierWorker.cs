using Microsoft.Extensions.Options;
using NotificationService.Contracts;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using System.Text;
using System.Text.Json;

namespace NotificationService;

public class NotifierWorker(
    IOptions<RabbitMqSettings> options,
    ILogger<NotifierWorker> logger) : BackgroundService
{
    private IConnection? _connection;
    private IChannel? _channel;
    private const string QueueName = "task.status.changed";

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        const int maxRetries = 10;
        const int delaySeconds = 5;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                var factory = new ConnectionFactory
                {
                    HostName = options.Value.HostName,
                    Port = options.Value.Port,
                    UserName = options.Value.UserName,
                    Password = options.Value.Password,
                };

                _connection = await factory.CreateConnectionAsync(cancellationToken);
                _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

                await _channel.QueueDeclareAsync(
                    queue: QueueName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null,
                    cancellationToken: cancellationToken);

                await _channel.BasicQosAsync(
                    prefetchSize: 0,
                    prefetchCount: 1,
                    global: false,
                    cancellationToken: cancellationToken);

                logger.LogInformation("Notifier connected to RabbitMQ on attempt {Attempt}", attempt);
                break;
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                logger.LogWarning(
                    "RabbitMQ not ready, attempt {Attempt}/{Max}. Retrying in {Delay}s... ({Message})",
                    attempt, maxRetries, delaySeconds, ex.Message);

                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
            }
        }

        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_channel is null)
        {
            logger.LogError("Channel is null, cannot start consuming");
            return;
        }

        var channel = _channel; // capture locally — safe to use in lambda
        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += async (_, ea) =>
        {
            try
            {
                var json = Encoding.UTF8.GetString(ea.Body.ToArray());
                var @event = JsonSerializer.Deserialize<TaskStatusChangedEvent>(json);

                if (@event is not null)
                {
                    foreach (var email in @event.ProjectUserEmails)
                    {
                        logger.LogInformation(
                            "[NOTIFICATION to {Email}] In Project {ProjectName}, Task '{TaskName}' status has been changed.",
                            email,
                            @event.ProjectName,
                            @event.Name);
                    }
                }

                await channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to process message");
                await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true);
            }
        };

        await channel.BasicConsumeAsync(
            queue: QueueName,
            autoAck: false,
            consumer: consumer);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        if (_channel is not null) await _channel.CloseAsync();
        if (_connection is not null) await _connection.CloseAsync();
    }
}