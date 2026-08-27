using Dapper;
using IdentityService.Application.Abstractions;
using IdentityService.Domain.Addresses;

namespace IdentityService.Infrastructure.Persistence;

public sealed class DapperAddressRepository(IDbConnectionFactory connectionFactory) : IAddressRepository
{
    public async Task<IReadOnlyList<CustomerAddress>> GetByCustomerAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<AddressRow>(new CommandDefinition("SELECT * FROM CustomerAddresses WHERE CustomerId = @CustomerId ORDER BY IsArchived, IsDefault DESC, CreatedAtUtc, Id;", new { CustomerId = customerId }, cancellationToken: cancellationToken));
        return rows.Select(Map).ToList();
    }
    public async Task<CustomerAddress?> GetByIdAsync(Guid customerId, Guid addressId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<AddressRow>(new CommandDefinition("SELECT * FROM CustomerAddresses WHERE CustomerId = @CustomerId AND Id = @AddressId;", new { CustomerId = customerId, AddressId = addressId }, cancellationToken: cancellationToken));
        return row is null ? null : Map(row);
    }
    public async Task<CustomerAddress?> GetByCreateKeyAsync(Guid customerId, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<AddressRow>(new CommandDefinition("SELECT * FROM CustomerAddresses WHERE CustomerId = @CustomerId AND CreateIdempotencyKey = @IdempotencyKey;", new { CustomerId = customerId, IdempotencyKey = idempotencyKey }, cancellationToken: cancellationToken));
        return row is null ? null : Map(row);
    }
    public async Task<bool> CreateAsync(CustomerAddress address, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection(); connection.Open(); using var tx = connection.BeginTransaction();
        if (address.IsDefault)
        {
            await LockCustomerAsync(connection, tx, address.CustomerId, cancellationToken);
            await connection.ExecuteAsync(new CommandDefinition("UPDATE CustomerAddresses SET IsDefault = false, UpdatedAtUtc = @Now WHERE CustomerId = @CustomerId AND IsDefault AND NOT IsArchived;", new { address.CustomerId, Now = address.UpdatedAtUtc }, tx, cancellationToken: cancellationToken));
        }
        var affected = await connection.ExecuteAsync(new CommandDefinition("""INSERT INTO CustomerAddresses (Id, CustomerId, Label, RecipientName, Line1, Line2, City, CountryCode, PostalCode, IsDefault, IsArchived, CreatedAtUtc, UpdatedAtUtc, CreateIdempotencyKey, CreateRequestHash) VALUES (@Id,@CustomerId,@Label,@RecipientName,@Line1,@Line2,@City,@CountryCode,@PostalCode,@IsDefault,@IsArchived,@CreatedAtUtc,@UpdatedAtUtc,@CreateIdempotencyKey,@CreateRequestHash) ON CONFLICT (CustomerId, CreateIdempotencyKey) WHERE CreateIdempotencyKey IS NOT NULL DO NOTHING;""", address, tx, cancellationToken: cancellationToken));
        tx.Commit(); return affected == 1;
    }
    public async Task<bool> UpdateAsync(CustomerAddress address, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition("UPDATE CustomerAddresses SET Label=@Label, RecipientName=@RecipientName, Line1=@Line1, Line2=@Line2, City=@City, CountryCode=@CountryCode, PostalCode=@PostalCode, UpdatedAtUtc=@UpdatedAtUtc WHERE Id=@Id AND CustomerId=@CustomerId AND NOT IsArchived;", address, cancellationToken: cancellationToken)) == 1;
    }
    public async Task<bool> ArchiveAsync(Guid customerId, Guid addressId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition("UPDATE CustomerAddresses SET IsArchived=true, IsDefault=false, UpdatedAtUtc=CURRENT_TIMESTAMP WHERE Id=@AddressId AND CustomerId=@CustomerId AND NOT IsArchived;", new { CustomerId = customerId, AddressId = addressId }, cancellationToken: cancellationToken)) == 1;
    }
    public async Task<bool> SetDefaultAsync(Guid customerId, Guid addressId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection(); connection.Open(); using var tx = connection.BeginTransaction();
        await LockCustomerAsync(connection, tx, customerId, cancellationToken);
        var exists = await connection.ExecuteScalarAsync<int?>(new CommandDefinition("SELECT 1 FROM CustomerAddresses WHERE CustomerId=@CustomerId AND Id=@AddressId AND NOT IsArchived FOR UPDATE;", new { CustomerId = customerId, AddressId = addressId }, tx, cancellationToken: cancellationToken));
        if (exists is null) { tx.Rollback(); return false; }
        await connection.ExecuteAsync(new CommandDefinition("UPDATE CustomerAddresses SET IsDefault=false, UpdatedAtUtc=CURRENT_TIMESTAMP WHERE CustomerId=@CustomerId AND IsDefault; UPDATE CustomerAddresses SET IsDefault=true, UpdatedAtUtc=CURRENT_TIMESTAMP WHERE CustomerId=@CustomerId AND Id=@AddressId;", new { CustomerId = customerId, AddressId = addressId }, tx, cancellationToken: cancellationToken)); tx.Commit(); return true;
    }
    private static Task<int> LockCustomerAsync(System.Data.IDbConnection connection, System.Data.IDbTransaction transaction, Guid customerId, CancellationToken cancellationToken) => connection.ExecuteAsync(new CommandDefinition("SELECT pg_advisory_xact_lock(hashtextextended(CAST(@CustomerId AS text), 0));", new { CustomerId = customerId }, transaction, cancellationToken: cancellationToken));
    private static CustomerAddress Map(AddressRow r) => new(r.Id,r.CustomerId,r.Label,r.RecipientName,r.Line1,r.Line2,r.City,r.CountryCode,r.PostalCode,r.IsDefault,r.IsArchived,r.CreatedAtUtc,r.UpdatedAtUtc,r.CreateIdempotencyKey,r.CreateRequestHash);
    private sealed record AddressRow(Guid Id, Guid CustomerId, string Label, string RecipientName, string Line1, string? Line2, string City, string CountryCode, string? PostalCode, bool IsDefault, bool IsArchived, DateTime CreatedAtUtc, DateTime UpdatedAtUtc, string? CreateIdempotencyKey, string? CreateRequestHash);
}
