using NotificationService;
using RabbitMQ.Client;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<RabbitMqSettings>(
    builder.Configuration.GetSection(RabbitMqSettings.SectionName));

// ── RabbitMQ connection for logging ──────────────────────────────────────────
var rmqHost     = builder.Configuration["RabbitMQ:HostName"] ?? "localhost";
var rmqPort     = int.Parse(builder.Configuration["RabbitMQ:Port"] ?? "5672");
var rmqUser     = builder.Configuration["RabbitMQ:UserName"] ?? "guest";
var rmqPassword = builder.Configuration["RabbitMQ:Password"] ?? "guest";

IConnection rmqConnection = null!;
const int maxRetries = 10;
for (int attempt = 1; attempt <= maxRetries; attempt++)
{
    try
    {
        var factory = new ConnectionFactory
        {
            HostName = rmqHost,
            Port = rmqPort,
            UserName = rmqUser,
            Password = rmqPassword,
            AutomaticRecoveryEnabled = true
        };
        rmqConnection = await factory.CreateConnectionAsync();
        Console.WriteLine($"[RabbitMQ] NotificationService connected on attempt {attempt}");
        break;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[RabbitMQ] Attempt {attempt}/{maxRetries} failed: {ex.Message}");
        if (attempt == maxRetries) throw;
        await Task.Delay(TimeSpan.FromSeconds(5));
    }
}

builder.Services.AddSingleton(rmqConnection);

// ── Serilog → Console + RabbitMQ ─────────────────────────────────────────────
builder.Services.AddSerilog(config =>
    config
        .Enrich.FromLogContext()
        .Enrich.WithMachineName()
        .Enrich.WithProperty("Application", "NotificationService")  // ← service tag
        .WriteTo.Console(
            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
        .WriteTo.Sink(new RabbitMqLogSink(rmqConnection))
);

builder.Services.AddHostedService<NotifierWorker>();

var host = builder.Build();
host.Run();