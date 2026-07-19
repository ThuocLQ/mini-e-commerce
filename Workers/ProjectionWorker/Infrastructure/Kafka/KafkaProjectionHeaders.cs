using System.Globalization;
using System.Text;
using Confluent.Kafka;

namespace ProjectionWorker.Infrastructure.Kafka;

internal static class KafkaProjectionHeaders
{
    private const string RetryCount = "microshop-retry-count";
    private const string NotBeforeUtc = "microshop-not-before-utc";
    private const string OriginalTopic = "microshop-original-topic";
    private const string OriginalPartition = "microshop-original-partition";
    private const string OriginalOffset = "microshop-original-offset";
    private const string FailureKind = "microshop-failure-kind";
    private const string FailureReason = "microshop-failure-reason";
    private const string FailedAtUtc = "microshop-failed-at-utc";

    private static readonly HashSet<string> RoutingHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        RetryCount,
        NotBeforeUtc,
        OriginalTopic,
        OriginalPartition,
        OriginalOffset,
        FailureKind,
        FailureReason,
        FailedAtUtc
    };

    public static int ReadRetryCount(Headers? headers)
    {
        var value = ReadString(headers, RetryCount);
        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var retryCount)
            && retryCount >= 0
                ? retryCount
                : 0;
    }

    public static DateTime? ReadNotBeforeUtc(Headers? headers)
    {
        var value = ReadString(headers, NotBeforeUtc);
        return DateTime.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var notBeforeUtc)
                ? notBeforeUtc
                : null;
    }

    public static Headers BuildRetryHeaders(
        ConsumeResult<string, string> source,
        int retryCount,
        DateTime notBeforeUtc,
        string reason)
    {
        var headers = CopyHeaders(source.Message.Headers);
        AddOriginalPosition(headers, source);
        Add(headers, RetryCount, retryCount.ToString(CultureInfo.InvariantCulture));
        Add(headers, NotBeforeUtc, notBeforeUtc.ToString("O", CultureInfo.InvariantCulture));
        Add(headers, FailureKind, "transient");
        Add(headers, FailureReason, Truncate(reason, 1024));
        Add(headers, FailedAtUtc, DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        return headers;
    }

    public static Headers BuildDeadLetterHeaders(
        ConsumeResult<string, string> source,
        int retryCount,
        string failureKind,
        string reason)
    {
        var headers = CopyHeaders(source.Message.Headers);
        AddOriginalPosition(headers, source);
        Add(headers, RetryCount, retryCount.ToString(CultureInfo.InvariantCulture));
        Add(headers, FailureKind, failureKind);
        Add(headers, FailureReason, Truncate(reason, 1024));
        Add(headers, FailedAtUtc, DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        return headers;
    }

    private static Headers CopyHeaders(Headers? source)
    {
        var destination = new Headers();

        if (source is null)
        {
            return destination;
        }

        foreach (var header in source.Where(header => !RoutingHeaders.Contains(header.Key)))
        {
            destination.Add(header.Key, header.GetValueBytes());
        }

        return destination;
    }

    private static void AddOriginalPosition(
        Headers headers,
        ConsumeResult<string, string> source)
    {
        Add(headers, OriginalTopic, ReadString(source.Message.Headers, OriginalTopic) ?? source.Topic);
        Add(
            headers,
            OriginalPartition,
            ReadString(source.Message.Headers, OriginalPartition)
            ?? source.Partition.Value.ToString(CultureInfo.InvariantCulture));
        Add(
            headers,
            OriginalOffset,
            ReadString(source.Message.Headers, OriginalOffset)
            ?? source.Offset.Value.ToString(CultureInfo.InvariantCulture));
    }

    private static string? ReadString(Headers? headers, string key)
    {
        var header = headers?
            .LastOrDefault(item => string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase));
        var bytes = header?.GetValueBytes();
        return bytes is null ? null : Encoding.UTF8.GetString(bytes);
    }

    private static void Add(Headers headers, string key, string value)
    {
        headers.Add(key, Encoding.UTF8.GetBytes(value));
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
