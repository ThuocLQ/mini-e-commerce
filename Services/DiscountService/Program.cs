using DiscountService.API;
using DiscountService.Application;
using DiscountService.Infrastructure;
using DiscountService.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApi();

var app = builder.Build();

await app.InitializeDatabaseAsync();

app.UseApiExceptionHandling();
app.UseHttpsRedirection();

app.MapDefaultEndpoints();
app.MapApiEndpoints();

app.Run();
