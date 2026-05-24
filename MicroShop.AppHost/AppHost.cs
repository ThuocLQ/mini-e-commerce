using Aspire.Hosting.ApplicationModel;

var builder = DistributedApplication.CreateBuilder(args);

var runMode = builder.Configuration["MicroShop:RunMode"] ?? "Full";
var runFull = string.Equals(runMode, "Full", StringComparison.OrdinalIgnoreCase);
var runOrderFlow = string.Equals(runMode, "OrderFlow", StringComparison.OrdinalIgnoreCase);

var postgres = builder.AddPostgres("Postgres")
    .WithDataVolume("microshop-postgres-data");

var catalogDb = postgres.AddDatabase("CatalogDb", "catalogdb");
var orderingDb = postgres.AddDatabase("OrderingDb", "orderingdb");
var discountDb = postgres.AddDatabase("DiscountDb", "discountdb");
var identityDb = postgres.AddDatabase("IdentityDb", "identitydb");
var paymentDb = postgres.AddDatabase("PaymentDb", "paymentdb");

var redis = builder.AddRedis("Redis")
    .WithHostPort(6379)
    .WithDataVolume("microshop-redis-data");

var rabbitUserName = builder.AddParameter("RabbitMqUserName", "microshop");
var rabbitPassword = builder.AddParameter("RabbitMqPassword", "microshop", secret: true);

var rabbit = builder.AddRabbitMQ("RabbitMQ", rabbitUserName, rabbitPassword, port: 5672)
    .WithManagementPlugin(port: 15672)
    .WithDataVolume("microshop-aspire-rabbitmq-data");

IResourceBuilder<ProjectResource>? catalog = null;
IResourceBuilder<ProjectResource>? basket = null;
IResourceBuilder<ProjectResource>? identity = null;
IResourceBuilder<ProjectResource>? ordering = null;
IResourceBuilder<ProjectResource>? discount = null;
IResourceBuilder<ProjectResource>? payment = null;

if (runFull || runOrderFlow)
{
    catalog = builder.AddProject<Projects.CatalogService>("CatalogService", launchProfileName: "https")
        .WithReference(catalogDb)
        .WaitFor(catalogDb);

    basket = builder.AddProject<Projects.BasketService>("BasketService", launchProfileName: "https")
        .WithReference(redis)
        .WaitFor(redis);

    identity = builder.AddProject<Projects.IdentityService>("IdentityService", launchProfileName: "https")
        .WithReference(identityDb)
        .WaitFor(identityDb);

    ordering = builder.AddProject<Projects.OrderingService>("OrderingService", launchProfileName: "https")
        .WithReference(orderingDb)
        .WithReference(rabbit)
        .WithReference(basket)
        .WaitFor(orderingDb)
        .WaitFor(rabbit)
        .WaitFor(basket)
        .WithEnvironment("ServiceUrls__BasketHttp", "https+http://BasketService");

    builder.AddProject<Projects.NotificationWorker>("NotificationWorker")
        .WithReference(rabbit)
        .WaitFor(rabbit);
}

if (runFull)
{
    discount = builder.AddProject<Projects.DiscountService>("DiscountService", launchProfileName: "https")
        .WithReference(discountDb)
        .WaitFor(discountDb);

    payment = builder.AddProject<Projects.PaymentService>("PaymentService", launchProfileName: "https")
        .WithReference(paymentDb)
        .WaitFor(paymentDb);
}

var gateway = builder.AddProject<Projects.ApiGateway>("ApiGateway", launchProfileName: "https");

if (catalog is not null)
{
    gateway.WithReference(catalog);
}

if (basket is not null)
{
    gateway.WithReference(basket);
}

if (ordering is not null)
{
    gateway.WithReference(ordering);
}

if (discount is not null)
{
    gateway.WithReference(discount);
}

if (identity is not null)
{
    gateway.WithReference(identity);
}

if (payment is not null)
{
    gateway.WithReference(payment);
}

builder.Build().Run();
