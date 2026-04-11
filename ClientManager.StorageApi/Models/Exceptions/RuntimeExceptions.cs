using ClientManager.Shared.Models.Problems;

namespace ClientManager.StorageApi.Models.Exceptions;

/// <summary>
/// Base exception for storage API problem responses.
/// </summary>
public abstract class StorageApiProblemException : Exception
{
    protected StorageApiProblemException(
        string message,
        int statusCode,
        string title,
        string errorCode,
        int? retryAfterSeconds = null)
        : base(message)
    {
        StatusCode = statusCode;
        Title = title;
        ErrorCode = errorCode;
        RetryAfterSeconds = retryAfterSeconds;
    }

    /// <summary>
    /// HTTP status code returned to the caller.
    /// </summary>
    public int StatusCode { get; }

    /// <summary>
    /// Problem-details title returned to the caller.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Stable error code used by internal clients to map the failure.
    /// </summary>
    public string ErrorCode { get; }

    /// <summary>
    /// Optional retry delay exposed on rate-limited responses.
    /// </summary>
    public int? RetryAfterSeconds { get; }
}

/// <summary>
/// Thrown when a client configuration cannot be found.
/// </summary>
public sealed class ClientNotFoundException(string clientId)
    : StorageApiProblemException($"Client '{clientId}' not found", StatusCodes.Status404NotFound, "Not Found", StorageErrorCodes.ClientNotFound);

/// <summary>
/// Thrown when a service definition cannot be found.
/// </summary>
public sealed class ServiceNotFoundException(string serviceId)
    : StorageApiProblemException($"Service '{serviceId}' not found", StatusCodes.Status404NotFound, "Not Found", StorageErrorCodes.ServiceNotFound);

/// <summary>
/// Thrown when a resource pool definition cannot be found.
/// </summary>
public sealed class ResourcePoolNotFoundException(string resourcePoolId)
    : StorageApiProblemException($"Resource pool '{resourcePoolId}' not found", StatusCodes.Status404NotFound, "Not Found", StorageErrorCodes.ResourcePoolNotFound);

/// <summary>
/// Thrown when an allocation cannot be found.
/// </summary>
public sealed class AllocationNotFoundException(string allocationId)
    : StorageApiProblemException($"Allocation '{allocationId}' not found", StatusCodes.Status404NotFound, "Not Found", StorageErrorCodes.AllocationNotFound);

/// <summary>
/// Thrown when a client lacks any access configuration for a service.
/// </summary>
public sealed class AccessNotConfiguredException(string clientId, string serviceId)
    : StorageApiProblemException($"Client '{clientId}' has no access configuration for service '{serviceId}'", StatusCodes.Status401Unauthorized, "Unauthorized", StorageErrorCodes.AccessNotConfigured);

/// <summary>
/// Thrown when access is explicitly denied for a configured service.
/// </summary>
public sealed class AccessDeniedException(string clientId, string serviceId)
    : StorageApiProblemException($"Client '{clientId}' does not have access to service '{serviceId}'", StatusCodes.Status403Forbidden, "Forbidden", StorageErrorCodes.AccessDenied);

/// <summary>
/// Thrown when a client is disabled.
/// </summary>
public sealed class ClientDisabledException(string clientId)
    : StorageApiProblemException($"Client '{clientId}' is disabled", StatusCodes.Status403Forbidden, "Forbidden", StorageErrorCodes.ClientDisabled);

/// <summary>
/// Thrown when a service is disabled.
/// </summary>
public sealed class ServiceDisabledException(string serviceId)
    : StorageApiProblemException($"Service '{serviceId}' is disabled", StatusCodes.Status403Forbidden, "Forbidden", StorageErrorCodes.ServiceDisabled);

/// <summary>
/// Thrown when a client's rate limit has been exceeded.
/// </summary>
public sealed class ClientRateLimitExceededException(int? retryAfterSeconds = null)
    : StorageApiProblemException("Rate limit exceeded", StatusCodes.Status429TooManyRequests, "Too Many Requests", StorageErrorCodes.ClientRateLimitExceeded, retryAfterSeconds);

/// <summary>
/// Thrown when a service-level global rate limit has been exceeded.
/// </summary>
public sealed class GlobalServiceRateLimitExceededException(int? retryAfterSeconds = null)
    : StorageApiProblemException("Global service rate limit exceeded", StatusCodes.Status429TooManyRequests, "Too Many Requests", StorageErrorCodes.GlobalServiceRateLimitExceeded, retryAfterSeconds);

/// <summary>
/// Thrown when a resource-pool-level global rate limit has been exceeded.
/// </summary>
public sealed class GlobalResourcePoolRateLimitExceededException(int? retryAfterSeconds = null)
    : StorageApiProblemException("Global resource pool rate limit exceeded", StatusCodes.Status429TooManyRequests, "Too Many Requests", StorageErrorCodes.GlobalResourcePoolRateLimitExceeded, retryAfterSeconds);

/// <summary>
/// Thrown when a client's per-pool slot cap has been reached.
/// </summary>
public sealed class ClientSlotLimitReachedException(string resourcePoolId)
    : StorageApiProblemException($"Client slot limit reached for pool '{resourcePoolId}'", StatusCodes.Status429TooManyRequests, "Too Many Requests", StorageErrorCodes.ClientSlotLimitReached);

/// <summary>
/// Thrown when a resource pool has no slots available.
/// </summary>
public sealed class NoSlotsAvailableException(string resourcePoolId)
    : StorageApiProblemException($"No slots available in pool '{resourcePoolId}'", StatusCodes.Status429TooManyRequests, "Too Many Requests", StorageErrorCodes.NoSlotsAvailable);