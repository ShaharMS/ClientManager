namespace ClientManager.AdminUI.Models.Dashboard;

public record FilterOption(string Label, string Value);

public record ClientUsagePoint(string ClientId, string ClientName, double Value);

public record DashboardDonutData(
    List<ClientUsagePoint> Slices,
    List<ClientUsagePoint> OthersBreakdown);

public record ClientSummaryTableRow(
    string ClientId,
    string DisplayName,
    int AccessibleServices,
    int TotalRateLimitRequests,
    int AccessiblePools,
    int MaxSlots);
