using IdentityService.Domain.Users;

namespace IdentityService.Application.Abstractions;

public interface IUserRepository
{
    Task<AppUser?> GetByUserNameAsync(string userName, CancellationToken cancellationToken = default);
}
