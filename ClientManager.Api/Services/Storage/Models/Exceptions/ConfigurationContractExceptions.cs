using ClientManager.Shared.Models.Enums;
using ClientManager.Shared.Models.Problems;

namespace ClientManager.Api.Services.Storage.Models.Exceptions;

public sealed class ServiceAlreadyExistsException(string serviceId)
    : StorageApiProblemException($"Service '{serviceId}' already exists", StatusCodes.Status409Conflict, "Conflict", StorageErrorCodes.ServiceAlreadyExists);

public sealed class ResourcePoolAlreadyExistsException(string resourcePoolId)
    : StorageApiProblemException($"Resource pool '{resourcePoolId}' already exists", StatusCodes.Status409Conflict, "Conflict", StorageErrorCodes.ResourcePoolAlreadyExists);

public sealed class GlobalRateLimitNotFoundException(string globalRateLimitId)
    : StorageApiProblemException($"Global rate limit '{globalRateLimitId}' not found", StatusCodes.Status404NotFound, "Not Found", StorageErrorCodes.GlobalRateLimitNotFound);

public sealed class GlobalRateLimitAlreadyExistsException(string targetId, TargetType targetType)
    : StorageApiProblemException($"A global rate limit already exists for {targetType} '{targetId}'", StatusCodes.Status409Conflict, "Conflict", StorageErrorCodes.GlobalRateLimitAlreadyExists);

public sealed class ServiceSettingsNotFoundException(string clientId, string serviceId)
    : StorageApiProblemException($"Service settings for '{serviceId}' not found on client '{clientId}'", StatusCodes.Status404NotFound, "Not Found", StorageErrorCodes.ServiceSettingsNotFound);

public sealed class ResourcePoolSettingsNotFoundException(string clientId, string resourcePoolId)
    : StorageApiProblemException($"Resource pool settings for '{resourcePoolId}' not found on client '{clientId}'", StatusCodes.Status404NotFound, "Not Found", StorageErrorCodes.ResourcePoolSettingsNotFound);

public sealed class ClientGlobalRateLimitNotFoundException(string clientId)
    : StorageApiProblemException($"No global rate limit configured for client '{clientId}'", StatusCodes.Status404NotFound, "Not Found", StorageErrorCodes.ClientGlobalRateLimitNotFound);