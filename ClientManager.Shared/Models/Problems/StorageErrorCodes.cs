namespace ClientManager.Shared.Models.Problems;

public static class StorageErrorCodes
{
    public const string ClientNotFound = "client_not_found";
    public const string ServiceNotFound = "service_not_found";
    public const string ResourcePoolNotFound = "resource_pool_not_found";
    public const string AllocationNotFound = "allocation_not_found";
    public const string AccessNotConfigured = "access_not_configured";
    public const string AccessDenied = "access_denied";
    public const string ClientDisabled = "client_disabled";
    public const string ServiceDisabled = "service_disabled";
    public const string ClientRateLimitExceeded = "client_rate_limit_exceeded";
    public const string GlobalServiceRateLimitExceeded = "global_service_rate_limit_exceeded";
    public const string GlobalResourcePoolRateLimitExceeded = "global_resource_pool_rate_limit_exceeded";
    public const string ClientSlotLimitReached = "client_slot_limit_reached";
    public const string NoSlotsAvailable = "no_slots_available";
    public const string ServiceAlreadyExists = "service_already_exists";
    public const string ResourcePoolAlreadyExists = "resource_pool_already_exists";
    public const string GlobalRateLimitNotFound = "global_rate_limit_not_found";
    public const string GlobalRateLimitAlreadyExists = "global_rate_limit_already_exists";
    public const string ServiceSettingsNotFound = "service_settings_not_found";
    public const string ResourcePoolSettingsNotFound = "resource_pool_settings_not_found";
    public const string ClientGlobalRateLimitNotFound = "client_global_rate_limit_not_found";
}