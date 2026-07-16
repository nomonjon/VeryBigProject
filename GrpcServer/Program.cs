using GrpcServer.ApiServices;
using GrpcServer.Data;
using GrpcServer.Interfaces;
using GrpcServer.Models;
using GrpcServer.Repository;
using GrpcServer.Services;
using GrpcServer.Workers;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);

// ── RabbitMQ connection for logging ──────────────────────────────────────────
var rmqHost     = builder.Configuration["RabbitMQ:HostName"] ?? "localhost";
var rmqPort     = int.Parse(builder.Configuration["RabbitMQ:Port"] ?? "5672");
var rmqUser     = builder.Configuration["RabbitMQ:UserName"] ?? "guest";
var rmqPassword = builder.Configuration["RabbitMQ:Password"] ?? "guest";

var rmqConnection = RabbitMqConnectionFactory.CreateWithRetry(rmqHost, rmqPort, rmqUser, rmqPassword);
builder.Services.AddSingleton(rmqConnection);

// ── Serilog → Console + RabbitMQ ─────────────────────────────────────────────
builder.Host.UseSerilog((ctx, _, config) =>
{
    config
        .ReadFrom.Configuration(ctx.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithMachineName()
        .Enrich.WithProperty("Application", "GrpcServer")   // ← service tag
        .WriteTo.Console(
            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
        .WriteTo.Sink(new RabbitMqLogSink(rmqConnection));
});

// ── Services ──────────────────────────────────────────────────────────────────
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IProductRuleRepository, ProductRuleRepository>();
builder.Services.AddScoped<IProductRuleService, ProductRuleService>();
builder.Services.AddHostedService<ProductRuleWorker>();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHttpClient("PriceRandomizer", client =>
{
    client.BaseAddress = new Uri("http://randomprice:5059");
});

builder.Services.AddGrpc();
builder.Services.AddGrpcReflection();
builder.Services.AddControllers();

var app = builder.Build();

app.MapGrpcService<ProductGrpcService>();
if (app.Environment.IsDevelopment())
    app.MapGrpcReflectionService();

app.MapControllers();

app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client.");

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.Run();