using OrderingService.API;
using OrderingService.Application;
using OrderingService.Infrastructure;
using OrderingService.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);
builder.Services.AddApi(builder.Configuration, builder.Environment);

var app = builder.Build();

await app.InitializeDatabaseAsync();

app.UseCorrelationId();
app.UseApiExceptionHandling();
if (!app.Environment.IsEnvironment("Docker") || app.Configuration.GetValue<bool>("ServiceDefaults:UseHttpsRedirection"))
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapDefaultEndpoints();

app.MapApiEndpoints();

app.Run();
