using Npgsql;
using NotificationWorker.Application.Abstractions;

namespace NotificationWorker.Infrastructure.Persistence;

public sealed class PostgresNotificationDeliveryStore : INotificationDeliveryStore
{
    private static readonly TimeSpan ProcessingLeaseDuration = TimeSpan.FromMinutes(10);
    private readonly string _connectionString;

    public PostgresNotificationDeliveryStore(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("NotificationDb")
            ?? throw new InvalidOperationException("Connection string 'NotificationDb' is missing.");
    }

    public async Task<NotificationDeliveryLeaseAcquisition> TryStartAsync(
        NotificationDelivery delivery,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var leaseToken = Guid.NewGuid();
        var deliveryId = Guid.NewGuid();

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        const string insertSql = """
            INSERT INTO NotificationDeliveries (
                Id, EventId, EventType, Template, Channel, CustomerId, OrderId, CorrelationId,
                Status, AttemptCount, ProcessingLeaseToken, ProcessingLeaseExpiresAtUtc, CreatedAtUtc, UpdatedAtUtc)
            VALUES (
                @Id, @EventId, @EventType, @Template, @Channel, @CustomerId, @OrderId, @CorrelationId,
                'Sending', 1, @LeaseToken, @LeaseExpiresAtUtc, @Now, @Now)
            ON CONFLICT (EventId, Template, Channel) DO NOTHING
            RETURNING Id;
            """;

        var insertedId = await ExecuteScalarAsync(
            connection, transaction, insertSql, delivery, deliveryId, leaseToken, now, cancellationToken);
        if (insertedId is not null)
        {
            await InsertAttemptAsync(connection, transaction, insertedId.Value, 1, now, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new NotificationDeliveryLeaseAcquisition(NotificationDeliveryStartResult.Started, insertedId, leaseToken);
        }

        const string retrySql = """
            UPDATE NotificationDeliveries
            SET Status = 'Sending',
                AttemptCount = AttemptCount + 1,
                ProcessingLeaseToken = @LeaseToken,
                ProcessingLeaseExpiresAtUtc = @LeaseExpiresAtUtc,
                LastError = NULL,
                UpdatedAtUtc = @Now
            WHERE EventId = @EventId
              AND Template = @Template
              AND Channel = @Channel
              AND (Status IN ('RetryableFailure', 'DeadLetter')
                   OR (Status = 'Sending' AND ProcessingLeaseExpiresAtUtc <= @Now))
            RETURNING Id, AttemptCount;
            """;

        await using var retryCommand = CreateCommand(
            connection, transaction, retrySql, delivery, deliveryId, leaseToken, now);
        await using var reader = await retryCommand.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            var existingId = reader.GetGuid(0);
            var attempt = reader.GetInt32(1);
            await reader.CloseAsync();
            await InsertAttemptAsync(connection, transaction, existingId, attempt, now, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new NotificationDeliveryLeaseAcquisition(NotificationDeliveryStartResult.Started, existingId, leaseToken);
        }

        await reader.CloseAsync();
        const string stateSql = """
            SELECT Status FROM NotificationDeliveries
            WHERE EventId = @EventId AND Template = @Template AND Channel = @Channel;
            """;
        await using var stateCommand = CreateCommand(
            connection, transaction, stateSql, delivery, deliveryId, leaseToken, now);
        var state = (string?)await stateCommand.ExecuteScalarAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new NotificationDeliveryLeaseAcquisition(
            string.Equals(state, "Sent", StringComparison.OrdinalIgnoreCase)
                ? NotificationDeliveryStartResult.AlreadySent
                : NotificationDeliveryStartResult.AlreadyProcessing);
    }

    public Task<bool> MarkSentAsync(Guid deliveryId, Guid leaseToken, CancellationToken cancellationToken = default) =>
        CompleteAsync(deliveryId, leaseToken, "Sent", null, cancellationToken);

    public async Task<int> MarkExhaustedAsDeadLetterAsync(
        int maxAttempts,
        DateTime olderThanUtc,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE NotificationDeliveries
            SET Status = 'DeadLetter',
                ProcessingLeaseToken = NULL,
                ProcessingLeaseExpiresAtUtc = NULL,
                UpdatedAtUtc = @Now
            WHERE Status = 'RetryableFailure'
              AND AttemptCount >= @MaxAttempts
              AND UpdatedAtUtc <= @OlderThanUtc;
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("Now", DateTime.UtcNow);
        command.Parameters.AddWithValue("MaxAttempts", maxAttempts);
        command.Parameters.AddWithValue("OlderThanUtc", olderThanUtc);
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }
    public Task<bool> MarkRetryableFailureAsync(
        Guid deliveryId,
        Guid leaseToken,
        Exception exception,
        CancellationToken cancellationToken = default) =>
        CompleteAsync(deliveryId, leaseToken, "RetryableFailure", exception.Message, cancellationToken);

    private async Task<bool> CompleteAsync(
        Guid deliveryId,
        Guid leaseToken,
        string status,
        string? error,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        const string updateSql = """
            UPDATE NotificationDeliveries
            SET Status = @Status,
                ProcessingLeaseToken = NULL,
                ProcessingLeaseExpiresAtUtc = NULL,
                LastError = @Error,
                UpdatedAtUtc = @Now,
                SentAtUtc = CASE WHEN @Status = 'Sent' THEN @Now ELSE SentAtUtc END
            WHERE Id = @DeliveryId AND ProcessingLeaseToken = @LeaseToken;
            """;
        await using var command = new NpgsqlCommand(updateSql, connection, transaction);
        command.Parameters.AddWithValue("Status", status);
        command.Parameters.AddWithValue("Error", (object?)error ?? DBNull.Value);
        command.Parameters.AddWithValue("Now", now);
        command.Parameters.AddWithValue("DeliveryId", deliveryId);
        command.Parameters.AddWithValue("LeaseToken", leaseToken);
        var changed = await command.ExecuteNonQueryAsync(cancellationToken) == 1;
        if (!changed)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        const string attemptSql = """
            UPDATE NotificationDeliveryAttempts
            SET CompletedAtUtc = @Now, Outcome = @Status, Error = @Error
            WHERE DeliveryId = @DeliveryId
              AND AttemptNumber = (SELECT AttemptCount FROM NotificationDeliveries WHERE Id = @DeliveryId);
            """;
        await using var attemptCommand = new NpgsqlCommand(attemptSql, connection, transaction);
        attemptCommand.Parameters.AddWithValue("Now", now);
        attemptCommand.Parameters.AddWithValue("Status", status);
        attemptCommand.Parameters.AddWithValue("Error", (object?)error ?? DBNull.Value);
        attemptCommand.Parameters.AddWithValue("DeliveryId", deliveryId);
        await attemptCommand.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private static async Task<Guid?> ExecuteScalarAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string sql,
        NotificationDelivery delivery,
        Guid deliveryId,
        Guid leaseToken,
        DateTime now,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(connection, transaction, sql, delivery, deliveryId, leaseToken, now);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is Guid id ? id : null;
    }

    private static async Task InsertAttemptAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid deliveryId,
        int attempt,
        DateTime now,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO NotificationDeliveryAttempts (DeliveryId, AttemptNumber, AttemptedAtUtc, Outcome)
            VALUES (@DeliveryId, @AttemptNumber, @AttemptedAtUtc, 'Sending');
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("DeliveryId", deliveryId);
        command.Parameters.AddWithValue("AttemptNumber", attempt);
        command.Parameters.AddWithValue("AttemptedAtUtc", now);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static NpgsqlCommand CreateCommand(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string sql,
        NotificationDelivery delivery,
        Guid deliveryId,
        Guid leaseToken,
        DateTime now)
    {
        var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("Id", deliveryId);
        command.Parameters.AddWithValue("EventId", delivery.EventId);
        command.Parameters.AddWithValue("EventType", delivery.EventType);
        command.Parameters.AddWithValue("Template", delivery.Template);
        command.Parameters.AddWithValue("Channel", delivery.Channel);
        command.Parameters.AddWithValue("CustomerId", delivery.CustomerId);
        command.Parameters.AddWithValue("OrderId", (object?)delivery.OrderId ?? DBNull.Value);
        command.Parameters.AddWithValue("CorrelationId", (object?)delivery.CorrelationId ?? DBNull.Value);
        command.Parameters.AddWithValue("LeaseToken", leaseToken);
        command.Parameters.AddWithValue("LeaseExpiresAtUtc", now.Add(ProcessingLeaseDuration));
        command.Parameters.AddWithValue("Now", now);
        return command;
    }
}