using CatalogService.Application.Products;
using CatalogService.Application.Products.CreateProduct;
using CatalogService.Application.Products.DeleteProduct;
using CatalogService.Application.Products.DiscoverProducts;
using CatalogService.Application.Products.GetProductById;
using CatalogService.Application.Products.GetProductCount;
using CatalogService.Application.Products.GetProducts;
using CatalogService.Application.Products.GetProductsByPrice;
using CatalogService.Application.Products.SearchProducts;
using CatalogService.Application.Products.UpdateProduct;
using CatalogService.Application.Products.SetProductStock;
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

        group.MapGet("/discovery", async (
            string? keyword,
            string? category,
            string? sort,
            int? pageSize,
            string? cursor,
            IValidator<DiscoverProductsQuery> validator,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            if (!ProductDiscoverySortExtensions.TryParse(sort, out var parsedSort))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["sort"] = ["Sort must be one of: name_asc, name_desc, price_asc, price_desc."]
                });
            }

            var query = new DiscoverProductsQuery(keyword, category, parsedSort, pageSize ?? 24, cursor);
            var validationResult = await validator.ValidateAsync(query, cancellationToken);

            if (!validationResult.IsValid)
            {
                return ValidationProblem(validationResult);
            }

            return Results.Ok(await sender.Send(query, cancellationToken));
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
            var command = new CreateProductCommand(request.Name, request.Price, request.Description, request.StockQuantity, request.Category, request.ImageUrl, request.Sku, request.Brand);
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
            var command = new UpdateProductCommand(id, updatedProduct.Name, updatedProduct.Price, updatedProduct.Description, updatedProduct.Category, updatedProduct.ImageUrl, updatedProduct.Brand, updatedProduct.Sku);
            var validationResult = await validator.ValidateAsync(command, cancellationToken);

            if (!validationResult.IsValid)
                return ValidationProblem(validationResult);

            var result = await sender.Send(command, cancellationToken);
    
            return result is null ? Results.NotFound() : Results.Ok(result);
        })
            .RequireAuthorization("AdminOnly");;

        group.MapPut("/{id}/stock", async (
            string id,
            SetProductStockRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new SetProductStockCommand(id, request.StockQuantity), cancellationToken);
            return Results.Ok(result);
        })
        .RequireAuthorization("AdminOnly");
        
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
