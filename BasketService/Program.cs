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
var catalogServiceUrl = builder.Configuration.GetSection("CatalogServiceUrl")
                            ?? throw new InvalidOperationException("Configuration 'CatalogServiceUrl' not found.");
builder.Services.AddRefitClient<ICatalogApi>()
    .ConfigureHttpClient(c =>
    {
        c.BaseAddress = new Uri(catalogServiceUrl.ToString());
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
    string userId, AddBasketItemRequest request, IBasketRepository repository, ICatalogApi catalogApi) =>
{
    // Validate
    if (string.IsNullOrWhiteSpace(request.ProductId))
        return Results.BadRequest("ProductId is required.");
    
    if (request.Quantity <= 0)
        return Results.BadRequest("Quantity must be greater than 0.");
    
    // Call CatalogService to validate product and get details
    var product = await catalogApi.GetProductByIdAsync(request.ProductId);

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


// Configure the HTTP request pipeline.

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
