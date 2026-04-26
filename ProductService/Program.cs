using MediatR;
using ProductService;
using ProductService.Commands;
using ProductService.DTOs;
using ProductService.Models;
using ProductService.Queries;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

builder.Services.AddSingleton<ProductStore>();

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

var app = builder.Build();

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
    
    var product = await mediator.Send(new CreateProductCommand(request.Name));
    return Results.Created($"/products/{product.Name}", product);
});

//Sua san pham
app.MapPut("/products/{id}", async (string id, UpdateProductRequest updatedProduct, IMediator mediator) =>
{
    if (string.IsNullOrWhiteSpace(updatedProduct.Name))
        return Results.BadRequest("Product name is required.");
    
    var product = await mediator.Send(new UpdateProductCommand(id, updatedProduct.Name));
    
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
    return Results.Ok(products);
});

// Configure the HTTP request pipeline.

app.UseHttpsRedirection();

app.UseAuthorization();

app.Run();