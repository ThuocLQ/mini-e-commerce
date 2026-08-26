using Aspire.Hosting.ApplicationModel;

var builder = DistributedApplication.CreateBuilder(args);

var runMode = builder.Configuration["MicroShop:RunMode"] ?? "Full";
var runFull = string.Equals(runMode, "Full", StringComparison.OrdinalIgnoreCase);
var runOrderFlow = string.Equals(runMode, "OrderFlow", StringComparison.OrdinalIgnoreCase);

var postgres = builder.AddPostgres("Postgres")
    .WithDataVolume("microshop-postgres-data");

var catalogDb = postgres.AddDatabase("CatalogDb", "catalogdb");
var inventoryDb = postgres.AddDatabase("InventoryDb", "inventorydb");
var orderingDb = postgres.AddDatabase("OrderingDb", "orderingdb");
var discountDb = postgres.AddDatabase("DiscountDb", "discountdb");
var identityDb = postgres.AddDatabase("IdentityDb", "identitydb");
var paymentDb = postgres.AddDatabase("PaymentDb", "paymentdb");

var redis = builder.AddRedis("Redis")
    .WithHostPort(6379)
    .WithDataVolume("microshop-redis-data");

var rabbitUserName = builder.AddParameter("RabbitMqUserName", "microshop");
var rabbitPassword = builder.AddParameter("RabbitMqPassword", "microshop", secret: true);
var bootstrapAdminUserName = builder.AddParameter("BootstrapAdminUserName");
var bootstrapAdminPassword = builder.AddParameter("BootstrapAdminPassword", secret: true);

var rabbit = builder.AddRabbitMQ("RabbitMQ", rabbitUserName, rabbitPassword, port: 5672)
    .WithManagementPlugin(port: 15672)
    .WithDataVolume("microshop-aspire-rabbitmq-data");

var mongodb = builder.AddContainer("MongoDB", "mongo", "7")
    .WithEnvironment("MONGO_INITDB_ROOT_USERNAME", "microshop")
    .WithEnvironment("MONGO_INITDB_ROOT_PASSWORD", "microshop")
    .WithEndpoint(port: 27017, targetPort: 27017, name: "mongodb")
    .WithVolume("microshop-mongodb-data", "/data/db");

IResourceBuilder<ProjectResource>? catalog = null;
IResourceBuilder<ProjectResource>? inventory = null;
IResourceBuilder<ProjectResource>? basket = null;
IResourceBuilder<ProjectResource>? identity = null;
IResourceBuilder<ProjectResource>? ordering = null;
IResourceBuilder<ProjectResource>? orderQuery = null;
IResourceBuilder<ProjectResource>? discount = null;
IResourceBuilder<ProjectResource>? payment = null;

