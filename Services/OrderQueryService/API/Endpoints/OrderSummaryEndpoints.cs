using OrderQueryService.API.Contracts;
using OrderQueryService.Application.Abstractions;
using OrderQueryService.Application.ReadModels;

namespace OrderQueryService.API.Endpoints;

public static class OrderSummaryEndpoints
{
    public static IEndpointRouteBuilder MapOrderSummaryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/order-summaries")
            .WithTags("Order Summaries");

        group.MapGet("", async (
            IOrderSummaryReadRepository repository,
            int? limit,
            CancellationToken cancellationToken) =>
        {
            var take = limit is > 0 and <= 100 ? limit.Value : 20;
            var summaries = await repository.GetLatestAsync(take, cancellationToken);

            return Results.Ok(summaries);
        })
        .WithName("GetOrderSummaries");

        group.MapGet("/{orderId:guid}", async (
            Guid orderId,
            IOrderSummaryReadRepository repository,
            CancellationToken cancellationToken) =>
        {
            var summary = await repository.GetByOrderIdAsync(orderId, cancellationToken);

            return summary is null
                ? Results.NotFound(new { message = "Order summary not found." })
                : Results.Ok(summary);
        })
        .WithName("GetOrderSummaryByOrderId");

        return app;
    }

    public static IEndpointRouteBuilder MapDebugOrderSummaryEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/debug/order-summaries", async (
            DebugUpsertOrderSummaryRequest request,
            IOrderSummaryReadRepository repository,
            CancellationToken cancellationToken) =>
        {
            Validate(request);

            var now = DateTime.UtcNow;
            var items = request.Items ?? [];

            var model = new OrderSummaryReadModel
            {
                Id = request.OrderId,
                OrderId = request.OrderId,
                CustomerId = request.CustomerId,
                CustomerName = request.CustomerName.Trim(),
                Status = request.Status.Trim(),
                TotalAmount = request.TotalAmount,
                Currency = request.Currency.Trim().ToUpperInvariant(),
                ItemCount = request.ItemCount > 0 ? request.ItemCount : items.Count,
                Items = items.Select(item => new OrderSummaryItemReadModel
                {
                    ProductId = item.ProductId,
                    ProductName = item.ProductName.Trim(),
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice
                }).ToList(),
                CreatedAtUtc = now,
                LastUpdatedAtUtc = now
            };

            await repository.UpsertAsync(model, cancellationToken);
            var persistedModel = await repository.GetByOrderIdAsync(request.OrderId, cancellationToken);

            return Results.Ok(persistedModel ?? model);
        })
        .WithTags("Debug")
        .WithName("DebugUpsertOrderSummary");

        return app;
    }

    private static void Validate(DebugUpsertOrderSummaryRequest request)
    {
        if (request.OrderId == Guid.Empty)
        {
            throw new ArgumentException("OrderId is required.");
        }

        if (request.CustomerId == Guid.Empty)
        {
            throw new ArgumentException("CustomerId is required.");
        }

        if (string.IsNullOrWhiteSpace(request.CustomerName))
        {
            throw new ArgumentException("CustomerName is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Status))
        {
            throw new ArgumentException("Status is required.");
        }

        if (request.TotalAmount < 0)
        {
            throw new ArgumentException("TotalAmount cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(request.Currency))
        {
            throw new ArgumentException("Currency is required.");
        }

        foreach (var item in request.Items ?? [])
        {
            if (item.ProductId == Guid.Empty)
            {
                throw new ArgumentException("ProductId is required.");
            }

            if (string.IsNullOrWhiteSpace(item.ProductName))
            {
                throw new ArgumentException("ProductName is required.");
            }

            if (item.Quantity <= 0)
            {
                throw new ArgumentException("Quantity must be greater than 0.");
            }

            if (item.UnitPrice < 0)
            {
                throw new ArgumentException("UnitPrice cannot be negative.");
            }
        }
    }
}
