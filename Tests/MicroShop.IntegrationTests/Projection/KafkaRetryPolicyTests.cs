using Microsoft.Extensions.Options;
using ProjectionWorker.Infrastructure.Kafka;

namespace MicroShop.IntegrationTests.Projection;

public sealed class KafkaRetryPolicyTests
{
    [Fact]
    public void RetryPolicy_UsesBoundedExponentialBackoff()
    {
        var policy = new KafkaRetryPolicy(Options.Create(new KafkaOptions
        {
            MaxRetryAttempts = 3,
            InitialRetryDelaySeconds = 5,
            MaxRetryDelaySeconds = 12
        }));

        Assert.True(policy.CanRetry(0));
        Assert.True(policy.CanRetry(2));
        Assert.False(policy.CanRetry(3));
        Assert.Equal(TimeSpan.FromSeconds(5), policy.GetDelay(1));
        Assert.Equal(TimeSpan.FromSeconds(10), policy.GetDelay(2));
        Assert.Equal(TimeSpan.FromSeconds(12), policy.GetDelay(3));
    }
}
