using CatalogService.API.Contracts;
using CatalogService.Application.Inventory.CommitInventory;
using CatalogService.Application.Inventory.ReleaseInventory;
using CatalogService.Application.Inventory.ReserveInventory;
using MediatR;
using System.Security.Cryptography;
using System.Text;

namespace CatalogService.API.Endpoints;

public static class InventoryEndpoints
{
    public static IEndpointRouteBuilder MapInventoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/_internal/inventory");
        var expectedKey = app.ServiceProvider.GetRequiredService<IConfiguration>()["InternalApi:Key"]
            ?? throw new InvalidOperationException("InternalApi:Key is missing.");

        group.AddEndpointFilter(async (context, next) =>
        {
            var suppliedKey = context.HttpContext.Request.Headers["X-MicroShop-Internal-Key"].ToString();
            if (string.IsNullOrEmpty(suppliedKey) || !CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(suppliedKey),
                    Encoding.UTF8.GetBytes(expectedKey)))
            {
                return Results.NotFound();
            }

            return await next(context);
        });

        group.MapPost("/reservations", async (InventoryReservationRequest request, ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new ReserveInventoryCommand(
                request.OrderId,
                request.Items.Select(item => new InventoryReservationItemDto(item.ProductId, item.Quantity)).ToList(),
                request.ExpiresAtUtc), cancellationToken);

            return result.Succeeded ? Results.Ok(result) : Results.Conflict(result);
        });

        group.MapPost("/reservations/{orderId:guid}/release", async (Guid orderId, ISender sender, CancellationToken cancellationToken) =>
        {
            await sender.Send(new ReleaseInventoryCommand(orderId), cancellationToken);
            return Results.NoContent();
        });

        group.MapPost("/reservations/{orderId:guid}/commit", async (Guid orderId, ISender sender, CancellationToken cancellationToken) =>
        {
            await sender.Send(new CommitInventoryCommand(orderId), cancellationToken);
            return Results.NoContent();
        });

        return app;
    }
}
