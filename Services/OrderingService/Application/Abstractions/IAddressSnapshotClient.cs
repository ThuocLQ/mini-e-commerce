namespace OrderingService.Application.Abstractions;

public interface IAddressSnapshotClient
{
    Task<CustomerAddressSnapshot?> GetAddressAsync(
        Guid customerId,
        Guid addressId,
        CancellationToken cancellationToken = default);
}

public sealed record CustomerAddressSnapshot(
    Guid AddressId,
    string Label,
    string RecipientName,
    string Line1,
    string? Line2,
    string City,
    string CountryCode,
    string? PostalCode);
