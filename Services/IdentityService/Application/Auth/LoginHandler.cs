using IdentityService.Domain.Users;
using IdentityService.Infrastructure.Auth;
using MediatR;

namespace IdentityService.Application.Auth;

public class LoginHandler : IRequestHandler<LoginCommand, LoginResult?>
{
    private readonly IJwtTokenGenerator _jwtTokenGenerator;

    public LoginHandler(IJwtTokenGenerator jwtTokenGenerator)
    {
        _jwtTokenGenerator = jwtTokenGenerator;
    }

    public Task<LoginResult?> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        // Demo only: hardcode user for learning JWT basics.
        // Production must use database + password hashing.
        if (request.UserName != "admin" || request.Password != "Admin@123")
        {
            return Task.FromResult<LoginResult?>(null);
        }

        var user = new AppUser(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "admin",
            "Admin");

        var token = _jwtTokenGenerator.GenerateToken(user);

        var result = new LoginResult(
            token.Token,
            token.ExpiresAt);

        return Task.FromResult<LoginResult?>(result);
    }
}
