using InventoryService.Application.Abstractions;

namespace InventoryService.Infrastructure.Inventory;

public sealed class ExpiredInventoryReservationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ExpiredInventoryReservationWorker> _logger;

    public ExpiredInventoryReservationWorker(IServiceScopeFactory scopeFactory, ILogger<ExpiredInventoryReservationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IInventoryReservationRepository>();
                var released = await repository.ReleaseExpiredAsync(stoppingToken);
                if (released > 0)
                {
                    _logger.LogInformation("Released {ReservationCount} expired inventory reservations.", released);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to release expired inventory reservations.");
            }
        }
    }
}

