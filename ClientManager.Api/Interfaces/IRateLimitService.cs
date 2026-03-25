using ClientManager.Shared.Models.Entities;

namespace ClientManager.Api.Interfaces;

/// <summary>
/// The result of a rate limit evaluation.
/// </summary>
public record RateLimitResult
{
    /// <summary>
    /// Whether the request is allowed under the evaluated rate limit.
    /// </summary>
    public bool IsAllowed { get; init; }

    /// <summary>
    /// The number of remaining requests before the limit is reached.
    /// </summary>
    public int RemainingRequests { get; init; }

    /// <summary>
    /// Seconds until the client may retry, if rate limited.
    /// </summary>
    public int? RetryAfterSeconds { get; init; }

    /// <summary>
    /// Whether the denial was caused by a global aggregate limit.
    /// </summary>
    public bool IsGlobalLimitHit { get; init; }
}

/// <summary>
/// Evaluates rate limit policies at per-client, per-service, and global scopes.
/// </summary>
public interface IRateLimitService
{
    /// <summary>
    /// Checks and increments the per-client-per-service rate limit counter.
    /// Accepts an already-loaded <see cref="ClientConfiguration"/> to avoid a redundant
    /// repository load — the calling service should supply the config it already has.
    /// </summary>
    /// <param name="config">The pre-loaded client configuration.</param>
    /// <param name="serviceId">The unique identifier of the service.</param>
    /// <param name="cancellationToken">Cancels the rate limit evaluation.</param>
    /// <returns>The rate limit evaluation result.</returns>
    Task<RateLimitResult> CheckAndIncrementAsync(ClientConfiguration config, string serviceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks and increments the global per-client rate limit counter.
    /// Loads client configuration internally since callers on this path do not have it preloaded.
    /// </summary>
    /// <param name="clientId">The unique identifier of the client.</param>
    /// <param name="cancellationToken">Cancels the rate limit evaluation.</param>
    /// <returns>The rate limit evaluation result.</returns>
    Task<RateLimitResult> CheckGlobalAndIncrementAsync(string clientId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks the global aggregate rate limit for a service.
    /// Accepts an already-loaded <see cref="ClientConfiguration"/> to avoid a redundant
    /// repository load — the calling service should supply the config it already has.
    /// </summary>
    /// <param name="config">The pre-loaded client configuration.</param>
    /// <param name="serviceId">The unique identifier of the service.</param>
    /// <param name="cancellationToken">Cancels the rate limit evaluation.</param>
    /// <returns>The rate limit evaluation result.</returns>
    Task<RateLimitResult> CheckGlobalServiceLimitAsync(ClientConfiguration config, string serviceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks the global aggregate rate limit for a resource pool.
    /// Accepts an already-loaded <see cref="ClientConfiguration"/> to avoid a redundant
    /// repository load — the calling service should supply the config it already has.
    /// </summary>
    /// <param name="config">The pre-loaded client configuration.</param>
    /// <param name="resourcePoolId">The unique identifier of the resource pool.</param>
    /// <param name="cancellationToken">Cancels the rate limit evaluation.</param>
    /// <returns>The rate limit evaluation result.</returns>
    Task<RateLimitResult> CheckGlobalResourcePoolLimitAsync(ClientConfiguration config, string resourcePoolId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks the rate limit status without incrementing any counters.
    /// Loads client configuration internally since callers on this path do not have it preloaded.
    /// </summary>
    /// <param name="clientId">The unique identifier of the client.</param>
    /// <param name="serviceId">The unique identifier of the service.</param>
    /// <param name="cancellationToken">Cancels the rate limit evaluation.</param>
    /// <returns>The rate limit evaluation result.</returns>
    Task<RateLimitResult> CheckWithoutIncrementAsync(string clientId, string serviceId, CancellationToken cancellationToken = default);
}
