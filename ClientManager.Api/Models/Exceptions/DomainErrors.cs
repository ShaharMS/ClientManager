using ClientManager.Shared.Models.Enums;
using ClientManager.Shared.Models.Problems;

namespace ClientManager.Api.Models.Exceptions;

/// <summary>
/// Factory for expected API failures mapped by <see cref="HttpProblemException"/> base types.
/// </summary>
public static class DomainErrors
{
    public static NotFoundException Client(string clientId) =>
        new($"Client '{clientId}' not found", StorageErrorCodes.ClientNotFound);

    public static BadRequestException UnknownClient(string clientId) =>
        new($"Client '{clientId}' not found", StorageErrorCodes.ClientNotFound);

    public static NotFoundException Service(string serviceId) =>
        new($"Service '{serviceId}' not found", StorageErrorCodes.ServiceNotFound);

    public static NotFoundException GlobalRateLimit(string globalRateLimitId) =>
        new($"Global rate limit '{globalRateLimitId}' not found", StorageErrorCodes.GlobalRateLimitNotFound);

    public static NotFoundException ClientGlobalRateLimit(string clientId) =>
        new($"No global rate limit configured for client '{clientId}'", StorageErrorCodes.ClientGlobalRateLimitNotFound);

    public static NotFoundException ServiceSettings(string serviceId, string clientId) =>
        new($"Service settings for '{serviceId}' not found on client '{clientId}'", StorageErrorCodes.ServiceSettingsNotFound);

    public static ConflictException DuplicateService(string serviceId) =>
        new($"Service '{serviceId}' already exists", StorageErrorCodes.ServiceAlreadyExists);

    public static ConflictException DuplicateGlobalRateLimit(string serviceId) =>
        new($"A global rate limit already exists for service '{serviceId}'", StorageErrorCodes.GlobalRateLimitAlreadyExists);

    public static UnauthorizedException AccessNotConfigured(string clientId, string serviceId) =>
        new($"Client '{clientId}' has no access configuration for service '{serviceId}'", StorageErrorCodes.AccessNotConfigured);

    public static ForbiddenException AccessDenied(string clientId, string serviceId) =>
        new($"Client '{clientId}' does not have access to service '{serviceId}'", StorageErrorCodes.AccessDenied);

    public static ForbiddenException ClientDisabled(string clientId) =>
        new($"Client '{clientId}' is disabled", StorageErrorCodes.ClientDisabled);

    public static ForbiddenException ServiceDisabled(string serviceId) =>
        new($"Service '{serviceId}' is disabled", StorageErrorCodes.ServiceDisabled);

    public static RateLimitedException ClientRateLimitExceeded(int? retryAfterSeconds = null) =>
        new("Rate limit exceeded", retryAfterSeconds, StorageErrorCodes.ClientRateLimitExceeded);

    public static RateLimitedException GlobalServiceRateLimitExceeded(int? retryAfterSeconds = null) =>
        new("Global service rate limit exceeded", retryAfterSeconds, StorageErrorCodes.GlobalServiceRateLimitExceeded);

}
