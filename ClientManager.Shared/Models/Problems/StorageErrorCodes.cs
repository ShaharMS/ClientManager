namespace ClientManager.Shared.Models.Problems;

public static class StorageErrorCodes
{
    public const string ClientNotFound = "client_not_found";
    public const string ServiceNotFound = "service_not_found";
    public const string AccessNotConfigured = "access_not_configured";
    public const string AccessDenied = "access_denied";
    public const string ClientDisabled = "client_disabled";
    public const string ServiceDisabled = "service_disabled";
    public const string ClientRateLimitExceeded = "client_rate_limit_exceeded";
    public const string GlobalServiceRateLimitExceeded = "global_service_rate_limit_exceeded";
    public const string ServiceAlreadyExists = "service_already_exists";
    public const string GlobalRateLimitNotFound = "global_rate_limit_not_found";
    public const string GlobalRateLimitAlreadyExists = "global_rate_limit_already_exists";
    public const string ServiceSettingsNotFound = "service_settings_not_found";
    public const string ClientGlobalRateLimitNotFound = "client_global_rate_limit_not_found";
    public const string StorageUnavailable = "storage_unavailable";
}