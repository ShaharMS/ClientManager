using ClientManager.Shared.Models.Enums;
using ClientManager.Shared.Models.Problems;

namespace ClientManager.Api.Services.Storage.Models.Exceptions;

/// <summary>
/// Factory for in-process storage failures surfaced as <see cref="StorageApiProblemException"/>.
/// </summary>
public static class StorageDomainErrors
{
    public static StorageApiProblemException ClientNotFound(string clientId) =>
        new($"Client '{clientId}' not found", StatusCodes.Status404NotFound, "Not Found", StorageErrorCodes.ClientNotFound);

    public static StorageApiProblemException ServiceNotFound(string serviceId) =>
        new($"Service '{serviceId}' not found", StatusCodes.Status404NotFound, "Not Found", StorageErrorCodes.ServiceNotFound);

    public static StorageApiProblemException ResourcePoolNotFound(string resourcePoolId) =>
        new($"Resource pool '{resourcePoolId}' not found", StatusCodes.Status404NotFound, "Not Found", StorageErrorCodes.ResourcePoolNotFound);

    public static StorageApiProblemException AllocationNotFound(string allocationId) =>
        new($"Allocation '{allocationId}' not found", StatusCodes.Status404NotFound, "Not Found", StorageErrorCodes.AllocationNotFound);

    public static StorageApiProblemException GlobalRateLimitNotFound(string globalRateLimitId) =>
        new($"Global rate limit '{globalRateLimitId}' not found", StatusCodes.Status404NotFound, "Not Found", StorageErrorCodes.GlobalRateLimitNotFound);

    public static StorageApiProblemException ServiceSettingsNotFound(string clientId, string serviceId) =>
        new($"Service settings for '{serviceId}' not found on client '{clientId}'", StatusCodes.Status404NotFound, "Not Found", StorageErrorCodes.ServiceSettingsNotFound);

    public static StorageApiProblemException ResourcePoolSettingsNotFound(string clientId, string resourcePoolId) =>
        new($"Resource pool settings for '{resourcePoolId}' not found on client '{clientId}'", StatusCodes.Status404NotFound, "Not Found", StorageErrorCodes.ResourcePoolSettingsNotFound);

    public static StorageApiProblemException ClientGlobalRateLimitNotFound(string clientId) =>
        new($"No global rate limit configured for client '{clientId}'", StatusCodes.Status404NotFound, "Not Found", StorageErrorCodes.ClientGlobalRateLimitNotFound);

    public static StorageApiProblemException ServiceAlreadyExists(string serviceId) =>
        new($"Service '{serviceId}' already exists", StatusCodes.Status409Conflict, "Conflict", StorageErrorCodes.ServiceAlreadyExists);

    public static StorageApiProblemException ResourcePoolAlreadyExists(string resourcePoolId) =>
        new($"Resource pool '{resourcePoolId}' already exists", StatusCodes.Status409Conflict, "Conflict", StorageErrorCodes.ResourcePoolAlreadyExists);

    public static StorageApiProblemException GlobalRateLimitAlreadyExists(string targetId, TargetType targetType) =>
        new($"A global rate limit already exists for {targetType} '{targetId}'", StatusCodes.Status409Conflict, "Conflict", StorageErrorCodes.GlobalRateLimitAlreadyExists);

    public static StorageApiProblemException AccessNotConfigured(string clientId, string serviceId) =>
        new($"Client '{clientId}' has no access configuration for service '{serviceId}'", StatusCodes.Status401Unauthorized, "Unauthorized", StorageErrorCodes.AccessNotConfigured);

    public static StorageApiProblemException AccessDenied(string clientId, string serviceId) =>
        new($"Client '{clientId}' does not have access to service '{serviceId}'", StatusCodes.Status403Forbidden, "Forbidden", StorageErrorCodes.AccessDenied);

    public static StorageApiProblemException ClientDisabled(string clientId) =>
        new($"Client '{clientId}' is disabled", StatusCodes.Status403Forbidden, "Forbidden", StorageErrorCodes.ClientDisabled);

    public static StorageApiProblemException ServiceDisabled(string serviceId) =>
        new($"Service '{serviceId}' is disabled", StatusCodes.Status403Forbidden, "Forbidden", StorageErrorCodes.ServiceDisabled);

    public static StorageApiProblemException ClientRateLimitExceeded(int? retryAfterSeconds = null) =>
        new("Rate limit exceeded", StatusCodes.Status429TooManyRequests, "Too Many Requests", StorageErrorCodes.ClientRateLimitExceeded, retryAfterSeconds);

    public static StorageApiProblemException GlobalServiceRateLimitExceeded(int? retryAfterSeconds = null) =>
        new("Global service rate limit exceeded", StatusCodes.Status429TooManyRequests, "Too Many Requests", StorageErrorCodes.GlobalServiceRateLimitExceeded, retryAfterSeconds);

    public static StorageApiProblemException GlobalResourcePoolRateLimitExceeded(int? retryAfterSeconds = null) =>
        new("Global resource pool rate limit exceeded", StatusCodes.Status429TooManyRequests, "Too Many Requests", StorageErrorCodes.GlobalResourcePoolRateLimitExceeded, retryAfterSeconds);

    public static StorageApiProblemException ClientSlotLimitReached(string resourcePoolId) =>
        new($"Client slot limit reached for pool '{resourcePoolId}'", StatusCodes.Status429TooManyRequests, "Too Many Requests", StorageErrorCodes.ClientSlotLimitReached);

    public static StorageApiProblemException NoSlotsAvailable(string resourcePoolId) =>
        new($"No slots available in pool '{resourcePoolId}'", StatusCodes.Status429TooManyRequests, "Too Many Requests", StorageErrorCodes.NoSlotsAvailable);
}
