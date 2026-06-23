using BasketService.API;
using BasketService.Application;
using BasketService.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApi();

var app = builder.Build();

app.UseCorrelationId();
app.UseApiExceptionHandling();
app.UseHttpsRedirection();

app.UseAuthorization();

app.MapDefaultEndpoints();
app.MapApiEndpoints();

app.Run();
