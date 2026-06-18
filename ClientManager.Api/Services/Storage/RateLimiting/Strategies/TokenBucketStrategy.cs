using ClientManager.Api.Models.Configuration;
using ClientManager.DataAccess.Databases.Interfaces;
using ClientManager.Shared.Models.Entities;
using ClientManager.Api.Services.Interfaces;
using ClientManager.Api.Services.Storage.Instrumentation;
using ClientManager.Api.Services.Storage.RateLimiting;
using Microsoft.Extensions.Options;

namespace ClientManager.Api.Services.Storage.RateLimiting.Strategies;

/// <summary>
/// Token-bucket rate limiting backed by persistent counter state.
/// </summary>
public class TokenBucketStrategy : IRateLimitStrategy
{
    private readonly IRateLimitStateDatabase _stateDatabase;
    private readonly StorageMetrics _metrics;
    private readonly TimeSpan? _windowAlignmentAnchor;

    public TokenBucketStrategy(
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
            nameof(TokenBucketStrategy),
            "increment",
            counterKeyCount: 2,
            async () =>
            {
                var state = CreateState(key, rateLimit);
                var consume = await _stateDatabase.TryConsumeTokenBucketAsync(
                    state.TokensKey,
                    state.LastRefillKey,
                    state.BucketCapacity,
                    state.TokensPerRefill,
                    state.RefillIntervalSeconds,
                    state.StateWindow,
                    state.Now,
                    cancellationToken);

                return new RateLimitResult
                {
                    IsAllowed = consume.IsAllowed,
                    RemainingRequests = consume.RemainingRequests,
                    RetryAfterSeconds = consume.RetryAfterSeconds
                };
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
            nameof(TokenBucketStrategy),
            "peek",
            counterKeyCount: 2,
            async () =>
            {
                var state = CreateState(key, rateLimit);
                var counts = await _stateDatabase.GetMultipleCountsAsync([state.TokensKey, state.LastRefillKey], cancellationToken);
                var bucketState = CalculateBucketState(counts[state.TokensKey], counts[state.LastRefillKey], state);

                if (bucketState.LastRefill == 0)
                {
                    return new RateLimitResult { IsAllowed = true, RemainingRequests = state.BucketCapacity };
                }

                if (bucketState.Tokens <= 0)
                {
                    return new RateLimitResult
                    {
                        IsAllowed = false,
                        RemainingRequests = 0,
                        RetryAfterSeconds = GetRetryAfterSeconds(state)
                    };
                }

                return new RateLimitResult
                {
                    IsAllowed = true,
                    RemainingRequests = (int)bucketState.Tokens
                };
            });
    }

    private BucketComputation CalculateBucketState(
        long storedTokens,
        long lastRefill,
        BucketStateContext state)
    {
        if (lastRefill == 0)
        {
            return new BucketComputation(0, 0, 0);
        }

        var alignedNow = RateLimitWindowAlignment.GetWindowStart(
            state.Now,
            state.RefillIntervalSeconds,
            _windowAlignmentAnchor);
        var alignedLast = RateLimitWindowAlignment.GetWindowStart(
            lastRefill,
            state.RefillIntervalSeconds,
            _windowAlignmentAnchor);
        var intervalsPassed = (alignedNow - alignedLast) / state.RefillIntervalSeconds;
        var tokensToAdd = intervalsPassed * state.TokensPerRefill;
        var tokens = Math.Min(state.BucketCapacity, storedTokens + tokensToAdd);
        var newLastRefill = intervalsPassed > 0 ? alignedNow : lastRefill;

        return new BucketComputation(tokens, lastRefill, newLastRefill);
    }

    private int GetRetryAfterSeconds(BucketStateContext state) =>
        RateLimitWindowAlignment.GetRetryAfterSeconds(
            state.Now,
            state.RefillIntervalSeconds,
            _windowAlignmentAnchor);

    private static BucketStateContext CreateState(string key, ClientRateLimit rateLimit)
    {
        var refillIntervalSeconds = (long)rateLimit.Window.TotalSeconds;
        var bucketCapacity = rateLimit.MaxRequests;
        var tokensPerRefill = rateLimit.TokensPerRefill ?? 1;

        return new BucketStateContext(
            $"bucket:{key}:tokens",
            $"bucket:{key}:lastrefill",
            bucketCapacity,
            tokensPerRefill,
            refillIntervalSeconds,
            TimeSpan.FromSeconds(refillIntervalSeconds * bucketCapacity * 2),
            DateTimeOffset.UtcNow.ToUnixTimeSeconds());
    }

    private sealed record BucketComputation(long Tokens, long LastRefill, long NewLastRefill);

    private sealed record BucketStateContext(
        string TokensKey,
        string LastRefillKey,
        int BucketCapacity,
        int TokensPerRefill,
        long RefillIntervalSeconds,
        TimeSpan StateWindow,
        long Now);
}