if (runFull || runOrderFlow)
{
    catalog = builder.AddProject<Projects.CatalogService>("CatalogService", launchProfileName: "https")
        .WithReference(catalogDb)
        .WithReference(rabbit)
        .WaitFor(catalogDb)
        .WaitFor(rabbit);

    inventory = builder.AddProject<Projects.InventoryService>("InventoryService", launchProfileName: "https")
        .WithReference(inventoryDb)
        .WithReference(rabbit)
        .WaitFor(inventoryDb)
        .WaitFor(rabbit);

    catalog.WithReference(inventory)
        .WaitFor(inventory)
        .WithEnvironment("ServiceUrls__InventoryHttp", "https+http://InventoryService");

    basket = builder.AddProject<Projects.BasketService>("BasketService", launchProfileName: "https")
        .WithReference(redis)
        .WaitFor(redis);

    identity = builder.AddProject<Projects.IdentityService>("IdentityService", launchProfileName: "https")
        .WithReference(identityDb)
        .WaitFor(identityDb)
        .WithEnvironment("BootstrapAdmin__Enabled", "true")
        .WithEnvironment("BootstrapAdmin__UserName", bootstrapAdminUserName)
        .WithEnvironment("BootstrapAdmin__Password", bootstrapAdminPassword);

    ordering = builder.AddProject<Projects.OrderingService>("OrderingService", launchProfileName: "https")
        .WithReference(orderingDb)
        .WithReference(rabbit)
        .WithReference(catalog)
        .WithReference(inventory)
        .WithReference(basket)
        .WaitFor(orderingDb)
        .WaitFor(rabbit)
        .WaitFor(catalog)
        .WaitFor(inventory)
        .WaitFor(basket)
        .WithEnvironment("ServiceUrls__BasketHttp", "https+http://BasketService")
        .WithEnvironment("ServiceUrls__CatalogHttp", "https+http://CatalogService")
        .WithEnvironment("ServiceUrls__InventoryHttp", "https+http://InventoryService")
        .WithEnvironment("KafkaOutbox__BootstrapServers", "localhost:9092")
        .WithEnvironment("KafkaOutbox__Topic", "microshop.order-events");

    builder.AddProject<Projects.NotificationWorker>("NotificationWorker")
        .WithReference(rabbit)
        .WithReference(redis)
        .WaitFor(rabbit)
        .WaitFor(redis);

    orderQuery = builder.AddProject<Projects.OrderQueryService>("OrderQueryService", launchProfileName: "https")
        .WithEnvironment("MongoDb__ConnectionString", "mongodb://microshop:microshop@localhost:27017/?authSource=admin")
        .WithEnvironment("MongoDb__DatabaseName", "MicroShop_OrderReadDb")
        .WithEnvironment("MongoDb__OrderSummariesCollectionName", "order_summaries")
        .WithEnvironment("MongoDb__InitializeMaxRetryCount", "10")
        .WithEnvironment("MongoDb__InitializeRetryDelaySeconds", "3")
        .WaitFor(mongodb);

    builder.AddProject<Projects.ProjectionWorker>("ProjectionWorker")
        .WithEnvironment("Kafka__BootstrapServers", "localhost:9092")
        .WithEnvironment("Kafka__Topic", "microshop.order-events")
        .WithEnvironment("Kafka__RetryTopic", "microshop.order-events.retry")
        .WithEnvironment("Kafka__DeadLetterTopic", "microshop.order-events.dlt")
        .WithEnvironment("Kafka__GroupId", "projection-worker")
        .WithEnvironment("Kafka__AutoOffsetReset", "Earliest")
        .WithEnvironment("MongoDb__ConnectionString", "mongodb://microshop:microshop@localhost:27017/?authSource=admin")
        .WithEnvironment("MongoDb__DatabaseName", "MicroShop_OrderReadDb")
        .WithEnvironment("MongoDb__OrderSummariesCollectionName", "order_summaries")
        .WithEnvironment("MongoDb__ProjectionFailuresCollectionName", "projection_failures")
        .WithEnvironment("MongoDb__ProcessedEventsCollectionName", "processed_projection_events")
        .WithEnvironment("MongoDb__InitializeMaxRetryCount", "10")
        .WithEnvironment("MongoDb__InitializeRetryDelaySeconds", "3")
        .WaitFor(mongodb);
}

if (runFull)
{
    discount = builder.AddProject<Projects.DiscountService>("DiscountService", launchProfileName: "https")
        .WithReference(discountDb)
        .WithReference(rabbit)
        .WaitFor(discountDb)
        .WaitFor(rabbit);

    ordering!.WithReference(discount)
        .WaitFor(discount);

    payment = builder.AddProject<Projects.PaymentService>("PaymentService", launchProfileName: "https")
        .WithReference(paymentDb)
        .WithReference(ordering!)
        .WithReference(rabbit)
        .WaitFor(paymentDb)
        .WaitFor(ordering!)
        .WaitFor(rabbit)
        .WithEnvironment("ServiceUrls__OrderingHttp", "https+http://OrderingService");
}

var gateway = builder.AddProject<Projects.ApiGateway>("ApiGateway", launchProfileName: "https");

if (catalog is not null)
{
    gateway.WithReference(catalog);
}

if (inventory is not null)
{
    gateway.WithReference(inventory);
}

if (basket is not null)
{
    gateway.WithReference(basket);
}

if (ordering is not null)
{
    gateway.WithReference(ordering);
}

if (orderQuery is not null)
{
    gateway.WithReference(orderQuery);
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
