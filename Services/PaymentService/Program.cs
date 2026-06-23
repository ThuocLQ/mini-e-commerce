using PaymentService.API;
using PaymentService.Application;
using PaymentService.Infrastructure;
using PaymentService.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApi();

var app = builder.Build();

await app.InitializeDatabaseAsync();

app.UseCorrelationId();
app.UseApiExceptionHandling();
app.UseHttpsRedirection();

app.MapDefaultEndpoints();
app.MapApiEndpoints();

app.Run();
