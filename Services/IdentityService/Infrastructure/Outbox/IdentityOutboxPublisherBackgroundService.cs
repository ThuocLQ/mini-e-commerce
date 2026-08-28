using System.Text.Json;
using BuildingBlocks.Contracts.Events.Identity;
using Dapper;
using IdentityService.Infrastructure.Persistence;
using MassTransit;
using Microsoft.Extensions.Options;

namespace IdentityService.Infrastructure.Outbox;

public sealed class IdentityOutboxPublisherBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<IdentityOutboxPublisherOptions> _options;
    private readonly ILogger<IdentityOutboxPublisherBackgroundService> _logger;

    public IdentityOutboxPublisherBackgroundService(IServiceScopeFactory scopeFactory, IOptions<IdentityOutboxPublisherOptions> options, ILogger<IdentityOutboxPublisherBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Value.Enabled) return;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.Value.IntervalSeconds));
        do { await PublishPendingAsync(stoppingToken); }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task PublishPendingAsync(CancellationToken cancellationToken)
    {
        var options = _options.Value;
        var lockId = Guid.NewGuid();
        using var scope = _scopeFactory.CreateScope();
        var connectionFactory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();
        using var connection = connectionFactory.CreateConnection();

        var messages = await connection.QueryAsync<OutboxRow>(new CommandDefinition("""
            WITH candidates AS (
                SELECT Id FROM IdentityOutboxMessages
                WHERE ProcessedAtUtc IS NULL AND RetryCount < 10 AND NextAttemptAtUtc <= CURRENT_TIMESTAMP
                    AND (LockedUntilUtc IS NULL OR LockedUntilUtc <= CURRENT_TIMESTAMP)
                ORDER BY OccurredAtUtc LIMIT @BatchSize FOR UPDATE SKIP LOCKED
            )
            UPDATE IdentityOutboxMessages message
            SET LockId = @LockId, LockedUntilUtc = CURRENT_TIMESTAMP + make_interval(secs => @LockSeconds)
            FROM candidates WHERE message.Id = candidates.Id
            RETURNING message.Id, message.Content::text AS Content, message.RetryCount;
            """, new { options.BatchSize, LockId = lockId, options.LockSeconds }, cancellationToken: cancellationToken));

        foreach (var message in messages)
        {
            try
            {
                var integrationEvent = JsonSerializer.Deserialize<CustomerEmailVerificationRequestedIntegrationEvent>(message.Content, new JsonSerializerOptions(JsonSerializerDefaults.Web))
                    ?? throw new InvalidOperationException("Identity verification outbox payload is invalid.");
                await publisher.Publish(integrationEvent, cancellationToken);
                await connection.ExecuteAsync(new CommandDefinition("""
                    UPDATE IdentityOutboxMessages SET ProcessedAtUtc = CURRENT_TIMESTAMP, LockId = NULL, LockedUntilUtc = NULL, LastError = NULL
                    WHERE Id = @Id AND LockId = @LockId;
                    """, new { message.Id, LockId = lockId }, cancellationToken: cancellationToken));
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                await connection.ExecuteAsync(new CommandDefinition("""
                    UPDATE IdentityOutboxMessages
                    SET RetryCount = RetryCount + 1, LastError = @Error, NextAttemptAtUtc = CURRENT_TIMESTAMP + interval '15 seconds', LockId = NULL, LockedUntilUtc = NULL
                    WHERE Id = @Id AND LockId = @LockId;
                    """, new { message.Id, LockId = lockId, Error = ex.Message[..Math.Min(ex.Message.Length, 4000)] }, cancellationToken: cancellationToken));
                _logger.LogWarning(ex, "Identity verification outbox publish failed. OutboxMessageId={OutboxMessageId}", message.Id);
            }
        }
    }

    private sealed record OutboxRow(Guid Id, string Content, int RetryCount);
}