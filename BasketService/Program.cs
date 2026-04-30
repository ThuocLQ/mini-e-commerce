using System.Diagnostics;
using BasketService.Clients;
using BasketService.DTOs;
using BasketService.Models;
using StackExchange.Redis;
using BasketService.Repositories;
using CatalogService.Grpc;
using Grpc.Core;
using Refit;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
//Redis
var redisConnectionString = builder.Configuration.GetConnectionString("Redis")
                            ?? throw new InvalidOperationException("Connection string 'Redis' not found.");
builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect(redisConnectionString));

//Refit
var catalogServiceUrl = builder.Configuration["ServiceUrls:CatalogService"];
if (!Uri.TryCreate(catalogServiceUrl, UriKind.Absolute, out var catalogServiceUri))
{
    throw new InvalidOperationException(
        "Configuration 'ServiceUrls:CatalogService' must be an absolute URL, for example 'https://localhost:7079'.");
}

builder.Services.AddRefitClient<ICatalogApi>()
    .ConfigureHttpClient(c =>
    {
        c.BaseAddress = catalogServiceUri;
        c.Timeout = TimeSpan.FromSeconds(5);
    });

//gRPC Client
var catalogGrpcUrl = builder.Configuration["ServiceUrls:CatalogGrpcService"]
    ??  throw new InvalidOperationException("Configuration 'ServiceUrls:CatalogGrpcService' not found.");
builder.Services.AddGrpcClient<CatalogGrpc.CatalogGrpcClient>(options =>
{
    options.Address = new Uri(catalogGrpcUrl);
});

// Add repositories
builder.Services.AddScoped<IBasketRepository, RedisBasketRepository>();

// Add authorization
builder.Services.AddAuthorization();

var app = builder.Build();

//Endpoint
//Get baskets
app.MapGet("/basket/{userId}", async (string userId, IBasketRepository repository) =>
{
    var basket = await repository.GetBasketAsync(userId);
    return Results.Ok(basket);
});

