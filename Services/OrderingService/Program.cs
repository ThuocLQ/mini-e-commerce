using OrderingService.API;
using OrderingService.Application;
using OrderingService.Infrastructure;
using OrderingService.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddApplication();
builder.Services.AddInfrastructure();
builder.Services.AddApi();

var app = builder.Build();

await app.InitializeDatabaseAsync();

app.UseApiExceptionHandling();
app.UseHttpsRedirection();

app.MapDefaultEndpoints();

app.MapApiEndpoints();

app.Run();
