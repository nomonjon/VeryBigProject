using System.Text;
using System.Text.Json;
using MongoDB.Driver;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace LoggerService;

public class LogConsumerWorker : BackgroundService
{
    private readonly ILogger<LogConsumerWorker> _logger;
    private readonly RabbitMqSettings _settings;
    private readonly string _logsDirectory;
    private readonly IMongoCollection<LogEntry> _collection;

    private IConnection? _connection;
    private IChannel? _channel;

    public LogConsumerWorker(
        ILogger<LogConsumerWorker> logger,
        RabbitMqSettings settings,
        IConfiguration configuration,
        IMongoCollection<LogEntry> collection)
    {
        _logger = logger;
        _settings = settings;
        _logsDirectory = configuration["LogsDirectory"] ?? "logs";
        _collection = collection;

        Directory.CreateDirectory(_logsDirectory);
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        var maxRetries = 5;
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {

                _logger.LogInformation("Connecting to RabbitMQ at {Host}", _settings.HostName);

                var factory = new ConnectionFactory
                {
                    HostName = _settings.HostName,
                    Port = _settings.Port,
                    UserName = _settings.UserName,
                    Password = _settings.Password,
                    AutomaticRecoveryEnabled = true,
                    NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
                };

                _connection = await factory.CreateConnectionAsync(cancellationToken);
                _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

                await _channel.ExchangeDeclareAsync(
                    exchange: "logs",
                    type: "fanout",
                    durable: true,
                    autoDelete: false,
                    cancellationToken: cancellationToken
                );

                await _channel.QueueDeclareAsync(
                    queue: "logs.queue",
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    cancellationToken: cancellationToken
                );

                await _channel.QueueBindAsync(
                    queue: "logs.queue",
                    exchange: "logs",
                    routingKey: "",
                    cancellationToken: cancellationToken
                );

                await _channel.BasicQosAsync(
                    prefetchSize: 0,
                    prefetchCount: 1,
                    global: false,
                    cancellationToken: cancellationToken
                );

                _logger.LogInformation("Connected, listening on logs.queue");

                await base.StartAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to RabbitMQ (attempt {Attempt}/{MaxRetries})", attempt, maxRetries);
                if (attempt == maxRetries)
                    throw;
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var consumer = new AsyncEventingBasicConsumer(_channel!);

        consumer.ReceivedAsync += async (_, args) =>
        {
            try
            {
                var json = Encoding.UTF8.GetString(args.Body.Span);
                var logEntry = JsonSerializer.Deserialize<JsonElement>(json);

                var line = FormatLogEntry(logEntry);
                var entry = ParseLogEntry(logEntry);

                // Write to both destinations simultaneously
                WriteToFile(line);
                await WriteToMongoAsync(entry);

                await _channel!.BasicAckAsync(
                    deliveryTag: args.DeliveryTag,
                    multiple: false,
                    cancellationToken: stoppingToken
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process log message");
                _logger.LogError("Log dump: {Dump}", Encoding.UTF8.GetString(args.Body.Span));

                await _channel!.BasicNackAsync(
                    deliveryTag: args.DeliveryTag,
                    multiple: false,
                    requeue: !args.Redelivered,
                    cancellationToken: stoppingToken
                );
            }
        };

        await _channel!.BasicConsumeAsync(
            queue: "logs.queue",
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken
        );

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private LogEntry ParseLogEntry(JsonElement log)
    {
        return new LogEntry
        {
            Timestamp = log.TryGetProperty("@t", out var ts)
                ? ts.GetString() ?? string.Empty : DateTime.UtcNow.ToString("o"),

            Level = log.TryGetProperty("@l", out var lvl)
                ? lvl.GetString() ?? "Information" : "Information",

            Message = log.TryGetProperty("@m", out var msg)
                ? msg.GetString() ?? string.Empty : string.Empty,

            SourceContext = log.TryGetProperty("SourceContext", out var ctx)
                ? ctx.GetString() ?? string.Empty : string.Empty,

            MachineName = log.TryGetProperty("MachineName", out var mn)
                ? mn.GetString() ?? string.Empty : string.Empty,

            Exception = log.TryGetProperty("@x", out var ex)
                ? ex.GetString() : null,

            CreatedAt = DateTime.UtcNow
        };
    }

    private async Task WriteToMongoAsync(LogEntry entry)
    {
        await _collection.InsertOneAsync(entry);
        _logger.LogDebug("Log entry written to MongoDB: {Level} {SourceContext}",
            entry.Level, entry.SourceContext);
    }

    private string FormatLogEntry(JsonElement log)
    {
        var timestamp = log.TryGetProperty("@t", out var ts)
            ? ts.GetString() : DateTime.UtcNow.ToString("o");

        var level = log.TryGetProperty("@l", out var lvl)
            ? lvl.GetString() : "Information"; // compact formatter omits @l when Information

        var message = log.TryGetProperty("@m", out var msg)
            ? msg.GetString() : string.Empty;

        var source = log.TryGetProperty("SourceContext", out var ctx)
            ? ctx.GetString() : string.Empty;

        var machine = log.TryGetProperty("MachineName", out var mn)
            ? mn.GetString() : string.Empty;

        var exception = log.TryGetProperty("@x", out var ex)
            ? ex.GetString() : null;

        var line = $"{timestamp} [{level}] ({machine}) {source} - {message}";

        if (!string.IsNullOrEmpty(exception))
            line += Environment.NewLine + exception;

        return line;
    }

    private void WriteToFile(string line)
    {
        var fileName = $"log-{DateTime.UtcNow:yyyy-MM-dd}.txt";
        var filePath = Path.Combine(_logsDirectory, fileName);

        using var stream = new FileStream(
            filePath,
            FileMode.Append,
            FileAccess.Write,
            FileShare.Read
        );
        using var writer = new StreamWriter(stream, Encoding.UTF8);

        writer.WriteLine(line);
        writer.Flush();
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("LogConsumerWorker stopping");

        if (_channel is not null)
            await _channel.CloseAsync(cancellationToken);

        if (_connection is not null)
            await _connection.CloseAsync(cancellationToken);

        await base.StopAsync(cancellationToken);
    }
}