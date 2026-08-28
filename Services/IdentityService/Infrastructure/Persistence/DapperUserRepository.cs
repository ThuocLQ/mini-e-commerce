using System.Data;
using Dapper;
using IdentityService.Application.Abstractions;
using IdentityService.Application.Auth;
using IdentityService.Domain.Users;

namespace IdentityService.Infrastructure.Persistence;

public sealed class DapperUserRepository : IUserRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public DapperUserRepository(IDbConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public Task<AppUser?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        GetSingleAsync("Id = @UserId", new { UserId = userId }, cancellationToken);

    public Task<AppUser?> GetByUserNameAsync(string userName, CancellationToken cancellationToken = default) =>
        string.IsNullOrWhiteSpace(userName)
            ? Task.FromResult<AppUser?>(null)
            : GetSingleAsync("NormalizedUserName = @NormalizedUserName", new { NormalizedUserName = userName.Trim().ToUpperInvariant() }, cancellationToken);

    public Task<AppUser?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        string.IsNullOrWhiteSpace(email)
            ? Task.FromResult<AppUser?>(null)
            : GetSingleAsync("NormalizedEmail = @NormalizedEmail", new { NormalizedEmail = email.Trim().ToUpperInvariant() }, cancellationToken);

    public async Task<bool> CreateAsync(AppUser user, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await InsertUserAsync(connection, null, user, cancellationToken) == 1;
    }

    public async Task<bool> CreateWithEmailVerificationAsync(AppUser user, byte[] tokenHash, DateTime expiresAtUtc, string eventContent, string? correlationId, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            if (await InsertUserAsync(connection, transaction, user, cancellationToken) != 1)
            {
                transaction.Rollback();
                return false;
            }

            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO EmailVerificationTokens (Id, UserId, TokenHash, CreatedAtUtc, ExpiresAtUtc)
                VALUES (@Id, @UserId, @TokenHash, CURRENT_TIMESTAMP, @ExpiresAtUtc);
                """, new { Id = Guid.NewGuid(), UserId = user.Id, TokenHash = tokenHash, ExpiresAtUtc = expiresAtUtc }, transaction, cancellationToken: cancellationToken));

            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO IdentityOutboxMessages (Id, OccurredAtUtc, Type, Content, CorrelationId, NextAttemptAtUtc)
                VALUES (@Id, CURRENT_TIMESTAMP, @Type, CAST(@Content AS jsonb), @CorrelationId, CURRENT_TIMESTAMP);
                """, new { Id = Guid.NewGuid(), Type = "CustomerEmailVerificationRequestedIntegrationEvent", Content = eventContent, CorrelationId = correlationId }, transaction, cancellationToken: cancellationToken));

            transaction.Commit();
            return true;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<EmailVerificationIssueResult> IssueEmailVerificationAsync(
        Guid userId,
        byte[] tokenHash,
        DateTime expiresAtUtc,
        string eventContent,
        string? correlationId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        var account = await connection.QuerySingleOrDefaultAsync<VerificationAccountRow>(new CommandDefinition("""
            SELECT IsActive, IsEmailVerified
            FROM Users
            WHERE Id = @UserId
            FOR UPDATE;
            """, new { UserId = userId }, transaction, cancellationToken: cancellationToken));

        if (account is null || !account.IsActive || account.IsEmailVerified)
        {
            transaction.Rollback();
            return account?.IsEmailVerified == true ? EmailVerificationIssueResult.AlreadyVerified : EmailVerificationIssueResult.NotEligible;
        }

        var lastRequestAtUtc = await connection.QuerySingleOrDefaultAsync<DateTime?>(new CommandDefinition("""
            SELECT MAX(CreatedAtUtc)
            FROM EmailVerificationTokens
            WHERE UserId = @UserId;
            """, new { UserId = userId }, transaction, cancellationToken: cancellationToken));

        if (lastRequestAtUtc is not null && lastRequestAtUtc > nowUtc.AddMinutes(-1))
        {
            transaction.Rollback();
            return EmailVerificationIssueResult.RateLimited;
        }

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE EmailVerificationTokens
            SET ConsumedAtUtc = @NowUtc
            WHERE UserId = @UserId
              AND ConsumedAtUtc IS NULL;
            """, new { UserId = userId, NowUtc = nowUtc }, transaction, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO EmailVerificationTokens (Id, UserId, TokenHash, CreatedAtUtc, ExpiresAtUtc)
            VALUES (@Id, @UserId, @TokenHash, @NowUtc, @ExpiresAtUtc);
            """, new { Id = Guid.NewGuid(), UserId = userId, TokenHash = tokenHash, NowUtc = nowUtc, ExpiresAtUtc = expiresAtUtc }, transaction, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO IdentityOutboxMessages (Id, OccurredAtUtc, Type, Content, CorrelationId, NextAttemptAtUtc)
            VALUES (@Id, @NowUtc, @Type, CAST(@Content AS jsonb), @CorrelationId, @NowUtc);
            """, new { Id = Guid.NewGuid(), NowUtc = nowUtc, Type = "CustomerEmailVerificationRequestedIntegrationEvent", Content = eventContent, CorrelationId = correlationId }, transaction, cancellationToken: cancellationToken));

        transaction.Commit();
        return EmailVerificationIssueResult.Issued;
    }

    public async Task<bool> VerifyEmailAsync(byte[] tokenHash, DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        var token = await connection.QuerySingleOrDefaultAsync<VerificationTokenRow>(new CommandDefinition("""
            SELECT Id, UserId
            FROM EmailVerificationTokens
            WHERE TokenHash = @TokenHash AND ConsumedAtUtc IS NULL AND ExpiresAtUtc > @NowUtc
            FOR UPDATE;
            """, new { TokenHash = tokenHash, NowUtc = nowUtc }, transaction, cancellationToken: cancellationToken));

        if (token is null)
        {
            transaction.Rollback();
            return false;
        }

        await connection.ExecuteAsync(new CommandDefinition("UPDATE EmailVerificationTokens SET ConsumedAtUtc = @NowUtc WHERE Id = @Id;", new { token.Id, NowUtc = nowUtc }, transaction, cancellationToken: cancellationToken));
        await connection.ExecuteAsync(new CommandDefinition("UPDATE Users SET IsEmailVerified = TRUE, EmailVerifiedAtUtc = @NowUtc WHERE Id = @UserId;", new { token.UserId, NowUtc = nowUtc }, transaction, cancellationToken: cancellationToken));
        transaction.Commit();
        return true;
    }

    private static Task<int> InsertUserAsync(IDbConnection connection, IDbTransaction? transaction, AppUser user, CancellationToken cancellationToken) =>
        connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO Users (Id, UserName, NormalizedUserName, PasswordHash, Role, IsActive, Email, NormalizedEmail, IsEmailVerified)
            VALUES (@Id, @UserName, @NormalizedUserName, @PasswordHash, @Role, @IsActive, @Email, @NormalizedEmail, @IsEmailVerified)
            ON CONFLICT DO NOTHING;
            """, new { user.Id, user.UserName, NormalizedUserName = user.UserName.ToUpperInvariant(), user.PasswordHash, user.Role, user.IsActive, user.Email, NormalizedEmail = user.Email?.ToUpperInvariant(), user.IsEmailVerified }, transaction, cancellationToken: cancellationToken));

    private async Task<AppUser?> GetSingleAsync(string predicate, object parameters, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<UserRow>(new CommandDefinition($"""
            SELECT Id, UserName, PasswordHash, Role, IsActive, Email, IsEmailVerified
            FROM Users WHERE {predicate};
            """, parameters, cancellationToken: cancellationToken));
        return row is null ? null : new AppUser(row.Id, row.UserName, row.PasswordHash, row.Role, row.IsActive, row.Email, row.IsEmailVerified);
    }

    private sealed record UserRow(Guid Id, string UserName, string PasswordHash, string Role, bool IsActive, string? Email, bool IsEmailVerified);
    private sealed record VerificationTokenRow(Guid Id, Guid UserId);
    private sealed record VerificationAccountRow(bool IsActive, bool IsEmailVerified);
}