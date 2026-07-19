using ProjectionWorker.Application.Events;

namespace ProjectionWorker.Infrastructure.Kafka;

internal enum ProjectionProcessingOutcome
{
    Applied,
    Duplicate,
    PermanentFailure
}

internal sealed record ProjectionProcessingResult(
    ProjectionProcessingOutcome Outcome,
    OrderProjectionEvent? OrderEvent,
    string? Error);
