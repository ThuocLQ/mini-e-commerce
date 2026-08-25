using BuildingBlocks.Contracts.Correlation;
using BuildingBlocks.Contracts.Events.Discounts;
using DiscountService.Application.Discounts.PromotionReservations.RedeemPromotion;
using DiscountService.Application.Discounts.PromotionReservations.ReleasePromotion;
using MassTransit;
using MediatR;

namespace DiscountService.Infrastructure.Messaging;

public sealed class PromotionRedeemRequestedConsumer(ISender sender)
    : IConsumer<PromotionRedeemRequestedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<PromotionRedeemRequestedIntegrationEvent> context)
    {
        using var correlationScope = CorrelationContext.BeginScope(context.Message.CorrelationId);

        var reservation = await sender.Send(
            new RedeemPromotionCommand(context.Message.ReservationId, context.Message.OrderId),
            context.CancellationToken);

        if (reservation is null)
        {
            throw new InvalidOperationException(
                $"Promotion reservation '{context.Message.ReservationId}' for order '{context.Message.OrderId}' was not found.");
        }
    }
}

public sealed class PromotionReleaseRequestedConsumer(ISender sender)
    : IConsumer<PromotionReleaseRequestedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<PromotionReleaseRequestedIntegrationEvent> context)
    {
        using var correlationScope = CorrelationContext.BeginScope(context.Message.CorrelationId);

        var reservation = await sender.Send(
            new ReleasePromotionCommand(
                context.Message.ReservationId,
                context.Message.OrderId,
                context.Message.Reason),
            context.CancellationToken);

        if (reservation is null)
        {
            throw new InvalidOperationException(
                $"Promotion reservation '{context.Message.ReservationId}' for order '{context.Message.OrderId}' was not found.");
        }
    }
}
