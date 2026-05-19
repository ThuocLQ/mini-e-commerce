using IdentityService.Domain.Users;

namespace IdentityService.Application.Abstractions;

public interface IJwtTokenGenerator
{
    (string Token, DateTime ExpiresAt) GenerateToken(AppUser user);
}
