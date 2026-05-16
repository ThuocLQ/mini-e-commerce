using CatalogService.API;
using CatalogService.Application;
using CatalogService.Infrastructure;
using CatalogService.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddApplication();
builder.Services.AddInfrastructure();
builder.Services.AddApi(builder.Configuration);

var app = builder.Build();

await app.InitializeDatabaseAsync();

app.UseApiExceptionHandling();
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapDefaultEndpoints();
app.MapApiEndpoints();

app.Run();
