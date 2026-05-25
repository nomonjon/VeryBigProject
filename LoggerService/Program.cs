using LoggerService;
using MongoDB.Driver;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

var rabbitMqSettings = builder.Configuration
    .GetSection(RabbitMqSettings.SectionName)
    .Get<RabbitMqSettings>()!;

var mongoDbSettings = builder.Configuration
    .GetSection(MongoDbSettings.SectionName)
    .Get<MongoDbSettings>()!;

builder.Services.AddSingleton(rabbitMqSettings);
builder.Services.AddSingleton(mongoDbSettings);

// Register MongoClient and expose IMongoDatabase (used by LogConsumerWorker)
builder.Services.AddSingleton<IMongoClient>(_ =>
    new MongoClient(mongoDbSettings.ConnectionString));

builder.Services.AddSingleton<IMongoDatabase>(sp =>
{
    var client = sp.GetRequiredService<IMongoClient>();
    return client.GetDatabase(mongoDbSettings.DatabaseName);
});

builder.Services.AddSerilog(config =>
    config
        .Enrich.FromLogContext()
        .Enrich.WithMachineName()
        .WriteTo.Console(
            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}"
        )
);

builder.Services.AddHostedService<LogConsumerWorker>();

var host = builder.Build();
host.Run();