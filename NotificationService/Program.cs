using NotificationService;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<RabbitMqSettings>(
    builder.Configuration.GetSection(RabbitMqSettings.SectionName));

builder.Services.AddHostedService<NotifierWorker>();

var host = builder.Build();
host.Run();