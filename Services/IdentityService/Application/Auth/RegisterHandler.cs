using IdentityService.Application.Abstractions;
using IdentityService.Domain.Users;
using MediatR;

namespace IdentityService.Application.Auth;

public sealed class RegisterHandler : IRequestHandler<RegisterCommand, RegisterResult>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<RegisterHandler> _logger;

    public RegisterHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        ILogger<RegisterHandler> logger)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public async Task<RegisterResult> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var userName = request.UserName?.Trim() ?? string.Empty;
        if (userName.Length is < 3 or > 100)
        {
            throw new ArgumentException("Username must contain between 3 and 100 characters.", nameof(request.UserName));
        }

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 14)
        {
            throw new ArgumentException("Password must contain at least 14 characters.", nameof(request.Password));
        }

        var user = new AppUser(
            Guid.NewGuid(),
            userName,
            _passwordHasher.Hash(request.Password),
            "Customer",
            true);

        if (!await _userRepository.CreateAsync(user, cancellationToken))
        {
            throw new UserNameAlreadyExistsException(userName);
        }

        _logger.LogInformation("Registered customer account. UserId={UserId}, UserName={UserName}", user.Id, user.UserName);
        return new RegisterResult(user.Id, user.UserName);
    }
}
