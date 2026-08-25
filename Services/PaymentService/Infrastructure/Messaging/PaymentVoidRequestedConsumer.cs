using BuildingBlocks.Contracts.Correlation;
using BuildingBlocks.Contracts.Events.Payments;
using MassTransit;
using MediatR;
using PaymentService.Application.Payments.RequestVoid;

namespace PaymentService.Infrastructure.Messaging;

public sealed class PaymentVoidRequestedConsumer(ISender sender)
    : IConsumer<PaymentVoidRequestedIntegrationEvent>
{
    public Task Consume(ConsumeContext<PaymentVoidRequestedIntegrationEvent> context)
    {
        using var correlationScope = CorrelationContext.BeginScope(context.Message.CorrelationId);

        return sender.Send(new RequestPaymentVoidCommand(
            context.Message.EventId,
            context.Message.PaymentId,
            context.Message.OrderId,
            context.Message.CustomerId,
            context.Message.Amount,
            context.Message.Currency,
            context.Message.Reason,
            context.Message.OccurredAtUtc), context.CancellationToken);
    }
}
