using IdentityService.Application.Abstractions;
using MediatR;

namespace IdentityService.Application.Auth;

public class LoginHandler : IRequestHandler<LoginCommand, LoginResult?>
{
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<LoginHandler> _logger;

    public LoginHandler(
        IJwtTokenGenerator jwtTokenGenerator,
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        ILogger<LoginHandler> logger)
    {
        _jwtTokenGenerator = jwtTokenGenerator;
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public async Task<LoginResult?> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Password))
        {
            _logger.LogWarning("Audit authentication failure. Reason={Reason}", "MissingCredentials");
            return null;
        }

        var user = await _userRepository.GetByUserNameAsync(request.UserName, cancellationToken);
        if (user is null || !user.IsActive)
        {
            _logger.LogWarning(
                "Audit authentication failure. UserName={UserName}, Reason={Reason}",
                request.UserName.Trim(),
                user is null ? "UnknownUser" : "InactiveUser");
            return null;
        }

        if (!_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            _logger.LogWarning(
                "Audit authentication failure. UserId={UserId}, UserName={UserName}, Reason={Reason}",
                user.Id,
                user.UserName,
                "InvalidPassword");
            return null;
        }

        var token = _jwtTokenGenerator.GenerateToken(user);

        _logger.LogInformation(
            "Audit authentication success. UserId={UserId}, UserName={UserName}, Role={Role}",
            user.Id,
            user.UserName,
            user.Role);

        return new LoginResult(
            token.Token,
            token.ExpiresAt);
    }
}
