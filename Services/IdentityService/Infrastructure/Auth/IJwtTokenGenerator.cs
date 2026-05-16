using IdentityService.Domain.Users;

namespace IdentityService.Infrastructure.Auth;

public interface IJwtTokenGenerator
{
    (string Token, DateTime ExpiresAt) GenerateToken(AppUser user);
}