namespace OrderQueryService.Infrastructure.ReadModels.MongoDb;

public static class MongoReadModelInitializationExtensions
{
    public static async Task InitializeMongoReadModelsAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var initializer = scope.ServiceProvider.GetRequiredService<IMongoReadModelInitializer>();
        var options = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<MongoDbOptions>>().Value;
        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("MongoReadModelInitialization");

        for (var attempt = 1; attempt <= options.InitializeMaxRetryCount; attempt++)
        {
            try
            {
                await initializer.InitializeAsync(app.Lifetime.ApplicationStopping);
                return;
            }
            catch (Exception ex) when (
                attempt < options.InitializeMaxRetryCount &&
                !app.Lifetime.ApplicationStopping.IsCancellationRequested)
            {
                logger.LogWarning(
                    ex,
                    "MongoDB read model initialization failed on attempt {Attempt}/{MaxAttempts}. Retrying in {DelaySeconds} seconds.",
                    attempt,
                    options.InitializeMaxRetryCount,
                    options.InitializeRetryDelaySeconds);

                await Task.Delay(
                    TimeSpan.FromSeconds(options.InitializeRetryDelaySeconds),
                    app.Lifetime.ApplicationStopping);
            }
        }
    }
}
