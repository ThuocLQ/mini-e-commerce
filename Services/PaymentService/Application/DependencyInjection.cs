namespace PaymentService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddMediatR(configuration =>
            configuration.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));
        services.AddScoped<Payments.Webhooks.IPaymentWebhookProcessor, Payments.Webhooks.PaymentWebhookProcessor>();

        services
            .AddOptions<Payments.Providers.PaymentProviderOptions>()
            .Bind(configuration.GetSection(Payments.Providers.PaymentProviderOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Provider), "PaymentProvider:Provider is required.")
            .Validate(options => options.SandboxActionExpiryMinutes is > 0 and <= 24 * 60,
                "PaymentProvider:SandboxActionExpiryMinutes must be between 1 and 1440.")
            .ValidateOnStart();

        services
            .AddOptions<Payments.Webhooks.PaymentWebhookOptions>()
            .Bind(configuration.GetSection(Payments.Webhooks.PaymentWebhookOptions.SectionName))
            .Validate(options => !options.RequireSignature || !string.IsNullOrWhiteSpace(options.SharedSecret),
                "PaymentWebhooks:SharedSecret is required when signature verification is enabled.")
            .ValidateOnStart();

        return services;
    }
}
