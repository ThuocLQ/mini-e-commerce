using System.Security.Cryptography;
using System.Text;
using MediatR;
using OrderingService.Application.Abstractions;
using OrderingService.Application.OrderPaymentSagas.ApplyPaymentEvent;
using OrderingService.Domain.OrderPaymentSagas;
using Microsoft.Extensions.Options;

namespace OrderingService.Infrastructure.Sagas;

public sealed class PaymentSagaTimeoutWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly PaymentSagaTimeoutOptions _options;
    private readonly ILogger<PaymentSagaTimeoutWorker> _logger;

    public PaymentSagaTimeoutWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<PaymentSagaTimeoutOptions> options,
        ILogger<PaymentSagaTimeoutWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Payment saga timeout worker is disabled.");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.IntervalSeconds));
        await ProcessTimedOutSagasAsync(stoppingToken);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await ProcessTimedOutSagasAsync(stoppingToken);
        }
    }

    private async Task ProcessTimedOutSagasAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var sagaRepository = scope.ServiceProvider.GetRequiredService<IOrderPaymentSagaRepository>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var timedOutSagas = await sagaRepository.GetTimedOutAsync(DateTime.UtcNow, _options.BatchSize, cancellationToken);

        foreach (var saga in timedOutSagas)
        {
            try
            {
                await sender.Send(new ApplyPaymentSagaEventCommand(
                    CreateTimeoutEventId(saga),
                    OrderPaymentSagaEventType.PaymentTimedOut,
                    saga.OrderId,
                    saga.PaymentId,
                    "Payment timed out."), cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Failed to process timeout for payment saga {PaymentSagaId}, order {OrderId}.",
                    saga.Id,
                    saga.OrderId);
            }
        }
    }

    private static Guid CreateTimeoutEventId(OrderPaymentSaga saga)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"payment-timeout:{saga.Id:N}:{saga.TimeoutAtUtc:O}"));
        return new Guid(hash.AsSpan(0, 16));
    }
}
