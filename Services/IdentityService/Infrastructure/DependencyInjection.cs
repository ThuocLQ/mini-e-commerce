using System.Text;
using IdentityService.Application.Abstractions;
using IdentityService.Infrastructure.Auth;
using IdentityService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace IdentityService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwtSection = configuration.GetRequiredSection(JwtOptions.SectionName);
        var jwtOptions = jwtSection.Get<JwtOptions>()
                         ?? throw new InvalidOperationException("Jwt configuration is missing.");

        ValidateJwtOptions(jwtOptions);

        services
            .AddOptions<JwtOptions>()
            .Bind(jwtSection)
            .Validate(options =>
            {
                try
                {
                    ValidateJwtOptions(options);
                    return true;
                }
                catch (InvalidOperationException)
                {
                    return false;
                }
            }, "Jwt configuration is invalid.")
            .ValidateOnStart();

        services.AddSingleton<IDbConnectionFactory, SqliteConnectionFactory>();
        services.AddSingleton<IDatabaseInitializer, SqliteDatabaseInitializer>();
        services.AddScoped<IUserRepository, DapperUserRepository>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,

                    ValidateAudience = true,
                    ValidAudience = jwtOptions.Audience,

                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecretKey)),

                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };
            });

        return services;
    }

    private static void ValidateJwtOptions(JwtOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Issuer))
        {
            throw new InvalidOperationException("Jwt:Issuer is missing.");
        }

        if (string.IsNullOrWhiteSpace(options.Audience))
        {
            throw new InvalidOperationException("Jwt:Audience is missing.");
        }

        if (string.IsNullOrWhiteSpace(options.SecretKey))
        {
            throw new InvalidOperationException("Jwt:SecretKey is missing.");
        }

        if (Encoding.UTF8.GetByteCount(options.SecretKey) < 32)
        {
            throw new InvalidOperationException("Jwt:SecretKey must be at least 32 bytes.");
        }

        if (options.ExpirationMinutes <= 0)
        {
            throw new InvalidOperationException("Jwt:ExpirationMinutes must be greater than 0.");
        }
    }
}
