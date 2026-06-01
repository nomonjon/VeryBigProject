using LogPlatform.Services;
using LogPlatform.Settings;
using LogPlatform.Workers;
using MongoDB.Driver;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ── Settings ────────────────────────────────────────────────────────────────
var mongoSettings = builder.Configuration
    .GetSection(MongoDbSettings.SectionName)
    .Get<MongoDbSettings>()!;

var rabbitSettings = builder.Configuration
    .GetSection(RabbitMqSettings.SectionName)
    .Get<RabbitMqSettings>()!;

builder.Services.AddSingleton(mongoSettings);
builder.Services.AddSingleton(rabbitSettings);

// ── Serilog ──────────────────────────────────────────────────────────────────
builder.Services.AddSerilog(config =>
    config
        .Enrich.FromLogContext()
        .Enrich.WithMachineName()
        .WriteTo.Console(
            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}"
        )
);

// ── MongoDB ──────────────────────────────────────────────────────────────────
builder.Services.AddSingleton<IMongoClient>(_ =>
    new MongoClient(mongoSettings.ConnectionString));

builder.Services.AddSingleton<IMongoDatabase>(sp =>
{
    var client = sp.GetRequiredService<IMongoClient>();
    return client.GetDatabase(mongoSettings.DatabaseName);
});

// ── Application Services ─────────────────────────────────────────────────────
builder.Services.AddScoped<LogService>();

// ── Background Worker (RabbitMQ consumer) ────────────────────────────────────
builder.Services.AddHostedService<LogConsumerWorker>();

// ── Web API ───────────────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "LogPlatform API", Version = "v1" });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular",
        policy =>
        {
            policy
                .WithOrigins("http://localhost:4200")
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowAngular");
app.MapControllers();

app.Run();
