using IdentityService.Application.Abstractions;
using MediatR;

namespace IdentityService.Application.Auth;

public class LoginHandler : IRequestHandler<LoginCommand, LoginResult?>
{
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;

    public LoginHandler(
        IJwtTokenGenerator jwtTokenGenerator,
        IUserRepository userRepository,
        IPasswordHasher passwordHasher)
    {
        _jwtTokenGenerator = jwtTokenGenerator;
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
    }

    public async Task<LoginResult?> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Password))
        {
            return null;
        }

        var user = await _userRepository.GetByUserNameAsync(request.UserName, cancellationToken);
        if (user is null || !user.IsActive)
        {
            return null;
        }

        if (!_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            return null;
        }

        var token = _jwtTokenGenerator.GenerateToken(user);

        return new LoginResult(
            token.Token,
            token.ExpiresAt);
    }
}
