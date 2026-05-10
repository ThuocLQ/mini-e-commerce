using CatalogService.API.Endpoints;
using MediatR;
using CatalogService;
using CatalogService.Application.Abstractions;
using CatalogService.Commands;
using CatalogService.Data;
using CatalogService.DTOs;
using CatalogService.GrpcServices;
using CatalogService.Infrastructure.Persistence;
using CatalogService.Queries;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.AddServiceDefaults();//aspire service defaults: service discovery, resilience, health checks, and OpenTelemetry.

builder.Services.AddControllers();

// DB + Dapper
builder.Services.AddSingleton<IDbConnectionFactory, SqliteConnectionFactory>();
builder.Services.AddScoped<IProductRepository, DapperProductRepository>();

// CQRS use MediatR DI
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

// GRPC DI
builder.Services.AddGrpc();

var app = builder.Build();

app.MapDefaultEndpoints();//aspire service default endpoints

app.MapProductEndpoints();//API

using (var scope = app.Services.CreateScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
    await initializer.InitializeAsync();
}

//GRPC map service
app.MapGrpcService<CatalogGrpcService>();

// Configure the HTTP request pipeline.
app.UseHttpsRedirection();

app.UseAuthorization();

app.Run();
