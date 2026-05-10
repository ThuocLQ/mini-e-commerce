using CatalogService.Commands;
using CatalogService.DTOs;
using CatalogService.Queries;
using MediatR;

namespace CatalogService.API.Endpoints;

public static class ProductEndpoints
{
    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/products")
            .WithTags("Products");

        //Get all products
        group.MapGet("", async (ISender sender) =>
        {
            var result = await sender.Send(new GetProductsQuery()); 
            
            return Results.Ok(result);
        });
        
        //Get 1 product by id
        group.MapGet("/{id}", async (string id, ISender sender) =>
        { 
            var result = await sender.Send(new GetProductByIdQuery(id));
   
            return result is null ? Results.NotFound() : Results.Ok(result);
        });
        
        //Add product
        group.MapPost("", async (CreateProductRequest request, ISender sender) =>
        {
            if(string.IsNullOrWhiteSpace(request.Name))
                return Results.BadRequest("Product name is required.");
    
            var result = await sender.Send(new CreateProductCommand(request.Name, request.Price));
            
            return Results.Created($"/products/{result.Id}", result);
        });
        
        //Update product
        group.MapPut("/{id}", async (string id, UpdateProductRequest updatedProduct, ISender sender) =>
        {
            if (string.IsNullOrWhiteSpace(updatedProduct.Name))
                return Results.BadRequest("Product name is required.");
    
            var result = await sender.Send(new UpdateProductCommand(id, updatedProduct.Name, updatedProduct.Price));
    
            return result is null ? Results.NotFound() : Results.Ok(result);
        });
        
        //Delete product : sau nay co the dung "Soft delete"
        group.MapDelete("/{id}", async (string id, ISender sender) => {
            var result = await sender.Send(new DeleteProductCommand(id));
            
            return result ? Results.NoContent() : Results.NotFound();
        });

        //Search product
        group.MapGet("/search", async (string? keyword, ISender sender) =>
        {
            var result = await sender.Send(new SearchProductsQuery(keyword));
    
            return result.Count == 0 ? Results.NotFound() : Results.Ok(result);
        });

        //Count
        group.MapGet("/count", async (ISender sender) =>
        {
            var count = await sender.Send(new GetProductCountQuery());
            return Results.Ok(count);
        });

        //Search price-range
        group.MapGet("/price-range", async (decimal minPrice, decimal maxPrice, ISender sender) =>
        {
            if (minPrice < 0 || maxPrice < 0 || minPrice > maxPrice)
                return Results.BadRequest("Invalid price range.");
    
            var result = await sender.Send(new GetProductsByPriceQuery(minPrice, maxPrice));
    
            return result.Count == 0 ? Results.NotFound() : Results.Ok(result);
        });
            
        return app;
    }
}
