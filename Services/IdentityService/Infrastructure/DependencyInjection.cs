using System.Text;
using IdentityService.Application.Abstractions;
using IdentityService.Infrastructure.Auth;
using IdentityService.Infrastructure.Bootstrap;
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
        var bootstrapSection = configuration.GetSection(BootstrapAdminOptions.SectionName);
        var bootstrapOptions = bootstrapSection.Get<BootstrapAdminOptions>() ?? new BootstrapAdminOptions();
        ValidateBootstrapAdminOptions(bootstrapOptions);

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

        services
            .AddOptions<BootstrapAdminOptions>()
            .Bind(bootstrapSection)
            .Validate(
                IsBootstrapAdminConfigurationValid,
                "BootstrapAdmin configuration is invalid.")
            .ValidateOnStart();

        services.AddSingleton<IDbConnectionFactory, NpgsqlConnectionFactory>();
        services.AddSingleton<IDatabaseInitializer, PostgresDatabaseInitializer>();
        services.AddScoped<IUserRepository, DapperUserRepository>();
        services.AddScoped<IAddressRepository, DapperAddressRepository>();
        services.AddScoped<IdentityService.Application.Addresses.AddressService>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddScoped<IAdminBootstrapper, AdminBootstrapper>();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddPostgresReadinessCheck(configuration, "IdentityDb");

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

    private static void ValidateBootstrapAdminOptions(BootstrapAdminOptions options)
    {
        if (!IsBootstrapAdminConfigurationValid(options))
        {
            throw new InvalidOperationException(
                "BootstrapAdmin requires a username between 3 and 100 characters and a password of at least 14 characters.");
        }
    }

    private static bool IsBootstrapAdminConfigurationValid(BootstrapAdminOptions options)
    {
        if (!options.Enabled)
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(options.UserName)
               && options.UserName.Trim().Length is >= 3 and <= 100
               && !string.IsNullOrWhiteSpace(options.Password)
               && options.Password.Length >= 14
               && !options.Password.Contains("CHANGEME", StringComparison.OrdinalIgnoreCase);
    }
}
