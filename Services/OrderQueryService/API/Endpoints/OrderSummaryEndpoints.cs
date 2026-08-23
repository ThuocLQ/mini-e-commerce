using FluentValidation;
using OrderQueryService.API.Contracts;
using OrderQueryService.API;
using OrderQueryService.Application.Abstractions;
using OrderQueryService.Application.ReadModels;
using System.Security.Claims;

namespace OrderQueryService.API.Endpoints;

public static class OrderSummaryEndpoints
{
    public static IEndpointRouteBuilder MapOrderSummaryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/order-summaries")
            .WithTags("Order Summaries")
            .RequireAuthorization("authenticated");

        group.MapGet("", async (
            ClaimsPrincipal user,
            IOrderSummaryReadRepository repository,
            int? limit,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken) =>
        {
            var logger = loggerFactory.CreateLogger("OrderQueryService.API.Endpoints.OrderSummaries");
            var take = limit is > 0 and <= 100 ? limit.Value : 20;

            logger.LogInformation(
                "Order summaries requested. Service={Service}, Limit={Limit}.",
                "OrderQueryService",
                take);

            if (!TryGetCustomerId(user, out var customerId))
            {
                return Results.Forbid();
            }

            var summaries = user.IsInRole("Admin")
                ? await repository.GetLatestAsync(take, cancellationToken)
                : await repository.GetLatestForCustomerAsync(customerId, take, cancellationToken);

            logger.LogInformation(
                "Order summaries returned. Service={Service}, ResultCount={ResultCount}.",
                "OrderQueryService",
                summaries.Count);

            return Results.Ok(summaries);
        })
        .WithName("GetOrderSummaries");

        group.MapGet("/{orderId:guid}", async (
            Guid orderId,
            ClaimsPrincipal user,
            IOrderSummaryReadRepository repository,
            ILoggerFactory loggerFactory,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var logger = loggerFactory.CreateLogger("OrderQueryService.API.Endpoints.OrderSummaries");

            logger.LogInformation(
                "Order summary requested. Service={Service}, OrderId={OrderId}.",
                "OrderQueryService",
                orderId);

            if (!TryGetCustomerId(user, out var customerId))
            {
                return Results.Forbid();
            }

            var summary = await repository.GetByOrderIdAsync(orderId, cancellationToken);

            if (summary is null || (!user.IsInRole("Admin") && summary.CustomerId != customerId))
            {
                logger.LogWarning(
                    "Order summary not found. Service={Service}, OrderId={OrderId}.",
                    "OrderQueryService",
                    orderId);

                return ApiProblemResults.NotFound(httpContext, "Order summary was not found.");
            }

            return Results.Ok(summary);
        })
        .WithName("GetOrderSummaryByOrderId");

        return app;
    }

    private static bool TryGetCustomerId(ClaimsPrincipal user, out Guid customerId)
    {
        return Guid.TryParse(user.FindFirst(ClaimTypes.NameIdentifier)?.Value, out customerId);
    }

    public static IEndpointRouteBuilder MapDebugOrderSummaryEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/debug/order-summaries", async (
            DebugUpsertOrderSummaryRequest request,
            IValidator<DebugUpsertOrderSummaryRequest> validator,
            IOrderSummaryReadRepository repository,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken);

            if (!validationResult.IsValid)
            {
                var errors = validationResult.Errors
                    .GroupBy(error => error.PropertyName)
                    .ToDictionary(
                        group => group.Key,
                        group => group.Select(error => error.ErrorMessage).ToArray());

                return ApiProblemResults.ValidationProblem(httpContext, errors);
            }

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
        .WithName("DebugUpsertOrderSummary")
        .RequireAuthorization("administrator");

        return app;
    }
}
