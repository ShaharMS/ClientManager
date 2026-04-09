using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Enums;
using ClientManager.Shared.Models.Responses;
using ClientManager.StorageApi.Models.Entities;

namespace ClientManager.StorageApi.Services.Interfaces;

/// <summary>
/// Evaluates deny-by-default access policies for clients against services.
/// </summary>
public interface IAccessControlService
{
    /// <summary>
    /// Checks whether a client may access a service right now.
    /// </summary>
    Task<AccessCheckResponse> CheckAccessAsync(string clientId, string serviceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a full accessibility report for a client across all services.
    /// </summary>
    Task<ClientAccessibilityResponse> GetClientAccessibilityAsync(string clientId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Manages resource allocation acquisition, release, and cleanup.
/// </summary>
public interface IResourceAllocationService
{
    /// <summary>
    /// Acquires a resource slot for a client from a resource pool.
    /// </summary>
    Task<ResourceAcquireResponse> AcquireAsync(string clientId, string resourcePoolId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases a previously acquired resource allocation.
    /// </summary>
    Task<ResourceReleaseResponse> ReleaseAsync(string allocationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cleans up expired allocations.
    /// </summary>
    Task CleanupExpiredAllocationsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Evaluates rate-limit policies at per-client and global scopes.
/// </summary>
public interface IRateLimitService
{
    /// <summary>
    /// Checks and increments the per-client rate limits that apply to a service request.
    /// </summary>
    Task<RateLimitResult> CheckAndIncrementAsync(ClientConfiguration config, string serviceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks the aggregate global rate limit for a service.
    /// </summary>
    Task<RateLimitResult> CheckGlobalServiceLimitAsync(ClientConfiguration config, string serviceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks the aggregate global rate limit for a resource pool.
    /// </summary>
    Task<RateLimitResult> CheckGlobalResourcePoolLimitAsync(ClientConfiguration config, string resourcePoolId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Peeks at rate-limit state without incrementing counters.
    /// </summary>
    Task<RateLimitResult> CheckWithoutIncrementAsync(string clientId, string serviceId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Defines a rate-limit strategy implementation.
/// </summary>
public interface IRateLimitStrategy
{
    /// <summary>
    /// Evaluates and increments the strategy's backing state.
    /// </summary>
    Task<RateLimitResult> EvaluateAsync(string key, ClientRateLimit rateLimit, CancellationToken cancellationToken = default);

    /// <summary>
    /// Evaluates rate-limit state without incrementing it.
    /// </summary>
    Task<RateLimitResult> PeekAsync(string key, ClientRateLimit rateLimit, CancellationToken cancellationToken = default);
}

/// <summary>
/// Records usage events for later persistence.
/// </summary>
public interface IUsageRecorder
{
    /// <summary>
    /// Records a service request event.
    /// </summary>
    void RecordServiceRequest(string clientId, string serviceId, UsageEventType eventType);

    /// <summary>
    /// Records a resource allocation event.
    /// </summary>
    void RecordAllocationEvent(string clientId, string resourcePoolId, UsageEventType eventType);
}