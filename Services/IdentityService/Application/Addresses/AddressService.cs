using System.Security.Cryptography;
using System.Text;
using IdentityService.Application.Abstractions;
using IdentityService.Domain.Addresses;

namespace IdentityService.Application.Addresses;

public sealed class AddressService(IAddressRepository repository)
{
    public async Task<IReadOnlyList<CustomerAddress>> GetAsync(Guid customerId, CancellationToken cancellationToken) => await repository.GetByCustomerAsync(customerId, cancellationToken);
    public Task<CustomerAddress?> GetAsync(Guid customerId, Guid addressId, CancellationToken cancellationToken) => repository.GetByIdAsync(customerId, addressId, cancellationToken);

    public async Task<CustomerAddress> CreateAsync(Guid customerId, AddressInput input, string? idempotencyKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey)) throw new ArgumentException("Idempotency-Key header is required.", nameof(idempotencyKey));
        var key = idempotencyKey.Trim(); if (key.Length > 128) throw new ArgumentException("Idempotency-Key cannot exceed 128 characters.", nameof(idempotencyKey));
        var hash = Hash(input);
        var existing = await repository.GetByCreateKeyAsync(customerId, key, cancellationToken);
        if (existing is not null) return existing.CreateRequestHash == hash ? existing : throw new InvalidOperationException("Idempotency key was already used for a different address.");
        var address = new CustomerAddress(Guid.NewGuid(), customerId, input.Label, input.RecipientName, input.Line1, input.Line2, input.City, input.CountryCode, input.PostalCode, input.MakeDefault, false, DateTime.UtcNow, DateTime.UtcNow, key, hash);
        if (await repository.CreateAsync(address, cancellationToken)) return address;
        existing = await repository.GetByCreateKeyAsync(customerId, key, cancellationToken) ?? throw new InvalidOperationException("Address creation conflicted without a stored request.");
        return existing.CreateRequestHash == hash ? existing : throw new InvalidOperationException("Idempotency key was already used for a different address.");
    }

    public async Task<CustomerAddress?> UpdateAsync(Guid customerId, Guid addressId, AddressInput input, CancellationToken cancellationToken)
    {
        var address = await repository.GetByIdAsync(customerId, addressId, cancellationToken); if (address is null || address.IsArchived) return null;
        address.Update(input.Label, input.RecipientName, input.Line1, input.Line2, input.City, input.CountryCode, input.PostalCode, DateTime.UtcNow);
        if (!await repository.UpdateAsync(address, cancellationToken)) return null;
        if (!input.MakeDefault) return address;

        if (!await repository.SetDefaultAsync(customerId, addressId, cancellationToken)) return null;
        return await repository.GetByIdAsync(customerId, addressId, cancellationToken);
    }

    public Task<bool> ArchiveAsync(Guid customerId, Guid addressId, CancellationToken cancellationToken) => repository.ArchiveAsync(customerId, addressId, cancellationToken);
    public Task<bool> SetDefaultAsync(Guid customerId, Guid addressId, CancellationToken cancellationToken) => repository.SetDefaultAsync(customerId, addressId, cancellationToken);

    private static string Hash(AddressInput input) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{input.Label.Trim()}|{input.RecipientName.Trim()}|{input.Line1.Trim()}|{input.Line2?.Trim()}|{input.City.Trim()}|{input.CountryCode.Trim().ToUpperInvariant()}|{input.PostalCode?.Trim().ToUpperInvariant()}|{input.MakeDefault}"))).ToLowerInvariant();
}

public sealed record AddressInput(string Label, string RecipientName, string Line1, string? Line2, string City, string CountryCode, string? PostalCode, bool MakeDefault);
