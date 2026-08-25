using BuildingBlocks.Contracts.Correlation;
using BuildingBlocks.Contracts.Events.Payments;
using MassTransit;
using MediatR;
using PaymentService.Application.Payments.RequestRefund;

namespace PaymentService.Infrastructure.Messaging;

public sealed class PaymentRefundRequestedConsumer(ISender sender) : IConsumer<PaymentRefundRequestedIntegrationEvent>
{
    public Task Consume(ConsumeContext<PaymentRefundRequestedIntegrationEvent> context)
    {
        using var correlationScope = CorrelationContext.BeginScope(context.Message.CorrelationId);
        return sender.Send(new RequestPaymentRefundCommand(
            context.Message.EventId, context.Message.PaymentId, context.Message.OrderId, context.Message.CustomerId,
            context.Message.Amount, context.Message.Currency, context.Message.Reason, context.Message.OccurredAtUtc),
            context.CancellationToken);
    }
}
