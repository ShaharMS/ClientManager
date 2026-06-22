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
    int Cap,
    long DeniedLast5Min,
    long DeniedUnauthenticatedCount,
    long DeniedBlockedCount,
    long DeniedRateLimitedCount,
    long DeniedCapacityLimitedCount);
