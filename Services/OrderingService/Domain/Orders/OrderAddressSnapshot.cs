namespace OrderingService.Domain.Orders;

public sealed record OrderAddressSnapshot(
    Guid AddressId,
    string Label,
    string RecipientName,
    string Line1,
    string? Line2,
    string City,
    string CountryCode,
    string? PostalCode)
{
    public OrderAddressSnapshot Normalize()
    {
        if (AddressId == Guid.Empty) throw new ArgumentException("Address id is required.", nameof(AddressId));
        if (string.IsNullOrWhiteSpace(Label) || Label.Trim().Length > 100) throw new ArgumentException("Address label is required and must not exceed 100 characters.", nameof(Label));
        if (string.IsNullOrWhiteSpace(RecipientName) || RecipientName.Trim().Length > 200) throw new ArgumentException("Recipient name is required and must not exceed 200 characters.", nameof(RecipientName));
        if (string.IsNullOrWhiteSpace(Line1) || Line1.Trim().Length > 300) throw new ArgumentException("Address line 1 is required and must not exceed 300 characters.", nameof(Line1));
        if (Line2?.Trim().Length > 300) throw new ArgumentException("Address line 2 must not exceed 300 characters.", nameof(Line2));
        if (string.IsNullOrWhiteSpace(City) || City.Trim().Length > 100) throw new ArgumentException("City is required and must not exceed 100 characters.", nameof(City));
        var countryCode = CountryCode?.Trim().ToUpperInvariant();
        if (countryCode is null || countryCode.Length != 2 || countryCode.Any(character => !char.IsLetter(character))) throw new ArgumentException("Country code must be ISO 3166-1 alpha-2.", nameof(CountryCode));
        if (PostalCode?.Trim().Length > 32) throw new ArgumentException("Postal code must not exceed 32 characters.", nameof(PostalCode));

        return this with
        {
            Label = Label.Trim(), RecipientName = RecipientName.Trim(), Line1 = Line1.Trim(), Line2 = string.IsNullOrWhiteSpace(Line2) ? null : Line2.Trim(),
            City = City.Trim(), CountryCode = countryCode, PostalCode = string.IsNullOrWhiteSpace(PostalCode) ? null : PostalCode.Trim().ToUpperInvariant()
        };
    }
}
