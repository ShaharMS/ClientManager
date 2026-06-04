using ClientManager.Shared.Models.Enums;

namespace ClientManager.Api.Models.Exceptions;

/// <summary>
/// Factory for expected public API failures mapped by <see cref="HttpProblemException"/> base types.
/// </summary>
public static class DomainErrors
{
    public static NotFoundException Client(string clientId) =>
        new($"Client '{clientId}' not found");

    public static NotFoundException Service(string serviceId) =>
        new($"Service '{serviceId}' not found");

    public static NotFoundException ResourcePool(string resourcePoolId) =>
        new($"Resource pool '{resourcePoolId}' not found");

    public static NotFoundException Allocation(string allocationId) =>
        new($"Allocation '{allocationId}' not found");

    public static NotFoundException GlobalRateLimit(string globalRateLimitId) =>
        new($"Global rate limit '{globalRateLimitId}' not found");

    public static NotFoundException ClientGlobalRateLimit(string clientId) =>
        new($"No global rate limit configured for client '{clientId}'");

    public static NotFoundException ServiceSettings(string serviceId, string clientId) =>
        new($"Service settings for '{serviceId}' not found on client '{clientId}'");

    public static NotFoundException ResourcePoolSettings(string resourcePoolId, string clientId) =>
        new($"Resource pool settings for '{resourcePoolId}' not found on client '{clientId}'");

    public static ConflictException DuplicateService(string serviceId) =>
        new($"Service '{serviceId}' already exists");

    public static ConflictException DuplicateResourcePool(string resourcePoolId) =>
        new($"Resource pool '{resourcePoolId}' already exists");

    public static ConflictException DuplicateGlobalRateLimit(string targetId, TargetType targetType) =>
        new($"A global rate limit already exists for {targetType} '{targetId}'");

    public static RateLimitedException ClientRateLimitExceeded(int? retryAfterSeconds = null) =>
        new("Rate limit exceeded", retryAfterSeconds);

    public static RateLimitedException GlobalServiceRateLimitExceeded(int? retryAfterSeconds = null) =>
        new("Global service rate limit exceeded", retryAfterSeconds);

    public static RateLimitedException GlobalResourcePoolRateLimitExceeded(int? retryAfterSeconds = null) =>
        new("Global resource pool rate limit exceeded", retryAfterSeconds);

    public static RateLimitedException ClientSlotLimitReached(string resourcePoolId) =>
        new($"Client slot limit reached for pool '{resourcePoolId}'");

    public static RateLimitedException NoSlotsAvailable(string resourcePoolId) =>
        new($"No slots available in pool '{resourcePoolId}'");
}
