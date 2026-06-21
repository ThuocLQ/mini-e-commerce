namespace Microsoft.Extensions.Hosting;

public sealed class HttpClientResilienceSettings
{
    public const string SectionName = "HttpClientResilience";

    public int AttemptTimeoutSeconds { get; init; } = 3;
    public int TotalRequestTimeoutSeconds { get; init; } = 8;
    public int RetryMaxAttempts { get; init; } = 0;
    public int RetryDelayMilliseconds { get; init; } = 200;
    public double CircuitBreakerFailureRatio { get; init; } = 0.5;
    public int CircuitBreakerMinimumThroughput { get; init; } = 8;
    public int CircuitBreakerSamplingDurationSeconds { get; init; } = 30;
    public int CircuitBreakerBreakDurationSeconds { get; init; } = 15;
}
