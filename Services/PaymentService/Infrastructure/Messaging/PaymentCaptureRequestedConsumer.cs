using BuildingBlocks.Contracts.Correlation;
using BuildingBlocks.Contracts.Events.Payments;
using MassTransit;
using MediatR;
using PaymentService.Application.Payments.RequestCapture;

namespace PaymentService.Infrastructure.Messaging;

public sealed class PaymentCaptureRequestedConsumer(ISender sender)
    : IConsumer<PaymentCaptureRequestedIntegrationEvent>
{
    public Task Consume(ConsumeContext<PaymentCaptureRequestedIntegrationEvent> context)
    {
        using var correlationScope = CorrelationContext.BeginScope(context.Message.CorrelationId);

        return sender.Send(
            new RequestPaymentCaptureCommand(
                context.Message.EventId,
                context.Message.PaymentId,
                context.Message.OrderId,
                context.Message.CustomerId,
                context.Message.Amount,
                context.Message.Currency,
                context.Message.OccurredAtUtc),
            context.CancellationToken);
    }
}