// Add item to basket use Refit
app.MapPost("/basket/{userId}/items", async (
    string userId,
    AddBasketItemRequest request,
    IBasketRepository repository,
    ICatalogApi catalogApi) =>
{
    // Validate
    if (string.IsNullOrWhiteSpace(request.ProductId))
        return Results.BadRequest("ProductId is required.");
    
    if (request.Quantity <= 0)
        return Results.BadRequest("Quantity must be greater than 0.");
    
    CatalogProductResponse? product;
    try
    {
        // Call CatalogService to validate product and get details
        product = await catalogApi.GetProductByIdAsync(request.ProductId);
    }
    catch (ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
    {
        return Results.NotFound("Product not found.");
    }

    if (product is null)
        return Results.NotFound("Product not found.");

    if (product.Price < 0)
        return Results.BadRequest("Price must be greater than or equal to 0.");

    var basket = await repository.GetBasketAsync(userId);
    
    var existingItem = basket.Items.FirstOrDefault(x => x.ProductId == request.ProductId);
    if (existingItem is null)
    {
        basket.Items.Add(new BasketItem
        {
            ProductId = request.ProductId,
            ProductName = product.Name,
            Quantity = request.Quantity,
            Price = product.Price
        });
    }
    else
    {
        existingItem.Quantity += request.Quantity;
        existingItem.ProductName = product.Name;
        existingItem.Price = product.Price;
    }
    
    await repository.UpdateBasketAsync(basket);
    
    return Results.Ok(basket);
});

// Add item to basket use gRPC
app.MapPost("/basket/{userId}/items-grpc", async (
    string userId,
    AddBasketItemRequest request,
    IBasketRepository repository,
    CatalogGrpc.CatalogGrpcClient catalogClient) =>
{
    // Validate
    if (string.IsNullOrWhiteSpace(request.ProductId))
        return Results.BadRequest("ProductId is required.");
    
    if (request.Quantity <= 0)
        return Results.BadRequest("Quantity must be greater than 0.");
    
    // Call Catalog Service use gRPC to validate product and get details
    var product = await catalogClient.GetProductByIdAsync(new GetProductByIdRequest
    {
        Id = request.ProductId
    });
    
    if (product.Found == false)
        return Results.NotFound("Product not found.");
    
    if (product.Price < 0)
        return Results.BadRequest("Price must be greater than or equal to 0.");
    
    var basket = await repository.GetBasketAsync(userId);
    
    var existingItem = basket.Items.FirstOrDefault(x => x.ProductId == request.ProductId);

    if (existingItem is null)
    {
        basket.Items.Add(new BasketItem
        {
            ProductId = request.ProductId,
            ProductName = product.Name,
            Quantity = request.Quantity,
            Price = (decimal)product.Price
        });
    }
    else
    {
        existingItem.Quantity += request.Quantity;
        existingItem.ProductName = product.Name;
        existingItem.Price = (decimal)product.Price;
    }
    
    await repository.UpdateBasketAsync(basket);
    
    return Results.Ok(basket);
});

// Update item quantity
app.MapPut("/basket/{userId}/items/{productId}", async (
    string userId,
    string productId,
    UpdateBasketItemQuantityRequest request,
    IBasketRepository repository) =>
{
    if (string.IsNullOrWhiteSpace(productId))
        return Results.BadRequest("ProductId is required.");

    if (request.Quantity < 0)
        return Results.BadRequest("Quantity cannot be negative.");

    try
    {
        var basket = await repository.UpdateItemQuantityAsync(userId, productId, request.Quantity);
        if (basket is null)
            return Results.NotFound();
        
        return Results.Ok(basket);
    }
    catch (KeyNotFoundException)
    {
        return Results.NotFound();
    }
});

// Remove item
app.MapDelete("/basket/{userId}/items/{productId}", async (
    string userId,
    string productId,
    IBasketRepository repository) =>
{
    var basket = await repository.GetBasketAsync(userId);

    var item = basket.Items.FirstOrDefault(x => x.ProductId == productId);

    if (item is null)
        return Results.NotFound();

    basket.Items.Remove(item);

    await repository.UpdateBasketAsync(basket);

    return Results.NoContent();
});

//Clear all items in basket
app.MapPut("/basket/{userId}/clear", async (string userId, IBasketRepository repository) =>
{
    var basket = await repository.ClearBasketAsync(userId);
    return Results.Ok(basket);
});

//Delete basket
app.MapDelete("/basket/{userId}", async (string userId, IBasketRepository repository) =>
{
    var deleted = await repository.DeleteBasketAsync(userId);
    
    if (!deleted)
        return Results.NotFound();
    
    return Results.NoContent();
});

// Validate product endpoint = Refit
app.MapGet("/basket/products/{productId}/validate", async (
    string productId,
    ICatalogApi catalogApi) =>
{
    if (string.IsNullOrWhiteSpace(productId))
    {
        return Results.BadRequest(new CatalogProductValidateErrors
        {
            Message = "ProductId is required."
        });
    }

    try
    {
        var product = await catalogApi.GetProductByIdAsync(productId);
        if (product is null)
        {
            return Results.Ok(new CatalogProductValidateErrors
            {
                Message = "Product not found."
            });
        }

        return Results.Ok(new CatalogProductValidateResponse()
        {
            ProductId = product.Id,
            ProductName = product.Name,
            Price = product.Price
        });
    }
    catch (ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
    {
        return Results.Ok(new CatalogProductValidateErrors
        {
            Message = "Product not found."
        });
    }
    catch (ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.RequestTimeout)
    {
        return Results.Ok(new CatalogProductValidateErrors
        {
            Message = "CatalogService is unavailable. Please try again later."
        });
    }
    catch (HttpRequestException)
    {
        return Results.Ok(new CatalogProductValidateErrors
        {
            Message = "CatalogService is unavailable. Please try again later."
        });
    }
    catch (TaskCanceledException)
    {
        return Results.Ok(new CatalogProductValidateErrors
        {
            Message = "CatalogService is unavailable. Please try again later."
        });
    }
});

//Validate product endpoint = gRPC
app.MapGet("/basket/products/{productId}/validate-grpc", async (
    string productId,
    CatalogGrpc.CatalogGrpcClient catalogClient) =>
{
    if (string.IsNullOrWhiteSpace(productId))
    {
        return Results.BadRequest(new CatalogProductValidateErrors
        {
            Message = "ProductId is required."
        });
    }

    ProductResponse product;
    try
    {
        product = await catalogClient.GetProductByIdAsync(new GetProductByIdRequest
        {
            Id = productId
        });
    }
    catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable || ex.StatusCode == StatusCode.DeadlineExceeded)
    {
        return Results.Ok(new CatalogProductValidateErrors
        {
            Message = "CatalogService gRPC is unavailable. Please try again later."
        });
    }

    if (product.Found == false)
    {
        return Results.Ok(new CatalogProductValidateErrors
        {
            Message = "Product not found."
        });
    }

    return Results.Ok(new CatalogProductValidateResponse()
    {
        ProductId = product.Id,
        ProductName = product.Name,
        Price = (decimal)product.Price,
        Description = product.Description
    });
});

//Preview basket item Refit
app.MapPost("/basket/preview-item", async (
    AddBasketItemRequest request,
    IBasketRepository repository,
    ICatalogApi catalogApi) =>
{
    if (string.IsNullOrWhiteSpace(request.ProductId))
        return Results.BadRequest("ProductId is required.");
    
    if (request.Quantity <= 0)
        return Results.BadRequest("Quantity must be greater than 0.");

    var product = await catalogApi.GetProductByIdAsync(request.ProductId);
    
    if (product is null)
        return Results.NotFound();

    return Results.Ok(new PreviewBasketItemResponse
    {
        ProductId = product.Id,
        ProductName = product.Name,
        Quantity = request.Quantity,
        UnitPrice = product.Price
    });
});

//Preview basket item gRPC 
app.MapPost("/basket/preview-item-grpc", async (
    AddBasketItemRequest request,
    CatalogGrpc.CatalogGrpcClient catalogClient) =>
{
    if (string.IsNullOrWhiteSpace(request.ProductId))
        return Results.BadRequest("ProductId is required.");
    
    if (request.Quantity <= 0)
        return Results.BadRequest("Quantity must be greater than 0.");

    var product = await catalogClient.GetProductByIdAsync(new GetProductByIdRequest
    {
        Id = request.ProductId
    });

    if (product.Found == false)
        return Results.NotFound();

    return Results.Ok(new PreviewBasketItemResponse
    {
        ProductId = product.Id,
        ProductName = product.Name,
        Quantity = request.Quantity,
        UnitPrice = (decimal)product.Price,
        Description = product.Description
    });
});


// Compare REST vs gRPC call duration
app.MapGet("/basket/products/{productId}/compare-rest-grpc", async (
    string productId,
    ICatalogApi catalogApi,
    CatalogGrpc.CatalogGrpcClient catalogClient,
    ILogger<Program> logger) =>
{
    if (string.IsNullOrWhiteSpace(productId))
        return Results.BadRequest("ProductId is required.");

    CatalogProductResponse? restProduct;
    ProductResponse grpcProduct;

    try
    {
        restProduct = await MeasureCatalogCallAsync(
            "REST GetProductByIdAsync",
            productId,
            () => catalogApi.GetProductByIdAsync(productId),
            logger);
    }
    catch (ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
    {
        return Results.NotFound("Product not found.");
    }

    try
    {
        grpcProduct = await MeasureCatalogCallAsync(
            "gRPC GetProductByIdAsync",
            productId,
            async () => await catalogClient.GetProductByIdAsync(new GetProductByIdRequest
            {
                Id = productId
            }),
            logger);
    }
    catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable || ex.StatusCode == StatusCode.DeadlineExceeded)
    {
        return Results.Ok(new
        {
            ProductId = productId,
            RestFound = restProduct is not null,
            GrpcFound = false,
            Message = "CatalogService gRPC is unavailable. Please try again later."
        });
    }

    if (restProduct is null || !grpcProduct.Found)
        return Results.NotFound("Product not found.");

    return Results.Ok(new
    {
        ProductId = productId,
        Rest = new
        {
            restProduct.Id,
            restProduct.Name,
            restProduct.Price
        },
        Grpc = new
        {
            grpcProduct.Id,
            grpcProduct.Name,
            Price = (decimal)grpcProduct.Price
        }
    });
});

// Configure the HTTP request pipeline.

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

static async Task<T> MeasureCatalogCallAsync<T>(
    string operationName,
    string productId,
    Func<Task<T>> operation,
    ILogger logger)
{
    var stopwatch = Stopwatch.StartNew();

    try
    {
        return await operation();
    }
    finally
    {
        stopwatch.Stop();
        logger.LogInformation(
            "CatalogService {OperationName} for product {ProductId} took {ElapsedMilliseconds} ms.",
            operationName,
            productId,
            stopwatch.ElapsedMilliseconds);
    }
}
