namespace OrderingService.Application.Addresses;

public sealed class AddressUnavailableException(Exception innerException)
    : Exception("IdentityService is unavailable. Checkout cannot validate the selected address.", innerException);
