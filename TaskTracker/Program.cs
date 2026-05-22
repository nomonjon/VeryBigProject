using GrpcServer;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.Text;
using System.Text.Json.Serialization;
using TaskTracker.Data;
using TaskTracker.GraphQL.Queries;
using TaskTracker.Interfaces;
using TaskTracker.Middlewares;
using TaskTracker.Models;
using TaskTracker.MyOptions;
using TaskTracker.Repository;
using TaskTracker.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOptions<DbConection>().BindConfiguration(DbConection.SectionName);
builder.Services.AddOptions<RabbitMqSettings>().BindConfiguration(RabbitMqSettings.SectionName);
builder.Services.AddOptions<JwtSettings>().BindConfiguration(JwtSettings.SectionName);

var jwtSettings = builder.Configuration
    .GetSection(JwtSettings.SectionName)
    .Get<JwtSettings>()!;

var rabbitMqSettings = builder.Configuration
    .GetSection(RabbitMqSettings.SectionName)
    .Get<RabbitMqSettings>()!;

// ── RabbitMQ publisher with retry ────────────────────────────────────────────
static RabbitMqPublisher CreatePublisherWithRetry(RabbitMqSettings settings)
{
    const int maxRetries = 10;
    const int delaySeconds = 5;

    for (int attempt = 1; attempt <= maxRetries; attempt++)
    {
        try
        {
            var pub = RabbitMqPublisher
                .CreateAsync(Options.Create(settings))
                .GetAwaiter()
                .GetResult();

            Console.WriteLine($"[RabbitMQ] Connected on attempt {attempt}");
            return pub;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[RabbitMQ] Attempt {attempt}/{maxRetries} failed: {ex.Message}");

            if (attempt == maxRetries)
                throw;

            Task.Delay(TimeSpan.FromSeconds(delaySeconds)).GetAwaiter().GetResult();
        }
    }

    throw new InvalidOperationException("Could not connect to RabbitMQ");
}

var publisher = CreatePublisherWithRetry(rabbitMqSettings);
builder.Services.AddSingleton(publisher);

// ── Serilog ──────────────────────────────────────────────────────────────────
builder.Host.UseSerilog((ctx, _, config) =>
{
    config
        .ReadFrom.Configuration(ctx.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithMachineName()
        .Enrich.WithThreadId()
        .WriteTo.Console(
            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
        .WriteTo.Sink(new RabbitMqLogSink(publisher));
});

// ── MVC / API ─────────────────────────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Enter the Token"
    });

    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id   = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// ── Database ──────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

// ── Repositories ──────────────────────────────────────────────────────────────
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IProjectRepository, ProjectRepository>();
builder.Services.AddScoped<IWorkTaskRepository, WorkTaskRepository>();

// ── Services ──────────────────────────────────────────────────────────────────
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IProjectService, ProjectService>();
builder.Services.AddScoped<IWorkTaskService, WorkTaskService>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ProductApiService>();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// ── Auth ──────────────────────────────────────────────────────────────────────
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole(Roles.Admin));
    options.AddPolicy("ManagerOrAdmin", policy => policy.RequireRole(Roles.Admin, Roles.Manager));
});

// ── GraphQL ───────────────────────────────────────────────────────────────────
builder.Services
    .AddGraphQLServer()
    .AddQueryType<Query>()
    .AddMutationType<Mutation>()
    .AddDataLoader<TasksByUserDataLoader>()
    .AddFiltering()
    .AddSorting()
    .AddProjections()
    .AddInMemorySubscriptions();

// ── gRPC ──────────────────────────────────────────────────────────────────────
builder.Services.AddGrpcClient<ProductService.ProductServiceClient>(o =>
    o.Address = new Uri("http://product:5000"));

// ── CORS ──────────────────────────────────────────────────────────────────────
builder.Services.AddCors(options =>
    options.AddPolicy("AllowAngular", policy =>
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()));

// ── Pipeline ──────────────────────────────────────────────────────────────────
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAngular");
app.UseExceptionHandler();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.UseWebSockets();
app.MapGraphQL();
app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.Run();