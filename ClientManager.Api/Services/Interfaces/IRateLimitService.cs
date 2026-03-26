using ClientManager.Api.Models.Entities;
using ClientManager.Shared.Models.Entities;

namespace ClientManager.Api.Services.Interfaces;

/// <summary>
/// Evaluates rate limit policies at per-client, per-service, and global scopes.
/// <para>
/// Rate limits are evaluated at three levels, each independently configured:
/// <list type="bullet">
///   <item><b>Per-client-per-service</b> — configured in <see cref="ServiceAccessSettings.RateLimit"/>.
///     Limits how many requests a single client can make to a specific service within a time window.</item>
///   <item><b>Per-client global</b> — configured in <see cref="ClientConfiguration.GlobalRateLimit"/>.
///     Limits total requests from a single client across all services.</item>
///   <item><b>Per-service global</b> — configured via <see cref="GlobalRateLimit"/> documents.
///     Limits total requests from all contributing clients to a given service or resource pool.</item>
/// </list>
/// </para>
/// </summary>
public interface IRateLimitService
{
    /// <summary>
    /// Checks and increments the per-client-per-service rate limit counter.
    /// Accepts an already-loaded <see cref="ClientConfiguration"/> to avoid a redundant
    /// database load — the calling service should supply the config it already has.
    /// </summary>
    /// <param name="config">The pre-loaded client configuration.</param>
    /// <param name="serviceId">The unique identifier of the service.</param>
    /// <param name="cancellationToken">Cancels the rate limit evaluation and any backing-store I/O.</param>
    /// <returns>The rate limit evaluation result.</returns>
    Task<RateLimitResult> CheckAndIncrementAsync(ClientConfiguration config, string serviceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks and increments the global per-client rate limit counter.
    /// Loads client configuration internally since callers on this path do not have it preloaded.
    /// </summary>
    /// <param name="clientId">The unique identifier of the client.</param>
    /// <param name="cancellationToken">Cancels the rate limit evaluation, including the client configuration lookup.</param>
    /// <returns>The rate limit evaluation result.</returns>
    Task<RateLimitResult> CheckGlobalAndIncrementAsync(string clientId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks the global aggregate rate limit for a service.
    /// This limit caps total traffic from all contributing clients to a specific service.
    /// Accepts an already-loaded <see cref="ClientConfiguration"/> to check the client's
    /// exempt/contribute flags without an extra database call.
    /// </summary>
    /// <param name="config">The pre-loaded client configuration.</param>
    /// <param name="serviceId">The unique identifier of the service.</param>
    /// <param name="cancellationToken">Cancels the global limit evaluation.</param>
    /// <returns>The rate limit evaluation result.</returns>
    Task<RateLimitResult> CheckGlobalServiceLimitAsync(ClientConfiguration config, string serviceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks the global aggregate rate limit for a resource pool.
    /// This limit caps total acquisition attempts from all contributing clients to a specific pool.
    /// Accepts an already-loaded <see cref="ClientConfiguration"/> to check the client's
    /// exempt/contribute flags without an extra database call.
    /// </summary>
    /// <param name="config">The pre-loaded client configuration.</param>
    /// <param name="resourcePoolId">The unique identifier of the resource pool.</param>
    /// <param name="cancellationToken">Cancels the global limit evaluation.</param>
    /// <returns>The rate limit evaluation result.</returns>
    Task<RateLimitResult> CheckGlobalResourcePoolLimitAsync(ClientConfiguration config, string resourcePoolId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks the rate limit status without incrementing any counters.
    /// Used for read-only dashboard views (e.g., accessibility reports) where you need to
    /// know the remaining quota without consuming it.
    /// Loads client configuration internally since callers on this path do not have it preloaded.
    /// </summary>
    /// <param name="clientId">The unique identifier of the client.</param>
    /// <param name="serviceId">The unique identifier of the service.</param>
    /// <param name="cancellationToken">Cancels the peek evaluation.</param>
    /// <returns>The rate limit evaluation result.</returns>
    Task<RateLimitResult> CheckWithoutIncrementAsync(string clientId, string serviceId, CancellationToken cancellationToken = default);
}
