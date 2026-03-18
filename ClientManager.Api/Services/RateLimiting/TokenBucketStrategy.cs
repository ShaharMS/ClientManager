using ClientManager.Api.Interfaces;
using ClientManager.DataAccess.Interfaces;
using ClientManager.Shared.Models.Entities;

namespace ClientManager.Api.Services.RateLimiting;

/// <summary>
/// Token bucket rate limiting strategy. Tokens refill at a fixed rate and each request
/// consumes one token. Allows controlled bursts up to the bucket capacity.
/// </summary>
public class TokenBucketStrategy : IRateLimitStrategy
{
    private readonly IRateLimitStateStore _stateStore;

    /// <summary>
    /// Initializes a new instance of <see cref="TokenBucketStrategy"/>.
    /// </summary>
    /// <param name="stateStore">The state store for rate limit counters.</param>
    public TokenBucketStrategy(IRateLimitStateStore stateStore)
    {
        _stateStore = stateStore;
    }

    /// <inheritdoc />
    public async Task<RateLimitResult> EvaluateAsync(
        string key,
        ClientRateLimit rateLimit,
        CancellationToken cancellationToken = default)
    {
        var tokensKey = $"bucket:{key}:tokens";
        var lastRefillKey = $"bucket:{key}:lastrefill";
        var bucketCapacity = rateLimit.MaxRequests;
        var tokensPerRefill = rateLimit.TokensPerRefill ?? 1;
        var refillIntervalSeconds = (long)rateLimit.Window.TotalSeconds;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // Use a long window for token bucket state so it doesn't expire prematurely
        var stateWindow = TimeSpan.FromSeconds(refillIntervalSeconds * bucketCapacity * 2);

        var storedTokens = await _stateStore.GetCountAsync(tokensKey, cancellationToken);
        var lastRefill = await _stateStore.GetCountAsync(lastRefillKey, cancellationToken);

        long tokens;
        if (lastRefill == 0)
        {
            // First request — initialize bucket to full capacity minus 1 (consuming this request)
            tokens = bucketCapacity - 1;
            await _stateStore.SetCountAsync(tokensKey, tokens, stateWindow, cancellationToken);
            await _stateStore.SetCountAsync(lastRefillKey, now, stateWindow, cancellationToken);

            return new RateLimitResult
            {
                IsAllowed = true,
                RemainingRequests = (int)tokens
            };
        }

        // Calculate tokens to add based on elapsed time
        var elapsed = now - lastRefill;
        var tokensToAdd = (elapsed / refillIntervalSeconds) * tokensPerRefill;
        tokens = Math.Min(bucketCapacity, storedTokens + tokensToAdd);

        // Update last refill time if tokens were added
        var newLastRefill = tokensToAdd > 0
            ? lastRefill + (tokensToAdd / tokensPerRefill) * refillIntervalSeconds
            : lastRefill;

        if (tokens <= 0)
        {
            // No tokens available
            var retryAfter = (int)(refillIntervalSeconds / tokensPerRefill);

            await _stateStore.SetCountAsync(tokensKey, 0, stateWindow, cancellationToken);
            await _stateStore.SetCountAsync(lastRefillKey, newLastRefill, stateWindow, cancellationToken);

            return new RateLimitResult
            {
                IsAllowed = false,
                RemainingRequests = 0,
                RetryAfterSeconds = Math.Max(1, retryAfter)
            };
        }

        // Consume one token
        tokens--;
        await _stateStore.SetCountAsync(tokensKey, tokens, stateWindow, cancellationToken);
        await _stateStore.SetCountAsync(lastRefillKey, newLastRefill, stateWindow, cancellationToken);

        return new RateLimitResult
        {
            IsAllowed = true,
            RemainingRequests = (int)tokens
        };
    }

    /// <inheritdoc />
    public async Task<RateLimitResult> PeekAsync(
        string key,
        ClientRateLimit rateLimit,
        CancellationToken cancellationToken = default)
    {
        var tokensKey = $"bucket:{key}:tokens";
        var lastRefillKey = $"bucket:{key}:lastrefill";
        var bucketCapacity = rateLimit.MaxRequests;
        var tokensPerRefill = rateLimit.TokensPerRefill ?? 1;
        var refillIntervalSeconds = (long)rateLimit.Window.TotalSeconds;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var storedTokens = await _stateStore.GetCountAsync(tokensKey, cancellationToken);
        var lastRefill = await _stateStore.GetCountAsync(lastRefillKey, cancellationToken);

        if (lastRefill == 0)
        {
            return new RateLimitResult
            {
                IsAllowed = true,
                RemainingRequests = bucketCapacity
            };
        }

        var elapsed = now - lastRefill;
        var tokensToAdd = (elapsed / refillIntervalSeconds) * tokensPerRefill;
        var tokens = Math.Min(bucketCapacity, storedTokens + tokensToAdd);

        if (tokens <= 0)
        {
            var retryAfter = (int)(refillIntervalSeconds / tokensPerRefill);

            return new RateLimitResult
            {
                IsAllowed = false,
                RemainingRequests = 0,
                RetryAfterSeconds = Math.Max(1, retryAfter)
            };
        }

        return new RateLimitResult
        {
            IsAllowed = true,
            RemainingRequests = (int)tokens
        };
    }
}
