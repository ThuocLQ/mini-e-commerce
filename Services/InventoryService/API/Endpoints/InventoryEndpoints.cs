using InventoryService.API.Contracts;
using InventoryService.Application.Inventory.CommitInventory;
using InventoryService.Application.Inventory.ReleaseInventory;
using InventoryService.Application.Inventory.ReserveInventory;
using InventoryService.Application.Inventory.ReceiveInventoryStock;
using InventoryService.Application.Inventory.UpsertStock;
using InventoryService.Application.Inventory.GetInventoryItems;
using MediatR;
using System.Security.Cryptography;
using System.Text;

namespace InventoryService.API.Endpoints;

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

        group.MapPost("/stock-receipts", async (InventoryStockReceiptRequest request, ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new ReceiveInventoryStockCommand(
                request.ReceiptId,
                request.SourcePurchaseOrderId,
                request.Items.Select(item => new InventoryStockReceiptItem(item.ProductId, item.Quantity)).ToList()), cancellationToken);
            return Results.Ok(result);
        });
        group.MapPost("/reservations", async (InventoryReservationRequest request, ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new ReserveInventoryCommand(
                request.OrderId,
                request.Items.Select(item => new InventoryReservationItemDto(item.ProductId, item.Quantity)).ToList(),
                request.ExpiresAtUtc), cancellationToken);

            return result.Succeeded ? Results.Ok(result) : Results.Conflict(result);
        });

        group.MapPut("/items/{productId}/stock", async (string productId, InventoryStockRequest request, ISender sender, CancellationToken cancellationToken) =>
        {
            await sender.Send(new UpsertInventoryStockCommand(productId, request.StockQuantity), cancellationToken);
            return Results.NoContent();
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

        var adminGroup = app.MapGroup("/inventory/admin")
            .WithTags("Inventory")
            .RequireAuthorization("administrator");

        adminGroup.MapGet("/items", async (ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetInventoryItemsQuery(), cancellationToken);
            return Results.Ok(result);
        });

        return app;
    }
}

public sealed record InventoryStockRequest(int StockQuantity);

