using System.Net.Sockets;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace MicroShop.ServiceDefaults.HealthChecks;

internal sealed class TcpReadinessHealthCheck : IHealthCheck
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(3);

    private readonly string _host;
    private readonly int _port;
    private readonly TimeSpan _timeout;

    public TcpReadinessHealthCheck(string host, int port, TimeSpan? timeout = null)
    {
        _host = host;
        _port = port;
        _timeout = timeout ?? DefaultTimeout;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_timeout);

        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(_host, _port, timeoutCts.Token);

            return HealthCheckResult.Healthy($"{_host}:{_port} is reachable.");
        }
        catch (Exception ex) when (ex is SocketException or OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy($"{_host}:{_port} is unreachable.", ex);
        }
    }
}
