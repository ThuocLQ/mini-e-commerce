using DiscountService.Application.Discounts.ApplyDiscount;
using DiscountService.Application.Discounts.GetDiscountByCode;
using DiscountService.Application.Discounts.PromotionReservations.RedeemPromotion;
using DiscountService.Application.Discounts.PromotionReservations.ReleasePromotion;
using DiscountService.Application.Discounts.PromotionReservations.ReservePromotion;
using MediatR;

namespace DiscountService.API.Endpoints;

public static class DiscountEndpoints
{
    public static IEndpointRouteBuilder MapDiscountEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/discounts")
            .WithTags("Discounts");

        group.MapGet("/{code}", async (
            string code,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetDiscountByCodeQuery(code), cancellationToken);

            return result is null
                ? Results.NotFound()
                : Results.Ok(result);
        });

        group.MapPost("/apply", async (
            ApplyDiscountRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var command = new ApplyDiscountCommand(
                request.CouponCode,
                request.OrderAmount);

            var result = await sender.Send(command, cancellationToken);

            return Results.Ok(result);
        });

        var internalGroup = app.MapGroup("/_internal/discounts/reservations")
            .WithTags("Internal Promotion Reservations")
            .ExcludeFromDescription()
            .RequireInternalApiKey(app.ServiceProvider.GetRequiredService<IConfiguration>());

        internalGroup.MapPost("", async (ReservePromotionRequest request, ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new ReservePromotionCommand(request.CouponCode, request.OrderId, request.CustomerId, request.OrderAmount, request.ExpiresAtUtc), cancellationToken);
            return result.IsReserved ? Results.Ok(result) : Results.Conflict(result);
        });
        internalGroup.MapPost("/{reservationId:guid}/redeem", async (Guid reservationId, PromotionOperationRequest request, ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new RedeemPromotionCommand(reservationId, request.OrderId), cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });
        internalGroup.MapPost("/{reservationId:guid}/release", async (Guid reservationId, PromotionOperationRequest request, ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new ReleasePromotionCommand(reservationId, request.OrderId, request.Reason ?? "Order did not complete."), cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        return app;
    }

    private sealed record ApplyDiscountRequest(
        string CouponCode,
        decimal OrderAmount);
    private sealed record ReservePromotionRequest(string CouponCode, Guid OrderId, Guid CustomerId, decimal OrderAmount, DateTime ExpiresAtUtc);
    private sealed record PromotionOperationRequest(Guid OrderId, string? Reason = null);
}
