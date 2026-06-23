using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Enums;
using ClientManager.Shared.Models.Responses;

namespace ClientManager.Api.Services.Interfaces;

/// <summary>
/// Evaluates rate-limit policies at per-client and global scopes.
/// </summary>
public interface IRateLimitService
{
    Task<RateLimitResult> CheckAndIncrementAsync(ClientConfiguration config, string serviceId, CancellationToken cancellationToken = default);

    Task<RateLimitResult> CheckGlobalServiceLimitAsync(ClientConfiguration config, string serviceId, CancellationToken cancellationToken = default);

    Task<RateLimitResult> CheckGlobalResourcePoolLimitAsync(ClientConfiguration config, string resourcePoolId, CancellationToken cancellationToken = default);

    Task<RateLimitResult> CheckWithoutIncrementAsync(string clientId, string serviceId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Defines a rate-limit strategy implementation.
/// </summary>
public interface IRateLimitStrategy
{
    Task<RateLimitResult> EvaluateAsync(string key, ClientRateLimit rateLimit, CancellationToken cancellationToken = default);

    Task<RateLimitResult> PeekAsync(string key, ClientRateLimit rateLimit, CancellationToken cancellationToken = default);
}

/// <summary>
/// Records usage events for later persistence.
/// </summary>
public interface IUsageRecorder
{
    void RecordServiceRequest(string clientId, string serviceId, UsageEventType eventType);

    void RecordAllocationEvent(string clientId, string resourcePoolId, UsageEventType eventType);

    void RecordDenied(string clientId, TargetType targetType, string targetId, UsageDenialCategory category);
}
