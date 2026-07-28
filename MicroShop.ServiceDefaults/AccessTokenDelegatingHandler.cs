using Microsoft.AspNetCore.Http;

namespace Microsoft.Extensions.Hosting;

public sealed class AccessTokenDelegatingHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AccessTokenDelegatingHandler(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var authorization = _httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();

        if (!string.IsNullOrWhiteSpace(authorization) && !request.Headers.Contains("Authorization"))
        {
            request.Headers.TryAddWithoutValidation("Authorization", authorization);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
