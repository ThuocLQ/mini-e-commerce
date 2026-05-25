using OrderingService.Application.Abstractions;

namespace OrderingService.API.Endpoints;

public static class OutboxEndpoints
{
    public static IEndpointRouteBuilder MapOutboxEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/debug/outbox", async (
                IOutboxRepository outboxRepository,
                int? limit,
                CancellationToken cancellationToken) =>
            {
                var take = limit is > 0 and <= 100 ? limit.Value : 20;
                var messages = await outboxRepository.GetLatestAsync(take, cancellationToken);

                var result = messages.Select(message => new
                {
                    message.Id,
                    message.Type,
                    message.OccurredAtUtc,
                    message.NextAttemptAtUtc,
                    message.ProcessedAtUtc,
                    message.RetryCount,
                    message.LastError,
                    message.LockId,
                    message.LockedUntilUtc,
                    Status = message.ProcessedAtUtc is not null ? "Processed" : "Pending"
                });

                return Results.Ok(result);
            })
            .WithTags("Debug")
            .WithName("DebugOutbox");

        return app;
    }
}
