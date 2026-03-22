using ClientManager.Api.Interfaces;
using ClientManager.Api.Models.Responses;
using ClientManager.DataAccess.Interfaces;
using ClientManager.Shared.Models.Enums;

namespace ClientManager.Api.Services;

/// <summary>
/// Formats usage statistics in OpenMetrics JSON format for Grafana consumption.
/// </summary>
public class GrafanaExportService : IGrafanaExportService
{
    private readonly IUsageSnapshotRepository _usageSnapshotRepository;
    private readonly IEntityRepository<Shared.Models.Entities.ResourcePool> _poolRepository;
    private readonly IResourceAllocationRepository _allocationRepository;
    private readonly IStatisticsService _statisticsService;

    /// <summary>
    /// Initializes a new instance of <see cref="GrafanaExportService"/>.
    /// </summary>
    public GrafanaExportService(
        IUsageSnapshotRepository usageSnapshotRepository,
        IEntityRepository<Shared.Models.Entities.ResourcePool> poolRepository,
        IResourceAllocationRepository allocationRepository,
        IStatisticsService statisticsService)
    {
        _usageSnapshotRepository = usageSnapshotRepository;
        _poolRepository = poolRepository;
        _allocationRepository = allocationRepository;
        _statisticsService = statisticsService;
    }

    /// <inheritdoc />
    public async Task<object> ExportMetricsAsync(CancellationToken cancellationToken = default)
    {
        var metrics = new List<MetricDefinition>();

        // Global RPM gauge
        var globalStats = await _statisticsService.GetGlobalUsageStatsAsync(cancellationToken);
        metrics.Add(new MetricDefinition(
            Name: "clientmanager_requests_per_minute",
            Type: "gauge",
            Help: "Estimated requests per minute across all services.",
            Values: [new MetricValue(null, globalStats.RequestsPerMinute)]
        ));

        // Per-service per-client request/denied counters
        var secondSnapshots = await _usageSnapshotRepository
            .GetAllByGranularityAsync(BucketGranularity.Second, cancellationToken);

        var serviceSnapshots = secondSnapshots
            .Where(s => s.TargetType == GlobalRateLimitTarget.Service)
            .ToList();

        if (serviceSnapshots.Count == 0)
        {
            serviceSnapshots = (await _usageSnapshotRepository
                .GetAllByGranularityAsync(BucketGranularity.FiveMinute, cancellationToken))
                .Where(s => s.TargetType == GlobalRateLimitTarget.Service)
                .ToList();
        }

        var requestsValues = new List<MetricValue>();
        var deniedValues = new List<MetricValue>();

        foreach (var snapshot in serviceSnapshots)
        {
            var latest = snapshot.Buckets.MaxBy(b => b.Timestamp);
            if (latest is null) continue;

            var labels = new Dictionary<string, string>
            {
                ["service"] = snapshot.TargetId,
                ["client"] = snapshot.ClientId
            };

            requestsValues.Add(new MetricValue(labels, latest.GrantedCount));
            deniedValues.Add(new MetricValue(labels, latest.DeniedCount));
        }

        metrics.Add(new MetricDefinition(
            Name: "clientmanager_requests_total",
            Type: "gauge",
            Help: "Total granted requests from latest buckets.",
            Values: requestsValues
        ));

        metrics.Add(new MetricDefinition(
            Name: "clientmanager_denied_total",
            Type: "gauge",
            Help: "Total denied requests from latest buckets.",
            Values: deniedValues
        ));

        // Pool metrics
        var pools = await _poolRepository.GetAllAsync(cancellationToken);
        var activeCountsByPool = await _allocationRepository.GetActiveCountsByPoolAsync(cancellationToken);

        var maxSlotsValues = new List<MetricValue>();
        var activeSlotsValues = new List<MetricValue>();

        foreach (var pool in pools)
        {
            var labels = new Dictionary<string, string> { ["pool"] = pool.Id };
            maxSlotsValues.Add(new MetricValue(labels, pool.MaxSlots));

            var activeCount = activeCountsByPool.GetValueOrDefault(pool.Id);
            activeSlotsValues.Add(new MetricValue(labels, activeCount));
        }

        metrics.Add(new MetricDefinition(
            Name: "clientmanager_pool_max_slots",
            Type: "gauge",
            Help: "Maximum slots configured for a resource pool.",
            Values: maxSlotsValues
        ));

        metrics.Add(new MetricDefinition(
            Name: "clientmanager_pool_active_slots",
            Type: "gauge",
            Help: "Currently active resource pool slots.",
            Values: activeSlotsValues
        ));

        return new GrafanaMetricsResponse(metrics);
    }
}
