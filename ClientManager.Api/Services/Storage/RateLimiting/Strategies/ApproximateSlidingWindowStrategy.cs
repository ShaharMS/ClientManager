using ClientManager.DataAccess.Databases.Interfaces;
using ClientManager.Shared.Models.Entities;
using ClientManager.Api.Services.Interfaces;
using ClientManager.Api.Services.Storage.Instrumentation;

namespace ClientManager.Api.Services.Storage.RateLimiting.Strategies;

/// <summary>
/// Uses weighted current and previous windows to approximate a sliding limit.
/// </summary>
public class ApproximateSlidingWindowStrategy : IRateLimitStrategy
{
    private readonly IRateLimitStateDatabase _stateDatabase;
    private readonly StorageMetrics _metrics;

    public ApproximateSlidingWindowStrategy(IRateLimitStateDatabase stateDatabase, StorageMetrics metrics)
    {
        _stateDatabase = stateDatabase;
        _metrics = metrics;
    }

    /// <inheritdoc />
    public async Task<RateLimitResult> EvaluateAsync(
        string key,
        ClientRateLimit rateLimit,
        CancellationToken cancellationToken = default)
    {
        return await RateLimitStrategyInstrumentation.TraceAsync(
            _metrics,
            nameof(ApproximateSlidingWindowStrategy),
            "increment",
            counterKeyCount: 2,
            async () =>
            {
                var state = CreateWindowState(key, rateLimit.Window);
                var previousCount = await _stateDatabase.GetCountAsync(state.PreviousWindowKey, cancellationToken);
                var currentCount = await _stateDatabase.IncrementAsync(state.CurrentWindowKey, rateLimit.Window, cancellationToken);

                return CreateResult(rateLimit.MaxRequests, previousCount, currentCount, state);
            });
    }

    /// <inheritdoc />
    public async Task<RateLimitResult> PeekAsync(
        string key,
        ClientRateLimit rateLimit,
        CancellationToken cancellationToken = default)
    {
        return await RateLimitStrategyInstrumentation.TraceAsync(
            _metrics,
            nameof(ApproximateSlidingWindowStrategy),
            "peek",
            counterKeyCount: 2,
            async () =>
            {
                var state = CreateWindowState(key, rateLimit.Window);
                var counts = await _stateDatabase.GetMultipleCountsAsync(
                    [state.PreviousWindowKey, state.CurrentWindowKey],
                    cancellationToken);

                return CreateResult(
                    rateLimit.MaxRequests,
                    counts[state.PreviousWindowKey],
                    counts[state.CurrentWindowKey],
                    state);
            });
    }

    private static RateLimitResult CreateResult(
        int maxRequests,
        long previousCount,
        long currentCount,
        SlidingWindowState state)
    {
        var elapsedRatio = (double)(state.Now - state.WindowStart) / state.WindowSeconds;
        var weightedCount = previousCount * (1 - elapsedRatio) + currentCount;

        if (weightedCount < maxRequests)
        {
            return new RateLimitResult
            {
                IsAllowed = true,
                RemainingRequests = Math.Max(0, maxRequests - (int)Math.Ceiling(weightedCount))
            };
        }

        return new RateLimitResult
        {
            IsAllowed = false,
            RemainingRequests = 0,
            RetryAfterSeconds = (int)(state.WindowSeconds - (state.Now - state.WindowStart))
        };
    }

    private static SlidingWindowState CreateWindowState(string key, TimeSpan window)
    {
        var windowSeconds = (long)window.TotalSeconds;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var currentWindowNumber = now / windowSeconds;

        return new SlidingWindowState(
            $"sliding:{key}:{currentWindowNumber - 1}",
            $"sliding:{key}:{currentWindowNumber}",
            currentWindowNumber * windowSeconds,
            windowSeconds,
            now);
    }

    private sealed record SlidingWindowState(
        string PreviousWindowKey,
        string CurrentWindowKey,
        long WindowStart,
        long WindowSeconds,
        long Now);
}