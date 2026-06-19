namespace ClientManager.DataAccess.Stores.Implementations.Helpers;

/// <summary>
/// Non-atomic token-bucket fallback for single-host document stores.
/// </summary>
internal static class TokenBucketConsumeDefaults
{
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

    private static long AlignWindow(long timestamp, long intervalSeconds) =>
        timestamp / intervalSeconds * intervalSeconds;
}
