using CatalogService.Application.Products.CreateProduct;
using CatalogService.Application.Products.DeleteProduct;
using CatalogService.Application.Products.GetProductById;
using CatalogService.Application.Products.GetProductCount;
using CatalogService.Application.Products.GetProducts;
using CatalogService.Application.Products.GetProductsByPrice;
using CatalogService.Application.Products.SearchProducts;
using CatalogService.Application.Products.UpdateProduct;
using CatalogService.API.Contracts;
using FluentValidation;
using FluentValidation.Results;
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

        //Search product
        group.MapGet("/search", async (
            string? keyword,
            IValidator<SearchProductsQuery> validator,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var query = new SearchProductsQuery(keyword);
            var validationResult = await validator.ValidateAsync(query, cancellationToken);

            if (!validationResult.IsValid)
                return ValidationProblem(validationResult);

            var result = await sender.Send(query, cancellationToken);

            return Results.Ok(result);
        });

        //Count
        group.MapGet("/count", async (ISender sender) =>
        {
            var count = await sender.Send(new GetProductCountQuery());
            return Results.Ok(count);
        });

        //Search price-range
        group.MapGet("/price-range", async (
            decimal minPrice,
            decimal maxPrice,
            IValidator<GetProductsByPriceQuery> validator,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var query = new GetProductsByPriceQuery(minPrice, maxPrice);
            var validationResult = await validator.ValidateAsync(query, cancellationToken);

            if (!validationResult.IsValid)
                return ValidationProblem(validationResult);

            var result = await sender.Send(query, cancellationToken);

            return Results.Ok(result);
        });

        //Add product
        group.MapPost("", async (
            CreateProductRequest request,
            IValidator<CreateProductCommand> validator,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var command = new CreateProductCommand(request.Name, request.Price);
            var validationResult = await validator.ValidateAsync(command, cancellationToken);

            if (!validationResult.IsValid)
                return ValidationProblem(validationResult);

            var result = await sender.Send(command, cancellationToken);
            
            return Results.Created($"/products/{result.Id}", result);
        })
            .RequireAuthorization("AdminOnly");
        
        //Update product
        group.MapPut("/{id}", async (
            string id,
            UpdateProductRequest updatedProduct,
            IValidator<UpdateProductCommand> validator,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var command = new UpdateProductCommand(id, updatedProduct.Name, updatedProduct.Price);
            var validationResult = await validator.ValidateAsync(command, cancellationToken);

            if (!validationResult.IsValid)
                return ValidationProblem(validationResult);

            var result = await sender.Send(command, cancellationToken);
    
            return result is null ? Results.NotFound() : Results.Ok(result);
        })
            .RequireAuthorization("AdminOnly");;
        
        //Delete product : sau nay co the dung "Soft delete"
        group.MapDelete("/{id}", async (
            string id,
            IValidator<DeleteProductCommand> validator,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var command = new DeleteProductCommand(id);
            var validationResult = await validator.ValidateAsync(command, cancellationToken);

            if (!validationResult.IsValid)
                return ValidationProblem(validationResult);

            var result = await sender.Send(command, cancellationToken);
            
            return result ? Results.NoContent() : Results.NotFound();
        })
        .RequireAuthorization("AdminOnly");;

        //Get 1 product by id
        group.MapGet("/{id}", async (
            string id,
            IValidator<GetProductByIdQuery> validator,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var query = new GetProductByIdQuery(id);
            var validationResult = await validator.ValidateAsync(query, cancellationToken);

            if (!validationResult.IsValid)
                return ValidationProblem(validationResult);

            var result = await sender.Send(query, cancellationToken);

            return result is null ? Results.NotFound() : Results.Ok(result);
        });
            
        return app;
    }

    private static IResult ValidationProblem(ValidationResult validationResult)
    {
        return Results.ValidationProblem(validationResult.ToDictionary());
    }
}
