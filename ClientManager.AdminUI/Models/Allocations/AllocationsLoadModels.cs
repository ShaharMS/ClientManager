using ClientManager.AdminUI.Models.Charts;
using ClientManager.AdminUI.Services;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Responses;

namespace ClientManager.AdminUI.Models.Allocations;

public sealed class AllocationsLoadContext
{
    public const string AllPoolsId = "__all__";
    public static readonly TimeSpan RecentWindow = TimeSpan.FromMinutes(5);

    public required string SelectedPoolId { get; init; }
    public required IEnumerable<string>? SelectedClientIds { get; init; }
    public required ChartTimeRange TimeRange { get; init; }
    public required bool IsAccessMetric { get; init; }
    public required List<ClientConfiguration> AllClients { get; init; }
    public int BucketCount { get; init; } = ChartBucketAggregator.DefaultBucketCount;
    public IReadOnlyList<ResourcePoolStatisticsResponse>? KnownPools { get; init; }
}

public sealed record AllocationsLoadResult(
    List<TargetChartData> Charts,
    List<AllocationClientRow> ClientRows,
    List<ResourcePoolStatisticsResponse> Pools,
    List<PoolSummaryRow> PoolSummaryRows);
