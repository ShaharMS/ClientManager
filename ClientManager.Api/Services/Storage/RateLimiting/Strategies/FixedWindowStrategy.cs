using ClientManager.Api.Models.Configuration;
using ClientManager.DataAccess.Databases.Interfaces;
using ClientManager.Shared.Models.Entities;
using ClientManager.Api.Services.Interfaces;
using ClientManager.Api.Services.Storage.Instrumentation;
using ClientManager.Api.Services.Storage.RateLimiting;
using Microsoft.Extensions.Options;

namespace ClientManager.Api.Services.Storage.RateLimiting.Strategies;

/// <summary>
/// Fixed-window rate limiting backed by the shared counter store.
/// </summary>
public class FixedWindowStrategy : IRateLimitStrategy
{
    private readonly IRateLimitStateDatabase _stateDatabase;
    private readonly StorageMetrics _metrics;
    private readonly TimeSpan? _windowAlignmentAnchor;

    public FixedWindowStrategy(
        IRateLimitStateDatabase stateDatabase,
        StorageMetrics metrics,
        IOptions<RateLimitingSettings> settings)
    {
        _stateDatabase = stateDatabase;
        _metrics = metrics;
        _windowAlignmentAnchor = settings.Value.WindowAlignmentAnchor;
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
                var windowStart = RateLimitWindowAlignment.GetWindowStart(now, windowSeconds, _windowAlignmentAnchor);
                var windowKey = $"fixed:{key}:{windowStart}";

                var count = await _stateDatabase.IncrementAsync(windowKey, rateLimit.Window, cancellationToken);
                return CreateEvaluateResult(rateLimit.MaxRequests, count, windowStart, windowSeconds, now);
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
                var windowStart = RateLimitWindowAlignment.GetWindowStart(now, windowSeconds, _windowAlignmentAnchor);
                var windowKey = $"fixed:{key}:{windowStart}";

                var count = await _stateDatabase.GetCountAsync(windowKey, cancellationToken);
                return CreatePeekResult(rateLimit.MaxRequests, count, windowStart, windowSeconds, now);
            });
    }

    private RateLimitResult CreateEvaluateResult(
        int maxRequests,
        long count,
        long windowStart,
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

        return CreateDeniedResult(windowSeconds, now);
    }

    private RateLimitResult CreatePeekResult(
        int maxRequests,
        long count,
        long windowStart,
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

        return CreateDeniedResult(windowSeconds, now);
    }

    private RateLimitResult CreateDeniedResult(
        long windowSeconds,
        long now)
    {
        return new RateLimitResult
        {
            IsAllowed = false,
            RemainingRequests = 0,
            RetryAfterSeconds = RateLimitWindowAlignment.GetRetryAfterSeconds(now, windowSeconds, _windowAlignmentAnchor)
        };
    }
}