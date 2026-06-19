using MediatR;
using OrderingService.API.Contracts;
using OrderingService.Application.OrderPaymentSagas.ApplyPaymentEvent;

namespace OrderingService.API.Endpoints;

public static class PaymentSagaEndpoints
{
    public static IEndpointRouteBuilder MapPaymentSagaEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/orders")
            .WithTags("Payment Saga");

        group.MapPost("/{orderId:guid}/payment-events", async (
            Guid orderId,
            ApplyPaymentSagaEventRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            if (!TryParseEventType(request.EventType, out var eventType))
            {
                return Results.BadRequest(new
                {
                    Error = "EventType must be 'PaymentSucceeded', 'PaymentFailed', or 'PaymentTimedOut'."
                });
            }

            var result = await sender.Send(new ApplyPaymentSagaEventCommand(
                request.EventId,
                eventType,
                orderId,
                request.PaymentId,
                request.FailureReason), cancellationToken);

            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        return app;
    }

    private static bool TryParseEventType(string eventType, out OrderPaymentSagaEventType result)
    {
        result = default;

        if (string.IsNullOrWhiteSpace(eventType))
        {
            return false;
        }

        var normalized = eventType.Trim();
        normalized = normalized.Equals("Succeeded", StringComparison.OrdinalIgnoreCase)
            ? nameof(OrderPaymentSagaEventType.PaymentSucceeded)
            : normalized;
        normalized = normalized.Equals("Failed", StringComparison.OrdinalIgnoreCase)
            ? nameof(OrderPaymentSagaEventType.PaymentFailed)
            : normalized;
        normalized = normalized.Equals("TimedOut", StringComparison.OrdinalIgnoreCase) ||
                     normalized.Equals("Timeout", StringComparison.OrdinalIgnoreCase)
            ? nameof(OrderPaymentSagaEventType.PaymentTimedOut)
            : normalized;

        return Enum.TryParse(normalized, ignoreCase: true, out result) &&
               Enum.IsDefined(result);
    }
}
