namespace ClientManager.Shared.Models.Problems;

/// <summary>
/// Machine-readable error codes placed on <see cref="ProblemResponse.ErrorCode"/> for access,
/// catalog, and storage failures.
/// </summary>
/// <remarks>
/// Values are stable contract identifiers consumed by the Admin UI and gateways for localization
/// and routing decisions.
/// </remarks>
public static class StorageErrorCodes
{
    /// <summary>No client configuration exists for the requested identifier.</summary>
    public const string ClientNotFound = "client_not_found";

    /// <summary>No service definition exists for the requested identifier.</summary>
    public const string ServiceNotFound = "service_not_found";

    /// <summary>The client has no configured relationship with the requested service.</summary>
    public const string AccessNotConfigured = "access_not_configured";

    /// <summary>The client-service relationship exists but access is explicitly denied.</summary>
    public const string AccessDenied = "access_denied";

    /// <summary>The client configuration is disabled.</summary>
    public const string ClientDisabled = "client_disabled";

    /// <summary>The service definition is disabled.</summary>
    public const string ServiceDisabled = "service_disabled";

    /// <summary>A per-client rate limit was exceeded.</summary>
    public const string ClientRateLimitExceeded = "client_rate_limit_exceeded";

    /// <summary>The global per-service rate limit was exceeded.</summary>
    public const string GlobalServiceRateLimitExceeded = "global_service_rate_limit_exceeded";

    /// <summary>A service with the same identifier already exists.</summary>
    public const string ServiceAlreadyExists = "service_already_exists";

    /// <summary>No global rate limit exists for the requested service.</summary>
    public const string GlobalRateLimitNotFound = "global_rate_limit_not_found";

    /// <summary>A global rate limit already exists for the requested service.</summary>
    public const string GlobalRateLimitAlreadyExists = "global_rate_limit_already_exists";

    /// <summary>Expected service access settings were missing from a client configuration.</summary>
    public const string ServiceSettingsNotFound = "service_settings_not_found";

    /// <summary>The client has no configured global rate-limit policy.</summary>
    public const string ClientGlobalRateLimitNotFound = "client_global_rate_limit_not_found";

    /// <summary>The storage provider is temporarily unavailable.</summary>
    public const string StorageUnavailable = "storage_unavailable";
}
