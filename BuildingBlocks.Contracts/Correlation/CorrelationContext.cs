namespace BuildingBlocks.Contracts.Correlation;

public static class CorrelationContext
{
    private static readonly AsyncLocal<string?> CurrentCorrelationId = new();

    public static string? CorrelationId => CurrentCorrelationId.Value;

    public static IDisposable BeginScope(string? correlationId)
    {
        var previousCorrelationId = CurrentCorrelationId.Value;
        CurrentCorrelationId.Value = string.IsNullOrWhiteSpace(correlationId)
            ? null
            : correlationId.Trim();

        return new RestoreScope(previousCorrelationId);
    }

    private sealed class RestoreScope : IDisposable
    {
        private readonly string? _previousCorrelationId;
        private bool _disposed;

        public RestoreScope(string? previousCorrelationId)
        {
            _previousCorrelationId = previousCorrelationId;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            CurrentCorrelationId.Value = _previousCorrelationId;
            _disposed = true;
        }
    }
}
