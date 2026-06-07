using ClientManager.DataAccess.Databases.Interfaces;
using ClientManager.Shared.Models.Entities;
using ClientManager.Api.Services.Storage.Models.Entities;
using ClientManager.Api.Services.Interfaces;
using ClientManager.Api.Services.Storage.Utils.Instrumentation;

namespace ClientManager.Api.Services.Storage.Implementations.RateLimiting.Strategies;

/// <summary>
/// Fixed-window rate limiting backed by the shared counter store.
/// </summary>
public class FixedWindowStrategy : IRateLimitStrategy
{
    private readonly IRateLimitStateDatabase _stateDatabase;
    private readonly StorageMetrics _metrics;

    public FixedWindowStrategy(IRateLimitStateDatabase stateDatabase, StorageMetrics metrics)
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
            nameof(FixedWindowStrategy),
            "increment",
            counterKeyCount: 1,
            async () =>
            {
                var windowSeconds = (long)rateLimit.Window.TotalSeconds;
                var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var windowNumber = now / windowSeconds;
                var windowKey = $"fixed:{key}:{windowNumber}";

                var count = await _stateDatabase.IncrementAsync(windowKey, rateLimit.Window, cancellationToken);
                return CreateEvaluateResult(rateLimit.MaxRequests, count, windowNumber, windowSeconds, now);
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
            nameof(FixedWindowStrategy),
            "peek",
            counterKeyCount: 1,
            async () =>
            {
                var windowSeconds = (long)rateLimit.Window.TotalSeconds;
                var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var windowNumber = now / windowSeconds;
                var windowKey = $"fixed:{key}:{windowNumber}";

                var count = await _stateDatabase.GetCountAsync(windowKey, cancellationToken);
                return CreatePeekResult(rateLimit.MaxRequests, count, windowNumber, windowSeconds, now);
            });
    }

    private static RateLimitResult CreateEvaluateResult(
        int maxRequests,
        long count,
        long windowNumber,
        long windowSeconds,
        long now)
    {
        if (count <= maxRequests)
        {
            return new RateLimitResult
            {
                IsAllowed = true,
                RemainingRequests = maxRequests - (int)count
            };
        }

        return CreateDeniedResult(windowNumber, windowSeconds, now);
    }

    private static RateLimitResult CreatePeekResult(
        int maxRequests,
        long count,
        long windowNumber,
        long windowSeconds,
        long now)
    {
        if (count < maxRequests)
        {
            return new RateLimitResult
            {
                IsAllowed = true,
                RemainingRequests = maxRequests - (int)count
            };
        }

        return CreateDeniedResult(windowNumber, windowSeconds, now);
    }

    private static RateLimitResult CreateDeniedResult(
        long windowNumber,
        long windowSeconds,
        long now)
    {
        var retryAfter = (windowNumber + 1) * windowSeconds - now;
        return new RateLimitResult
        {
            IsAllowed = false,
            RemainingRequests = 0,
            RetryAfterSeconds = (int)retryAfter
        };
    }
}