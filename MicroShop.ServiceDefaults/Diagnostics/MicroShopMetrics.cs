using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace MicroShop.ServiceDefaults.Diagnostics;

public static class MicroShopMetrics
{
    public const string MeterName = "MicroShop.Operations";

    private static readonly Meter Meter = new(MeterName);
    private static readonly ConcurrentDictionary<string, OutboxSnapshot> OutboxSnapshots = new();

    private static readonly Counter<long> OutboxMessages = Meter.CreateCounter<long>(
        "microshop.outbox.messages",
        description: "Number of outbox messages handled by outcome.");

    private static readonly Counter<long> WebhookRequests = Meter.CreateCounter<long>(
        "microshop.webhooks.requests",
        description: "Number of payment webhook requests handled by outcome.");

    private static readonly Counter<long> ProjectionEvents = Meter.CreateCounter<long>(
        "microshop.projection.events",
        description: "Number of projection events handled by outcome.");

    static MicroShopMetrics()
    {
        Meter.CreateObservableGauge(
            "microshop.outbox.pending",
            ObservePendingOutboxMessages,
            description: "Current number of pending or processing outbox messages.");

        Meter.CreateObservableGauge(
            "microshop.outbox.failed",
            ObserveFailedOutboxMessages,
            description: "Current number of failed outbox messages.");
    }

    public static void RecordOutboxMessage(string service, string outcome, long count = 1)
    {
        OutboxMessages.Add(
            count,
            new KeyValuePair<string, object?>("service", service),
            new KeyValuePair<string, object?>("outcome", outcome));
    }

    public static void SetOutboxSnapshot(string service, long pending, long failed)
    {
        OutboxSnapshots[service] = new OutboxSnapshot(pending, failed);
    }

    public static void RecordWebhookRequest(string outcome)
    {
        WebhookRequests.Add(
            1,
            new KeyValuePair<string, object?>("service", "PaymentService"),
            new KeyValuePair<string, object?>("outcome", outcome));
    }

    public static void RecordProjectionEvent(string outcome, string? eventType)
    {
        ProjectionEvents.Add(
            1,
            new KeyValuePair<string, object?>("service", "ProjectionWorker"),
            new KeyValuePair<string, object?>("outcome", outcome),
            new KeyValuePair<string, object?>("event_type", eventType ?? "unknown"));
    }

    private static IEnumerable<Measurement<long>> ObservePendingOutboxMessages()
    {
        return OutboxSnapshots.Select(snapshot =>
            new Measurement<long>(
                snapshot.Value.Pending,
                new KeyValuePair<string, object?>("service", snapshot.Key)));
    }

    private static IEnumerable<Measurement<long>> ObserveFailedOutboxMessages()
    {
        return OutboxSnapshots.Select(snapshot =>
            new Measurement<long>(
                snapshot.Value.Failed,
                new KeyValuePair<string, object?>("service", snapshot.Key)));
    }

    private sealed record OutboxSnapshot(long Pending, long Failed);
}
