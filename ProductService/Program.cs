using MediatR;
using ProductService;
using ProductService.Commands;
using ProductService.Data;
using ProductService.DTOs;
using ProductService.Queries;
using ProductService.Repositories;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

builder.Services.AddSingleton<DatabaseInitializer>();
builder.Services.AddScoped<IProductRepository, ProductRepository>();

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
    await initializer.InitializeAsync();
}

//Lay danh sach SP
app.MapGet("/products", async (IMediator mediator) =>
{
    var  products = await mediator.Send(new GetProductsQuery());
    return Results.Ok(products);
});

//Lay 1 san pham
app.MapGet("/products/{id}", async (string id, IMediator mediator) =>
{
   var product = await mediator.Send(new GetProductByIdQuery(id));
   
   if (product is null)
       return Results.NotFound();
   
   return Results.Ok(product);
});

//Them san pham
app.MapPost("/products", async (CreateProductRequest request, IMediator mediator) =>
{
    if(string.IsNullOrWhiteSpace(request.Name))
        return Results.BadRequest("Product name is required.");
    
    var product = await mediator.Send(new CreateProductCommand(request.Name, request.Price));
    return Results.Created($"/products/{product.Name}", product);
});

//Sua san pham
app.MapPut("/products/{id}", async (string id, UpdateProductRequest updatedProduct, IMediator mediator) =>
{
    if (string.IsNullOrWhiteSpace(updatedProduct.Name))
        return Results.BadRequest("Product name is required.");
    
    var product = await mediator.Send(new UpdateProductCommand(id, updatedProduct.Name, updatedProduct.Price));
    
    if (product is null)
        return Results.NotFound();
    
    return Results.Ok(product);
});

//Xoa san pham
app.MapDelete("/products/{id}", async (string id, IMediator mediator) => {
   var deleted = await mediator.Send(new DeleteProductCommand(id));
   if (!deleted)
       return Results.NotFound();
   return Results.NoContent();
});

//Search product
app.MapGet("/products/search", async (string? keyword, IMediator mediator) =>
{
    var products = await mediator.Send(new SearchProductsQuery(keyword));
    
    if (products is null)
        return Results.NotFound();
    
    return Results.Ok(products);
});

//So luong san pham
app.MapGet("/products/count", async (IMediator mediator) =>
{
    var count = await mediator.Send(new GetProductCountQuery());
    return Results.Ok(count);
});

//Tim san pham theo khoang gia
app.MapGet("/products/price-range", async (decimal minPrice, decimal maxPrice, IMediator mediator) =>
{
    if (minPrice < 0 || maxPrice < 0 || minPrice > maxPrice)
        return Results.BadRequest("Invalid price range.");
    
    var products = await mediator.Send(new GetProductsByPriceQuery(minPrice, maxPrice));
    
    if (products is null)
        return Results.NotFound();
    
    return Results.Ok(products);
});

// Configure the HTTP request pipeline.

app.UseHttpsRedirection();

app.UseAuthorization();

app.Run();