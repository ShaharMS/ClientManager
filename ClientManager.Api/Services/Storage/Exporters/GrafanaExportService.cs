using ClientManager.DataAccess.Databases.Interfaces;
using ClientManager.DataAccess.Repositories.Interfaces;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Enums;
using ClientManager.Shared.Models.Responses;
using ClientManager.Api.Services.Interfaces;

namespace ClientManager.Api.Services.Storage.Exporters;

/// <summary>
/// Formats usage and allocation metrics as Grafana JSON payloads.
/// </summary>
public class GrafanaExportService : IGrafanaExportService
{
    private readonly IUsageSnapshotDatabase _usageSnapshotDatabase;
    private readonly IEntityRepository<ResourcePool> _poolRepository;
    private readonly IResourceAllocationDatabase _allocationDatabase;
    private readonly IUsageStatisticsService _statisticsService;

    public GrafanaExportService(
        IUsageSnapshotDatabase usageSnapshotDatabase,
        IEntityRepository<ResourcePool> poolRepository,
        IResourceAllocationDatabase allocationDatabase,
        IUsageStatisticsService statisticsService)
    {
        _usageSnapshotDatabase = usageSnapshotDatabase;
        _poolRepository = poolRepository;
        _allocationDatabase = allocationDatabase;
        _statisticsService = statisticsService;
    }

    public async Task<object> ExportMetricsAsync(CancellationToken cancellationToken = default)
    {
        var metrics = new List<MetricDefinition>();
        var globalStats = await _statisticsService.GetGlobalUsageStatsAsync(cancellationToken);

        metrics.Add(new MetricDefinition(
            "clientmanager_requests_per_minute",
            "gauge",
            "Estimated requests per minute across all services.",
            [new MetricValue(null, globalStats.RequestsPerMinute)]));

        var secondSnapshots = await _usageSnapshotDatabase.GetAllByGranularityAsync(BucketGranularity.Second, cancellationToken);
        var serviceSnapshots = secondSnapshots.Where(snapshot => snapshot.TargetType == TargetType.Service);

        if (!serviceSnapshots.Any())
        {
            serviceSnapshots = (await _usageSnapshotDatabase.GetAllByGranularityAsync(BucketGranularity.FiveMinute, cancellationToken))
                .Where(snapshot => snapshot.TargetType == TargetType.Service);
        }

        var requestValues = new List<MetricValue>();
        var deniedValues = new List<MetricValue>();

        foreach (var snapshot in serviceSnapshots)
        {
            var latest = snapshot.Buckets.MaxBy(bucket => bucket.Timestamp);
            if (latest is null)
            {
                continue;
            }

            var labels = new Dictionary<string, string>
            {
                ["service"] = snapshot.TargetId,
                ["client"] = snapshot.ClientId
            };

            requestValues.Add(new MetricValue(labels, latest.GrantedCount));
            deniedValues.Add(new MetricValue(labels, latest.DeniedCount));
        }

        metrics.Add(new MetricDefinition(
            "clientmanager_requests_total",
            "gauge",
            "Total granted requests from latest buckets.",
            requestValues));

        metrics.Add(new MetricDefinition(
            "clientmanager_denied_total",
            "gauge",
            "Total denied requests from latest buckets.",
            deniedValues));

        var pools = await _poolRepository.GetAllAsync(cancellationToken);
        var activeCountsByPool = await _allocationDatabase.GetActiveCountsByPoolAsync(cancellationToken);
        var maxSlotValues = new List<MetricValue>();
        var activeSlotValues = new List<MetricValue>();

        foreach (var pool in pools)
        {
            var labels = new Dictionary<string, string> { ["pool"] = pool.Id };
            maxSlotValues.Add(new MetricValue(labels, pool.MaxSlots));
            activeSlotValues.Add(new MetricValue(labels, activeCountsByPool.GetValueOrDefault(pool.Id)));
        }

        metrics.Add(new MetricDefinition(
            "clientmanager_pool_max_slots",
            "gauge",
            "Maximum slots configured for a resource pool.",
            maxSlotValues));

        metrics.Add(new MetricDefinition(
            "clientmanager_pool_active_slots",
            "gauge",
            "Currently active resource pool slots.",
            activeSlotValues));

        return new GrafanaMetricsResponse(metrics);
    }
}