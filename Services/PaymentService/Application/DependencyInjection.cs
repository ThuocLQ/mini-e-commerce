namespace PaymentService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddMediatR(configuration =>
            configuration.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));

        services
            .AddOptions<Payments.Webhooks.PaymentWebhookOptions>()
            .Bind(configuration.GetSection(Payments.Webhooks.PaymentWebhookOptions.SectionName))
            .Validate(options => !options.RequireSignature || !string.IsNullOrWhiteSpace(options.SharedSecret),
                "PaymentWebhooks:SharedSecret is required when signature verification is enabled.")
            .ValidateOnStart();

        return services;
    }
}
