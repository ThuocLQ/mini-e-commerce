namespace IdentityService.Domain.Addresses;

public sealed class CustomerAddress
{
    public Guid Id { get; }
    public Guid CustomerId { get; }
    public string Label { get; private set; } = string.Empty;
    public string RecipientName { get; private set; } = string.Empty;
    public string Line1 { get; private set; } = string.Empty;
    public string? Line2 { get; private set; }
    public string City { get; private set; } = string.Empty;
    public string CountryCode { get; private set; } = string.Empty;
    public string? PostalCode { get; private set; }
    public bool IsDefault { get; internal set; }
    public bool IsArchived { get; internal set; }
    public DateTime CreatedAtUtc { get; }
    public DateTime UpdatedAtUtc { get; internal set; }
    public string? CreateIdempotencyKey { get; }
    public string? CreateRequestHash { get; }

    public CustomerAddress(Guid id, Guid customerId, string label, string recipientName, string line1, string? line2, string city, string countryCode, string? postalCode, bool isDefault, bool isArchived, DateTime createdAtUtc, DateTime updatedAtUtc, string? createIdempotencyKey = null, string? createRequestHash = null)
    {
        if (id == Guid.Empty || customerId == Guid.Empty) throw new ArgumentException("Address and customer ids are required.");
        Id = id; CustomerId = customerId; CreatedAtUtc = createdAtUtc; UpdatedAtUtc = updatedAtUtc;
        CreateIdempotencyKey = createIdempotencyKey; CreateRequestHash = createRequestHash;
        IsDefault = isDefault; IsArchived = isArchived;
        Update(label, recipientName, line1, line2, city, countryCode, postalCode, updatedAtUtc);
        if (IsArchived) IsDefault = false;
    }

    public void Update(string label, string recipientName, string line1, string? line2, string city, string countryCode, string? postalCode, DateTime updatedAtUtc)
    {
        Label = Required(label, nameof(label), 80); RecipientName = Required(recipientName, nameof(recipientName), 120);
        Line1 = Required(line1, nameof(line1), 200); Line2 = Optional(line2, 200);
        City = Required(city, nameof(city), 120); CountryCode = Required(countryCode, nameof(countryCode), 2).ToUpperInvariant();
        if (CountryCode.Any(character => character is < 'A' or > 'Z')) throw new ArgumentException("CountryCode must use ISO alpha-2 letters.", nameof(countryCode));
        PostalCode = Optional(postalCode, 20)?.ToUpperInvariant(); UpdatedAtUtc = updatedAtUtc;
    }

    private static string Required(string value, string name, int max) => string.IsNullOrWhiteSpace(value) || value.Trim().Length > max ? throw new ArgumentException($"{name} is required and cannot exceed {max} characters.", name) : value.Trim();
    private static string? Optional(string? value, int max) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().Length > max ? throw new ArgumentException($"Value cannot exceed {max} characters.", nameof(value)) : value.Trim();
}
