namespace ClientManager.AdminUI.Models.Allocations;

public record MetricOption(string Value, string Name);

public record AllocationClientRow(
    string ClientId,
    string ClientName,
    string PoolName,
    long CurrentValue,
    int CapValue,
    long DeniedLast5Min,
    long DeniedUnauthenticatedCount,
    long DeniedBlockedCount,
    long DeniedRateLimitedCount,
    long DeniedCapacityLimitedCount);

public record PoolSummaryRow(
    string PoolId,
    string Name,
    long CurrentValue,
    int CapValue,
    long? RemainingValue,
    long DeniedLast5Min,
    long DeniedUnauthenticatedCount,
    long DeniedBlockedCount,
    long DeniedRateLimitedCount,
    long DeniedCapacityLimitedCount);
