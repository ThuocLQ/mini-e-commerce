using Dapper;
using MicroShop.ServiceDefaults.Diagnostics;
using PaymentService.Infrastructure.Persistence;

namespace PaymentService.Infrastructure.Outbox;

public sealed class PaymentOutboxMetricsBackgroundService : BackgroundService
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<PaymentOutboxMetricsBackgroundService> _logger;

    public PaymentOutboxMetricsBackgroundService(
        IDbConnectionFactory connectionFactory,
        ILogger<PaymentOutboxMetricsBackgroundService> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));

        await RefreshAsync(stoppingToken);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RefreshAsync(stoppingToken);
        }
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            var counts = await connection.QuerySingleAsync<OutboxCounts>(new CommandDefinition("""
                SELECT
                    COUNT(*) FILTER (WHERE Status IN ('Pending', 'Processing')) AS Pending,
                    COUNT(*) FILTER (WHERE Status = 'Failed') AS Failed
                FROM PaymentOutboxMessages;
                """, cancellationToken: cancellationToken));

            MicroShopMetrics.SetOutboxSnapshot("PaymentService", counts.Pending, counts.Failed);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "Failed to collect PaymentService outbox metrics.");
        }
    }

    private sealed record OutboxCounts(long Pending, long Failed);
}
