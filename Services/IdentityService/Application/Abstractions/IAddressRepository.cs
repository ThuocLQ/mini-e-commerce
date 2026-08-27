using IdentityService.Domain.Addresses;

namespace IdentityService.Application.Abstractions;

public interface IAddressRepository
{
    Task<IReadOnlyList<CustomerAddress>> GetByCustomerAsync(Guid customerId, CancellationToken cancellationToken = default);
    Task<CustomerAddress?> GetByIdAsync(Guid customerId, Guid addressId, CancellationToken cancellationToken = default);
    Task<CustomerAddress?> GetByCreateKeyAsync(Guid customerId, string idempotencyKey, CancellationToken cancellationToken = default);
    Task<bool> CreateAsync(CustomerAddress address, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(CustomerAddress address, CancellationToken cancellationToken = default);
    Task<bool> ArchiveAsync(Guid customerId, Guid addressId, CancellationToken cancellationToken = default);
    Task<bool> SetDefaultAsync(Guid customerId, Guid addressId, CancellationToken cancellationToken = default);
}
