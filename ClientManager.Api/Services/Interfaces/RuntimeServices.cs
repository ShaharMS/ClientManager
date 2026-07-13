using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Responses;

namespace ClientManager.Api.Services.Interfaces;

/// <summary>
/// Evaluates rate-limit policies at per-client and global scopes.
/// </summary>
public interface IRateLimitService
{
    Task<RateLimitResult> CheckAndIncrementAsync(ClientConfiguration config, string serviceId, CancellationToken cancellationToken = default);

    Task<RateLimitResult> CheckGlobalServiceLimitAsync(ClientConfiguration config, string serviceId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Defines a rate-limit strategy implementation.
/// </summary>
public interface IRateLimitStrategy
{
    Task<RateLimitResult> EvaluateAsync(string key, RateLimitPolicy rateLimit, CancellationToken cancellationToken = default);

    Task<RateLimitResult> PeekAsync(string key, RateLimitPolicy rateLimit, CancellationToken cancellationToken = default);
}
