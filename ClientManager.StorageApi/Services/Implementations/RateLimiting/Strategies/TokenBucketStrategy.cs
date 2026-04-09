using ClientManager.DataAccess.Databases.Interfaces;
using ClientManager.Shared.Models.Entities;
using ClientManager.StorageApi.Models.Entities;
using ClientManager.StorageApi.Services.Interfaces;

namespace ClientManager.StorageApi.Services.Implementations.RateLimiting.Strategies;

/// <summary>
/// Token-bucket rate limiting backed by persistent counter state.
/// </summary>
public class TokenBucketStrategy : IRateLimitStrategy
{
    private readonly IRateLimitStateDatabase _stateDatabase;

    public TokenBucketStrategy(IRateLimitStateDatabase stateDatabase)
    {
        _stateDatabase = stateDatabase;
    }

    /// <inheritdoc />
    public async Task<RateLimitResult> EvaluateAsync(
        string key,
        ClientRateLimit rateLimit,
        CancellationToken cancellationToken = default)
    {
        var state = CreateState(key, rateLimit);
        var counts = await _stateDatabase.GetMultipleCountsAsync([state.TokensKey, state.LastRefillKey], cancellationToken);
        var bucketState = CalculateBucketState(counts[state.TokensKey], counts[state.LastRefillKey], state);

        if (bucketState.LastRefill == 0)
        {
            return await InitializeBucketAsync(state, cancellationToken);
        }

        if (bucketState.Tokens <= 0)
        {
            return await PersistDeniedAsync(state, bucketState.NewLastRefill, cancellationToken);
        }

        return await PersistAllowedAsync(state, bucketState.Tokens - 1, bucketState.NewLastRefill, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<RateLimitResult> PeekAsync(
        string key,
        ClientRateLimit rateLimit,
        CancellationToken cancellationToken = default)
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
                RetryAfterSeconds = Math.Max(1, (int)(state.RefillIntervalSeconds / state.TokensPerRefill))
            };
        }

        return new RateLimitResult
        {
            IsAllowed = true,
            RemainingRequests = (int)bucketState.Tokens
        };
    }

    private async Task<RateLimitResult> InitializeBucketAsync(
        BucketStateContext state,
        CancellationToken cancellationToken)
    {
        var initialTokens = state.BucketCapacity - 1;
        await PersistStateAsync(state, initialTokens, state.Now, cancellationToken);

        return new RateLimitResult
        {
            IsAllowed = true,
            RemainingRequests = (int)initialTokens
        };
    }

    private async Task<RateLimitResult> PersistDeniedAsync(
        BucketStateContext state,
        long lastRefill,
        CancellationToken cancellationToken)
    {
        await PersistStateAsync(state, 0, lastRefill, cancellationToken);

        return new RateLimitResult
        {
            IsAllowed = false,
            RemainingRequests = 0,
            RetryAfterSeconds = Math.Max(1, (int)(state.RefillIntervalSeconds / state.TokensPerRefill))
        };
    }

    private async Task<RateLimitResult> PersistAllowedAsync(
        BucketStateContext state,
        long tokens,
        long lastRefill,
        CancellationToken cancellationToken)
    {
        await PersistStateAsync(state, tokens, lastRefill, cancellationToken);

        return new RateLimitResult
        {
            IsAllowed = true,
            RemainingRequests = (int)tokens
        };
    }

    private Task PersistStateAsync(
        BucketStateContext state,
        long tokens,
        long lastRefill,
        CancellationToken cancellationToken)
    {
        return _stateDatabase.SetMultipleCountsAsync(new Dictionary<string, (long value, TimeSpan window)>
        {
            [state.TokensKey] = (tokens, state.StateWindow),
            [state.LastRefillKey] = (lastRefill, state.StateWindow)
        }, cancellationToken);
    }

    private static BucketComputation CalculateBucketState(
        long storedTokens,
        long lastRefill,
        BucketStateContext state)
    {
        if (lastRefill == 0)
        {
            return new BucketComputation(0, 0, 0);
        }

        var elapsed = state.Now - lastRefill;
        var tokensToAdd = (elapsed / state.RefillIntervalSeconds) * state.TokensPerRefill;
        var tokens = Math.Min(state.BucketCapacity, storedTokens + tokensToAdd);
        var newLastRefill = tokensToAdd > 0
            ? lastRefill + (tokensToAdd / state.TokensPerRefill) * state.RefillIntervalSeconds
            : lastRefill;

        return new BucketComputation(tokens, lastRefill, newLastRefill);
    }

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