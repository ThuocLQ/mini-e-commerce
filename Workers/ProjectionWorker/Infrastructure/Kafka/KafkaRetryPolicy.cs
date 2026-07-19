using Microsoft.Extensions.Options;

namespace ProjectionWorker.Infrastructure.Kafka;

public sealed class KafkaRetryPolicy
{
    private readonly KafkaOptions _options;

    public KafkaRetryPolicy(IOptions<KafkaOptions> options)
    {
        _options = options.Value;
    }

    public bool CanRetry(int currentRetryCount)
    {
        return currentRetryCount < _options.MaxRetryAttempts;
    }

    public TimeSpan GetDelay(int nextRetryCount)
    {
        if (nextRetryCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nextRetryCount));
        }

        var exponentialDelay = _options.InitialRetryDelaySeconds
                               * Math.Pow(2, nextRetryCount - 1);
        var boundedDelay = Math.Min(exponentialDelay, _options.MaxRetryDelaySeconds);

        return TimeSpan.FromSeconds(boundedDelay);
    }
}
