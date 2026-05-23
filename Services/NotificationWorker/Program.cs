using NotificationWorker.Application;
using NotificationWorker.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

var host = builder.Build();
host.Run();
