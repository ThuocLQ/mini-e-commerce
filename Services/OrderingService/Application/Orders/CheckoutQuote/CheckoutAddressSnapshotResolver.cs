using OrderingService.Application.Abstractions;
using OrderingService.Domain.Orders;

namespace OrderingService.Application.Orders.CheckoutQuote;

public sealed class CheckoutAddressSnapshotResolver(IAddressSnapshotClient addressSnapshotClient)
{
    public async Task<OrderAddressSnapshot?> ResolveAsync(
        Guid customerId,
        Guid? shippingAddressId,
        CancellationToken cancellationToken)
    {
        if (shippingAddressId is null)
        {
            return null;
        }

        if (shippingAddressId == Guid.Empty)
        {
            throw new ArgumentException("ShippingAddressId cannot be empty.", nameof(shippingAddressId));
        }

        var address = await addressSnapshotClient.GetAddressAsync(customerId, shippingAddressId.Value, cancellationToken);
        if (address is null)
        {
            throw new ArgumentException(
                "Selected shipping address does not exist or does not belong to the authenticated customer.",
                nameof(shippingAddressId));
        }

        return new OrderAddressSnapshot(
            address.AddressId,
            address.Label,
            address.RecipientName,
            address.Line1,
            address.Line2,
            address.City,
            address.CountryCode,
            address.PostalCode).Normalize();
    }
}
