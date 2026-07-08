using ClientManager.AdminUI.Models.Charts;
using ClientManager.AdminUI.Services;

namespace ClientManager.AdminUI.Models.Dashboard;

public sealed class DashboardChartLoadContext
{
    public const string AllTargetsId = "__all__";

    public required string SelectedFilterType { get; init; }
    public required string? SelectedTargetId { get; init; }
    public required IEnumerable<string>? SelectedClientIds { get; init; }
    public required ChartTimeRange TimeRange { get; init; }
    public required List<NamedItem> FilterTargets { get; init; }
    public required List<NamedItem> AllServices { get; init; }
    public required List<NamedItem> AllPools { get; init; }
    public required List<NamedItem> Clients { get; init; }
    public int BucketCount { get; init; } = ChartBucketAggregator.DefaultBucketCount;
}
