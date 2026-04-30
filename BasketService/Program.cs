using System.Diagnostics;
using BasketService.Clients;
using BasketService.DTOs;
using BasketService.Models;
using StackExchange.Redis;
using BasketService.Repositories;
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

// Add item to basket
app.MapPost("/basket/{userId}/items", async (
    string userId,
    AddBasketItemRequest request,
    IBasketRepository repository,
    ICatalogApi catalogApi,
    ILogger<Program> logger) =>
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
        product = await GetCatalogProductByIdAsync(catalogApi, request.ProductId, logger);
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

// Validate product endpoint
app.MapGet("/basket/products/{productId}/validate", async (
    string productId,
    ICatalogApi catalogApi,
    ILogger<Program> logger) =>
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
        var product = await GetCatalogProductByIdAsync(catalogApi, productId, logger);
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

//Preview basket item
app.MapPost("/basket/preview-item", async (
    AddBasketItemRequest request,
    IBasketRepository repository,
    ICatalogApi catalogApi,
    ILogger<Program> logger) =>
{
    if (string.IsNullOrWhiteSpace(request.ProductId))
        return Results.BadRequest("ProductId is required.");
    
    if (request.Quantity <= 0)
        return Results.BadRequest("Quantity must be greater than 0.");

    var product = await GetCatalogProductByIdAsync(catalogApi, request.ProductId, logger);
    
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


// Configure the HTTP request pipeline.

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

static async Task<CatalogProductResponse?> GetCatalogProductByIdAsync(
    ICatalogApi catalogApi,
    string productId,
    ILogger logger)
{
    var stopwatch = Stopwatch.StartNew();

    try
    {
        return await catalogApi.GetProductByIdAsync(productId);
    }
    finally
    {
        stopwatch.Stop();
        logger.LogInformation(
            "CatalogService GetProductByIdAsync for product {ProductId} took {ElapsedMilliseconds} ms.",
            productId,
            stopwatch.ElapsedMilliseconds);
    }
}
