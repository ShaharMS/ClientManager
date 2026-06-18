using System.Text.Json;
using ClientManager.DataAccess.Stores.Implementations.Helpers;

namespace ClientManager.DataAccess.Stores.Implementations.Helpers;

/// <summary>
/// Shared fallback implementations for optimistic concurrency helpers.
/// </summary>
internal static class DocumentStoreConcurrencyDefaults
{
    public static async Task<bool> SetIfFieldEqualsAsync<T>(
        Func<string, string, CancellationToken, Task<T?>> getAsync,
        Func<string, string, T, CancellationToken, Task> setAsync,
        string collection,
        string id,
        T document,
        string fieldName,
        object? expectedValue,
        CancellationToken cancellationToken) where T : class
    {
        var existing = await getAsync(collection, id, cancellationToken);
        if (!FieldEquals(existing, fieldName, expectedValue))
        {
            return false;
        }

        await setAsync(collection, id, document, cancellationToken);
        return true;
    }

    public static async Task<bool> TryIncrementWithinLimitsAsync(
        Func<string, TimeSpan, CancellationToken, Task<long>> incrementAsync,
        Func<IReadOnlyDictionary<string, long>, CancellationToken, Task> decrementManyAsync,
        IReadOnlyList<(string key, long max, TimeSpan window)> counters,
        CancellationToken cancellationToken)
    {
        if (counters.Count == 0)
        {
            return true;
        }

        var incremented = new Dictionary<string, long>(counters.Count, StringComparer.Ordinal);
        foreach (var counter in counters)
        {
            var value = await incrementAsync(counter.key, counter.window, cancellationToken);
            incremented[counter.key] = 1;
            if (value > counter.max)
            {
                await decrementManyAsync(incremented, cancellationToken);
                return false;
            }
        }

        return true;
    }

    public static async Task<(bool IsAllowed, long RemainingTokens, long RetryAfterSeconds)> TryConsumeTokenBucketAsync(
        Func<IEnumerable<string>, CancellationToken, Task<IReadOnlyDictionary<string, long>>> getManyAsync,
        Func<IReadOnlyDictionary<string, (long value, TimeSpan window)>, CancellationToken, Task> setManyAsync,
        string tokensKey,
        string lastRefillKey,
        int bucketCapacity,
        int tokensPerRefill,
        long refillIntervalSeconds,
        TimeSpan stateWindow,
        long nowUnixSeconds,
        CancellationToken cancellationToken)
    {
        var counts = await getManyAsync([tokensKey, lastRefillKey], cancellationToken);
        var storedTokens = counts[tokensKey];
        var lastRefill = counts[lastRefillKey];

        if (lastRefill == 0)
        {
            var initialTokens = bucketCapacity - 1;
            await setManyAsync(new Dictionary<string, (long value, TimeSpan window)>
            {
                [tokensKey] = (initialTokens, stateWindow),
                [lastRefillKey] = (nowUnixSeconds, stateWindow)
            }, cancellationToken);

            return (true, initialTokens, 0);
        }

        var alignedNow = AlignWindow(nowUnixSeconds, refillIntervalSeconds);
        var alignedLast = AlignWindow(lastRefill, refillIntervalSeconds);
        var intervalsPassed = (alignedNow - alignedLast) / refillIntervalSeconds;
        var tokensToAdd = intervalsPassed * tokensPerRefill;
        var tokens = Math.Min(bucketCapacity, storedTokens + tokensToAdd);
        var newLastRefill = intervalsPassed > 0 ? alignedNow : lastRefill;

        if (tokens <= 0)
        {
            await setManyAsync(new Dictionary<string, (long value, TimeSpan window)>
            {
                [tokensKey] = (0, stateWindow),
                [lastRefillKey] = (newLastRefill, stateWindow)
            }, cancellationToken);

            var retryAfter = Math.Max(1, refillIntervalSeconds - (nowUnixSeconds - alignedNow));
            return (false, 0, retryAfter);
        }

        var remaining = tokens - 1;
        await setManyAsync(new Dictionary<string, (long value, TimeSpan window)>
        {
            [tokensKey] = (remaining, stateWindow),
            [lastRefillKey] = (newLastRefill, stateWindow)
        }, cancellationToken);

        return (true, remaining, 0);
    }

    private static bool FieldEquals<T>(T? document, string fieldName, object? expectedValue) where T : class
    {
        if (document is null)
        {
            return expectedValue is null;
        }

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(document, StoreSerialization.Options));
        if (!json.RootElement.TryGetProperty(fieldName, out var property))
        {
            return expectedValue is null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.True => expectedValue is true,
            JsonValueKind.False => expectedValue is false,
            JsonValueKind.Number when property.TryGetInt64(out var number) => Convert.ToInt64(expectedValue) == number,
            JsonValueKind.String => string.Equals(property.GetString(), expectedValue?.ToString(), StringComparison.Ordinal),
            _ => string.Equals(property.GetRawText(), JsonSerializer.Serialize(expectedValue, StoreSerialization.JsonOptions), StringComparison.Ordinal)
        };
    }

    private static long AlignWindow(long timestamp, long intervalSeconds)
    {
        return timestamp / intervalSeconds * intervalSeconds;
    }
}
