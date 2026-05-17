var builder = DistributedApplication.CreateBuilder(args);

var redis = builder.AddRedis("Redis")
    .WithHostPort(6379)
    .WithDataVolume("microshop-redis-data");

var rabbit = builder.AddRabbitMQ("RabbitMQ", port: 5672)
    .WithManagementPlugin(port: 15672)
    .WithDataVolume("microshop-rabbitmq-data");

var catalog = builder.AddProject<Projects.CatalogService>("CatalogService");

var basket = builder.AddProject<Projects.BasketService>("BasketService")
    .WithReference(redis)
    .WaitFor(redis);

var identity = builder.AddProject<Projects.IdentityService>("IdentityService");

var ordering = builder.AddProject<Projects.OrderingService>("OrderingService")
    .WithReference(basket)
    .WaitFor(basket)
    .WithEnvironment("ServiceUrls__BasketHttp", "https+http://BasketService");

var discount = builder.AddProject<Projects.DiscountService>("DiscountService");

var gateway = builder.AddProject<Projects.ApiGateway>("ApiGateway")
    .WithReference(catalog)
    .WithReference(basket)
    .WithReference(ordering)
    .WithReference(discount);

builder.Build().Run();
