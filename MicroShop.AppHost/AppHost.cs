var builder = DistributedApplication.CreateBuilder(args);

var redis = builder.AddRedis("redis");

var catalog = builder.AddProject<Projects.CatalogService>("CatalogService");

var basket = builder.AddProject<Projects.BasketService>("BasketService")
    .WithReference(redis);

var gateway = builder.AddProject<Projects.ApiGateway>("ApiGateway")
    .WithReference(catalog)
    .WithReference(basket);

builder.Build().Run();
