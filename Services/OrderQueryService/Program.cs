using OrderQueryService.API;
using OrderQueryService.Application;
using OrderQueryService.Infrastructure;
using OrderQueryService.Infrastructure.ReadModels.MongoDb;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApi(builder.Configuration, builder.Environment);

var app = builder.Build();

await app.InitializeMongoReadModelsAsync();

app.UseCorrelationId();
app.UseApiExceptionHandling();
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapDefaultEndpoints();
app.MapApiEndpoints();

app.Run();
