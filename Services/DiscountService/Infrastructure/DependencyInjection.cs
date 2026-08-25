using DiscountService.Application.Abstractions;
using BuildingBlocks.Contracts.Events.Discounts;
using DiscountService.Infrastructure.Messaging;
using DiscountService.Infrastructure.Persistence;
using MassTransit;

namespace DiscountService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IDbConnectionFactory, NpgsqlConnectionFactory>();
        services.AddSingleton<IDatabaseInitializer, PostgresDatabaseInitializer>();
        services.AddScoped<IDiscountRepository, DapperDiscountRepository>();
        services.AddScoped<IPromotionReservationRepository, DapperPromotionReservationRepository>();
        services.AddPostgresReadinessCheck(configuration, "DiscountDb");
        services.AddRabbitMqReadinessCheck(configuration);

        services.AddMassTransit(busRegistrationConfigurator =>
        {
            busRegistrationConfigurator.AddConsumer<PromotionRedeemRequestedConsumer>();
            busRegistrationConfigurator.AddConsumer<PromotionReleaseRequestedConsumer>();
            busRegistrationConfigurator.UsingRabbitMq((context, busFactoryConfigurator) =>
            {
                var rabbitMqOptions = RabbitMqOptionsResolver.Resolve(configuration);

                busFactoryConfigurator.Message<PromotionRedeemRequestedIntegrationEvent>(messageConfigurator =>
                    messageConfigurator.SetEntityName("promotion.redeem-requested"));
                busFactoryConfigurator.Message<PromotionReleaseRequestedIntegrationEvent>(messageConfigurator =>
                    messageConfigurator.SetEntityName("promotion.release-requested"));

                busFactoryConfigurator.Host(
                    rabbitMqOptions.Host,
                    rabbitMqOptions.Port,
                    rabbitMqOptions.VirtualHost,
                    hostConfigurator =>
                    {
                        hostConfigurator.Username(rabbitMqOptions.UserName);
                        hostConfigurator.Password(rabbitMqOptions.Password);
                    });

                busFactoryConfigurator.ReceiveEndpoint("discount.promotion-reservation-requests", endpoint =>
                {
                    endpoint.ConfigureConsumer<PromotionRedeemRequestedConsumer>(context);
                    endpoint.ConfigureConsumer<PromotionReleaseRequestedConsumer>(context);
                });
            });
        });

        return services;
    }
}
