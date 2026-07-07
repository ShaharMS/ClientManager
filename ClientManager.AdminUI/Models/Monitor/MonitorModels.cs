namespace ClientManager.AdminUI.Models.Monitor;

public record ClientOption(string Id, string Name);

public record MonitorClientRow(
    string ClientId,
    string ClientName,
    string ServiceName,
    long GrantedLast5Min,
    long DeniedLast5Min,
    long DeniedUnauthenticatedCount,
    long DeniedBlockedCount,
    long DeniedRateLimitedCount,
    long DeniedCapacityLimitedCount,
    int RateLimitCap);

public record ServiceSummaryRow(
    string Id,
    string Name,
    long CurrentUsage,
    long OffBudgetUsage,
    int Cap,
    bool UsesGlobalCap,
    long DeniedLast5Min,
    long DeniedUnauthenticatedCount,
    long DeniedBlockedCount,
    long DeniedRateLimitedCount,
    long DeniedCapacityLimitedCount)
{
    public long UtilizationUsage => UsesGlobalCap ? CurrentUsage : CurrentUsage + OffBudgetUsage;
}
