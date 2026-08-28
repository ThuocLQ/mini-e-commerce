using System.Net.Mail;
using System.Security.Cryptography;
using System.Text.Json;
using BuildingBlocks.Contracts.Events.Identity;
using IdentityService.Application.Abstractions;
using IdentityService.Domain.Users;
using MediatR;
using BuildingBlocks.Contracts.Correlation;

namespace IdentityService.Application.Auth;

public sealed class RegisterHandler : IRequestHandler<RegisterCommand, RegisterResult>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<RegisterHandler> _logger;

    public RegisterHandler(IUserRepository userRepository, IPasswordHasher passwordHasher, ILogger<RegisterHandler> logger)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public async Task<RegisterResult> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var userName = request.UserName?.Trim() ?? string.Empty;
        var email = NormalizeEmail(request.Email, nameof(request.Email));
        if (userName.Length is < 3 or > 100) throw new ArgumentException("Username must contain between 3 and 100 characters.", nameof(request.UserName));
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 14) throw new ArgumentException("Password must contain at least 14 characters.", nameof(request.Password));

        if (await _userRepository.GetByUserNameAsync(userName, cancellationToken) is not null) throw new UserNameAlreadyExistsException(userName);
        if (await _userRepository.GetByEmailAsync(email, cancellationToken) is not null) throw new EmailAlreadyExistsException(email);

        var user = new AppUser(Guid.NewGuid(), userName, _passwordHasher.Hash(request.Password), "Customer", true, email);
        var token = CreateToken();
        var expiresAtUtc = DateTime.UtcNow.AddMinutes(30);
        var verificationEvent = new CustomerEmailVerificationRequestedIntegrationEvent
        {
            CustomerId = user.Id,
            Token = token,
            ExpiresAtUtc = expiresAtUtc,
            CorrelationId = CorrelationContext.CorrelationId
        };

        var created = await _userRepository.CreateWithEmailVerificationAsync(
            user,
            SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)),
            expiresAtUtc,
            JsonSerializer.Serialize(verificationEvent, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            verificationEvent.CorrelationId,
            cancellationToken);

        if (!created)
        {
            if (await _userRepository.GetByEmailAsync(email, cancellationToken) is not null) throw new EmailAlreadyExistsException(email);
            throw new UserNameAlreadyExistsException(userName);
        }

        _logger.LogInformation("Registered unverified customer account and stored verification request. UserId={UserId}, UserName={UserName}", user.Id, user.UserName);
        return new RegisterResult(user.Id, user.UserName, email);
    }

    private static string CreateToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string NormalizeEmail(string? value, string parameterName)
    {
        var email = value?.Trim() ?? string.Empty;
        if (email.Length is < 3 or > 320) throw new ArgumentException("Email must contain between 3 and 320 characters.", parameterName);

        try
        {
            var parsed = new MailAddress(email);
            if (!string.Equals(parsed.Address, email, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Email is invalid.", parameterName);
        }
        catch (FormatException) { throw new ArgumentException("Email is invalid.", parameterName); }

        return email;
    }
}