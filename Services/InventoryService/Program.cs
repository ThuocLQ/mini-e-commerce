using InventoryService.Application;
using InventoryService.API;
using InventoryService.Infrastructure;
using InventoryService.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);
builder.Services.AddApi(builder.Configuration, builder.Environment);

var app = builder.Build();
await app.InitializeDatabaseAsync();

app.UseCorrelationId();
app.UseApiExceptionHandling();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapDefaultEndpoints();
app.MapApiEndpoints();

app.Run();
