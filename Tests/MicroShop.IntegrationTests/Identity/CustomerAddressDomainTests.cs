using IdentityService.Domain.Addresses;

namespace MicroShop.IntegrationTests.Identity;

public sealed class CustomerAddressDomainTests
{
    [Fact]
    public void Address_NormalizesSnapshotFields()
    {
        var address = new CustomerAddress(Guid.NewGuid(), Guid.NewGuid(), " Home ", " Ada Lovelace ", " 1 Main St ", " Apt 2 ", " Hanoi ", " vn ", " 10000 ", true, false, DateTime.UtcNow, DateTime.UtcNow);
        Assert.Equal("Home", address.Label);
        Assert.Equal("VN", address.CountryCode);
        Assert.Equal("10000", address.PostalCode);
    }

    [Fact]
    public void Address_RejectsNonIsoCountryCode()
    {
        Assert.Throws<ArgumentException>(() => new CustomerAddress(Guid.NewGuid(), Guid.NewGuid(), "Home", "Ada", "1 Main", null, "Hanoi", "VNM", null, false, false, DateTime.UtcNow, DateTime.UtcNow));
    }
}
