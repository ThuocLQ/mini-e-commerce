using DiscountService.Application.Discounts.ApplyDiscount;
using DiscountService.Application.Discounts.GetDiscountByCode;
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

        return app;
    }

    private sealed record ApplyDiscountRequest(
        string CouponCode,
        decimal OrderAmount);
}