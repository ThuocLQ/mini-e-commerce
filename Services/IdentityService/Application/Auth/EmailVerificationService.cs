using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BuildingBlocks.Contracts.Correlation;
using BuildingBlocks.Contracts.Events.Identity;
using IdentityService.Application.Abstractions;

namespace IdentityService.Application.Auth;

public sealed class EmailVerificationService
{
    private readonly IUserRepository _users;

    public EmailVerificationService(IUserRepository users)
    {
        _users = users;
    }

    public async Task<EmailVerificationIssueResult> ResendAsync(Guid userId, CancellationToken cancellationToken)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var expiresAtUtc = DateTime.UtcNow.AddMinutes(30);
        var integrationEvent = new CustomerEmailVerificationRequestedIntegrationEvent
        {
            CustomerId = userId,
            Token = token,
            ExpiresAtUtc = expiresAtUtc,
            CorrelationId = CorrelationContext.CorrelationId
        };

        return await _users.IssueEmailVerificationAsync(
            userId,
            SHA256.HashData(Encoding.UTF8.GetBytes(token)),
            expiresAtUtc,
            JsonSerializer.Serialize(integrationEvent, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            integrationEvent.CorrelationId,
            DateTime.UtcNow,
            cancellationToken);
    }
}
