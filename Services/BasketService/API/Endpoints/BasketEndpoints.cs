using BasketService.API.Contracts;
using BasketService.Application.Baskets;
using BasketService.Application.Baskets.AddBasketItem;
using BasketService.Application.Baskets.ClearBasket;
using BasketService.Application.Baskets.CompareCatalogCommunication;
using BasketService.Application.Baskets.DeleteBasket;
using BasketService.Application.Baskets.GetBasket;
using BasketService.Application.Baskets.PreviewBasketItem;
using BasketService.Application.Baskets.RemoveBasketItem;
using BasketService.Application.Baskets.UpdateBasketItemQuantity;
using BasketService.Application.Baskets.ValidateCatalogProduct;
using BasketService.Application.Catalog;
using MediatR;

namespace BasketService.API.Endpoints;

public static class BasketEndpoints
{
    public static IEndpointRouteBuilder MapBasketEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/basket/{userId}", async (string userId, ISender sender, CancellationToken cancellationToken) =>
        {
            var basket = await sender.Send(new GetBasketQuery(userId), cancellationToken);

            return Results.Ok(basket);
        });

        app.MapPost("/basket/{userId}/items", async (
            string userId,
            AddBasketItemRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            return await AddItemAsync(
                userId,
                request,
                CatalogCommunicationMode.Rest,
                sender,
                cancellationToken);
        });

        app.MapPost("/basket/{userId}/items-grpc", async (
            string userId,
            AddBasketItemRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            return await AddItemAsync(
                userId,
                request,
                CatalogCommunicationMode.Grpc,
                sender,
                cancellationToken);
        });

        app.MapPut("/basket/{userId}/items/{productId}", async (
            string userId,
            string productId,
            UpdateBasketItemQuantityRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var basket = await sender.Send(
                    new UpdateBasketItemQuantityCommand(userId, productId, request.Quantity),
                    cancellationToken);

                return basket is null ? Results.NotFound() : Results.Ok(basket);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(ex.Message);
            }
        });

        app.MapDelete("/basket/{userId}/items/{productId}", async (
            string userId,
            string productId,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var removed = await sender.Send(new RemoveBasketItemCommand(userId, productId), cancellationToken);

            return removed ? Results.NoContent() : Results.NotFound();
        });

        app.MapPut("/basket/{userId}/clear", async (
            string userId,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var basket = await sender.Send(new ClearBasketCommand(userId), cancellationToken);

            return Results.Ok(basket);
        });

        app.MapDelete("/basket/{userId}", async (
            string userId,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var deleted = await sender.Send(new DeleteBasketCommand(userId), cancellationToken);

            return deleted ? Results.NoContent() : Results.NotFound();
        });

        app.MapGet("/basket/products/{productId}/validate", async (
            string productId,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            return await ValidateProductAsync(
                productId,
                CatalogCommunicationMode.Rest,
                sender,
                cancellationToken);
        });

        app.MapGet("/basket/products/{productId}/validate-grpc", async (
            string productId,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            return await ValidateProductAsync(
                productId,
                CatalogCommunicationMode.Grpc,
                sender,
                cancellationToken);
        });

        app.MapPost("/basket/preview-item", async (
            AddBasketItemRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            return await PreviewItemAsync(
                request,
                CatalogCommunicationMode.Rest,
                sender,
                cancellationToken);
        });

        app.MapPost("/basket/preview-item-grpc", async (
            AddBasketItemRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            return await PreviewItemAsync(
                request,
                CatalogCommunicationMode.Grpc,
                sender,
                cancellationToken);
        });

        app.MapGet("/basket/products/{productId}/compare-communication", async (
            string productId,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var result = await sender.Send(new CompareCatalogCommunicationQuery(productId), cancellationToken);

                return Results.Ok(result);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(ex.Message);
            }
        });

        return app;
    }

    private static async Task<IResult> AddItemAsync(
        string userId,
        AddBasketItemRequest request,
        CatalogCommunicationMode mode,
        ISender sender,
        CancellationToken cancellationToken)
    {
        try
        {
            var basket = await sender.Send(
                new AddBasketItemCommand(userId, request.ProductId, request.Quantity, mode),
                cancellationToken);

            return Results.Ok(basket);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(ex.Message);
        }
        catch (ProductNotFoundException ex)
        {
            return Results.NotFound(ex.Message);
        }
        catch (CatalogUnavailableException)
        {
            return DownstreamUnavailable();
        }
    }

    private static async Task<IResult> ValidateProductAsync(
        string productId,
        CatalogCommunicationMode mode,
        ISender sender,
        CancellationToken cancellationToken)
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
            var result = await sender.Send(
                new ValidateCatalogProductQuery(productId, mode),
                cancellationToken);

            return Results.Ok(ToValidateResponse(result));
        }
        catch (CatalogUnavailableException)
        {
            return DownstreamUnavailable();
        }
    }

    private static async Task<IResult> PreviewItemAsync(
        AddBasketItemRequest request,
        CatalogCommunicationMode mode,
        ISender sender,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await sender.Send(
                new PreviewBasketItemCommand(request.ProductId, request.Quantity, mode),
                cancellationToken);

            return result is null
                ? Results.NotFound()
                : Results.Ok(new PreviewBasketItemResponse
                {
                    ProductId = result.ProductId,
                    ProductName = result.ProductName,
                    Quantity = result.Quantity,
                    UnitPrice = result.UnitPrice,
                    Description = result.Description
                });
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(ex.Message);
        }
        catch (CatalogUnavailableException)
        {
            return DownstreamUnavailable();
        }
    }

    private static object ToValidateResponse(CatalogProductValidateResult result)
    {
        if (!result.Valid)
        {
            return new CatalogProductValidateErrors
            {
                Message = result.Message ?? "Product not found."
            };
        }

        return new CatalogProductValidateResponse
        {
            ProductId = result.ProductId ?? string.Empty,
            ProductName = result.ProductName,
            Price = result.Price,
            Description = result.Description
        };
    }

    private static IResult DownstreamUnavailable()
    {
        return Results.Json(
            new
            {
                ErrorCode = "DOWNSTREAM_UNAVAILABLE",
                Message = "CatalogService is unavailable. Please try again later."
            },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}
