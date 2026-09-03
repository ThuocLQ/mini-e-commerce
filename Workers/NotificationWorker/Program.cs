using NotificationWorker.Application;
using NotificationWorker.Infrastructure;
using NotificationWorker.Infrastructure.Persistence;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

var host = builder.Build();
await host.InitializeDatabaseAsync();
await host.RunAsync();
