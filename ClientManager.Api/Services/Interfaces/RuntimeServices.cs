using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Responses;

namespace ClientManager.Api.Services.Interfaces;

/// <summary>
/// Evaluates rate-limit policies at per-client and global scopes.
/// </summary>
/// <remarks>
/// <para>
/// Rate limiting runs inside the access-check hot path. Each scope uses the strategy configured on
/// the relevant <see cref="RateLimitPolicy"/> (fixed window, approximate sliding window, or token
/// bucket) and persists state through atomic storage counters.
/// </para>
/// <para>
/// Global limits apply per service unless a client is exempt. Per-client limits apply only when
/// the client's service access entry defines a policy.
/// </para>
/// </remarks>
public interface IRateLimitService
{
    /// <summary>
    /// Evaluates the per-client-per-service rate limit and increments counters on success.
    /// </summary>
    /// <param name="config">The client configuration containing service access settings.</param>
    /// <param name="serviceId">The service being accessed.</param>
    /// <param name="cancellationToken">Cancels the evaluation before storage writes complete.</param>
    /// <returns>The rate limit result, including whether the request is allowed.</returns>
    Task<RateLimitResult> CheckAndIncrementAsync(ClientConfiguration config, string serviceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Evaluates the global per-service rate limit and increments counters on success.
    /// </summary>
    /// <param name="config">The client configuration (used for exemption flags).</param>
    /// <param name="serviceId">The service being accessed.</param>
    /// <param name="cancellationToken">Cancels the evaluation before storage writes complete.</param>
    /// <returns>The rate limit result, including whether the request is allowed.</returns>
    Task<RateLimitResult> CheckGlobalServiceLimitAsync(ClientConfiguration config, string serviceId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Defines a rate-limit strategy implementation.
/// </summary>
/// <remarks>
/// <para>
/// Each strategy encapsulates how counters are read, updated, and expired for one algorithm.
/// Implementations delegate persistence to <c>IRateLimitStateDatabase</c> so Redis and MongoDB can
/// provide backend-appropriate atomicity.
/// </para>
/// </remarks>
public interface IRateLimitStrategy
{
    /// <summary>
    /// Evaluates the policy for the given key and increments consumption when allowed.
    /// </summary>
    /// <param name="key">Storage key identifying the counter scope.</param>
    /// <param name="rateLimit">The policy parameters to enforce.</param>
    /// <param name="cancellationToken">Cancels the evaluation before storage writes complete.</param>
    /// <returns>The evaluation result.</returns>
    Task<RateLimitResult> EvaluateAsync(string key, RateLimitPolicy rateLimit, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the current policy state without incrementing counters.
    /// </summary>
    /// <param name="key">Storage key identifying the counter scope.</param>
    /// <param name="rateLimit">The policy parameters to inspect.</param>
    /// <param name="cancellationToken">Cancels the read before it completes.</param>
    /// <returns>The peek result showing remaining capacity.</returns>
    Task<RateLimitResult> PeekAsync(string key, RateLimitPolicy rateLimit, CancellationToken cancellationToken = default);
}
