using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace Microsoft.Extensions.Hosting;

public static class MicroShopJwtAuthenticationExtensions
{
    public static IServiceCollection AddMicroShopJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var issuer = configuration["Jwt:Issuer"];
        var audience = configuration["Jwt:Audience"];
        var secretKey = configuration["Jwt:SecretKey"];

        ValidateConfiguration(issuer, audience, secretKey, environment);

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    ValidateAudience = true,
                    ValidAudience = audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey!)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1)
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy("authenticated", policy => policy.RequireAuthenticatedUser());
            options.AddPolicy("administrator", policy => policy.RequireRole("Admin"));
        });
        services.AddTransient<AccessTokenDelegatingHandler>();

        return services;
    }

    private static void ValidateConfiguration(
        string? issuer,
        string? audience,
        string? secretKey,
        IHostEnvironment environment)
    {
        if (string.IsNullOrWhiteSpace(issuer) ||
            string.IsNullOrWhiteSpace(audience) ||
            string.IsNullOrWhiteSpace(secretKey) ||
            secretKey.Length < 32)
        {
            throw new InvalidOperationException("Jwt:Issuer, Jwt:Audience, and a Jwt:SecretKey of at least 32 characters are required.");
        }

        if (!environment.IsDevelopment() &&
            (secretKey.StartsWith("SET_BY_", StringComparison.OrdinalIgnoreCase) ||
             secretKey.Contains("CHANGEME", StringComparison.OrdinalIgnoreCase) ||
             secretKey.Contains("development-secret", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("Jwt:SecretKey must be supplied from a non-development secret source outside Development.");
        }
    }
}
