using Dapper;
using Microsoft.Extensions.Options;
using MicroShop.ServiceDefaults.Diagnostics;
using OrderingService.Infrastructure.Persistence;

namespace OrderingService.Infrastructure.Outbox;

public sealed class OutboxMetricsBackgroundService : BackgroundService
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<OutboxMetricsBackgroundService> _logger;
    private readonly OutboxPublisherOptions _options;

    public OutboxMetricsBackgroundService(
        IDbConnectionFactory connectionFactory,
        ILogger<OutboxMetricsBackgroundService> logger,
        IOptions<OutboxPublisherOptions> options)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
        _options = options.Value;
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
                    COUNT(*) FILTER (
                        WHERE ProcessedAtUtc IS NULL
                          AND RetryCount < @MaxRetryCount
                    ) AS Pending,
                    COUNT(*) FILTER (
                        WHERE ProcessedAtUtc IS NULL
                          AND RetryCount >= @MaxRetryCount
                    ) AS Failed
                FROM OutboxMessages;
                """, new { _options.MaxRetryCount }, cancellationToken: cancellationToken));

            MicroShopMetrics.SetOutboxSnapshot("OrderingService", counts.Pending, counts.Failed);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "Failed to collect OrderingService outbox metrics.");
        }
    }

    private sealed record OutboxCounts(long Pending, long Failed);
}
