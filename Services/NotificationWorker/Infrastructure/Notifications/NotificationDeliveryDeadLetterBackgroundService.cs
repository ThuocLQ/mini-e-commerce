using Microsoft.Extensions.Options;
using NotificationWorker.Application.Abstractions;
using NotificationWorker.Infrastructure.Messaging;

namespace NotificationWorker.Infrastructure.Notifications;

public sealed class NotificationDeliveryDeadLetterBackgroundService : BackgroundService
{
    private readonly INotificationDeliveryStore _deliveries;
    private readonly MessageRetryOptions _retry;
    private readonly NotificationDeliveryRecoveryOptions _options;
    private readonly ILogger<NotificationDeliveryDeadLetterBackgroundService> _logger;

    public NotificationDeliveryDeadLetterBackgroundService(
        INotificationDeliveryStore deliveries,
        IOptions<MessageRetryOptions> retry,
        IOptions<NotificationDeliveryRecoveryOptions> options,
        ILogger<NotificationDeliveryDeadLetterBackgroundService> logger)
    {
        _deliveries = deliveries;
        _retry = retry.Value;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.ScanIntervalSeconds));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                var marked = await _deliveries.MarkExhaustedAsDeadLetterAsync(
                    maxAttempts: _retry.RetryCount + 1,
                    olderThanUtc: DateTime.UtcNow.AddSeconds(-_options.FailureGraceSeconds),
                    stoppingToken);

                if (marked > 0)
                {
                    _logger.LogWarning(
                        "Marked {DeliveryCount} notification deliveries as DeadLetter after retry budget was exhausted. MaxAttempts={MaxAttempts}",
                        marked,
                        _retry.RetryCount + 1);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Notification delivery dead-letter reconciliation failed.");
            }
        }
    }
}