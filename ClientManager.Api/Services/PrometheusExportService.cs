using System.Text;
using ClientManager.Api.Interfaces;
using ClientManager.DataAccess.Interfaces;
using ClientManager.Shared.Models.Enums;

namespace ClientManager.Api.Services;

/// <summary>
/// Formats usage statistics in Prometheus exposition format (text/plain; version=0.0.4).
/// </summary>
public class PrometheusExportService : IPrometheusExportService
{
    private readonly IUsageSnapshotRepository _usageSnapshotRepository;
    private readonly IEntityRepository<Shared.Models.Entities.ResourcePool> _poolRepository;
    private readonly IResourceAllocationRepository _allocationRepository;
    private readonly IStatisticsService _statisticsService;

    /// <summary>
    /// Initializes a new instance of <see cref="PrometheusExportService"/>.
    /// </summary>
    public PrometheusExportService(
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
    public async Task<string> ExportMetricsAsync(CancellationToken cancellationToken = default)
    {
        var sb = new StringBuilder();

        // Global RPM gauge
        var globalStats = await _statisticsService.GetGlobalUsageStatsAsync(cancellationToken);
        sb.AppendLine("# HELP clientmanager_requests_per_minute Estimated requests per minute across all services.");
        sb.AppendLine("# TYPE clientmanager_requests_per_minute gauge");
        sb.AppendLine($"clientmanager_requests_per_minute {globalStats.RequestsPerMinute}");
        sb.AppendLine();

        // Per-service per-client request/denied counters from latest second buckets (or 5-min fallback)
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

        sb.AppendLine("# HELP clientmanager_requests_total Total granted requests from latest buckets.");
        sb.AppendLine("# TYPE clientmanager_requests_total gauge");
        foreach (var snapshot in serviceSnapshots)
        {
            var latest = snapshot.Buckets.MaxBy(b => b.Timestamp);
            if (latest is null) continue;
            sb.AppendLine($"clientmanager_requests_total{{service=\"{EscapeLabel(snapshot.TargetId)}\",client=\"{EscapeLabel(snapshot.ClientId)}\"}} {latest.GrantedCount}");
        }
        sb.AppendLine();

        sb.AppendLine("# HELP clientmanager_denied_total Total denied requests from latest buckets.");
        sb.AppendLine("# TYPE clientmanager_denied_total gauge");
        foreach (var snapshot in serviceSnapshots)
        {
            var latest = snapshot.Buckets.MaxBy(b => b.Timestamp);
            if (latest is null) continue;
            sb.AppendLine($"clientmanager_denied_total{{service=\"{EscapeLabel(snapshot.TargetId)}\",client=\"{EscapeLabel(snapshot.ClientId)}\"}} {latest.DeniedCount}");
        }
        sb.AppendLine();

        // Pool metrics
        var pools = await _poolRepository.GetAllAsync(cancellationToken);

        sb.AppendLine("# HELP clientmanager_pool_max_slots Maximum slots configured for a resource pool.");
        sb.AppendLine("# TYPE clientmanager_pool_max_slots gauge");
        foreach (var pool in pools)
        {
            sb.AppendLine($"clientmanager_pool_max_slots{{pool=\"{EscapeLabel(pool.Id)}\"}} {pool.MaxSlots}");
        }
        sb.AppendLine();

        sb.AppendLine("# HELP clientmanager_pool_active_slots Currently active resource pool slots.");
        sb.AppendLine("# TYPE clientmanager_pool_active_slots gauge");
        foreach (var pool in pools)
        {
            var activeCount = await _allocationRepository.GetActiveCountAsync(pool.Id, cancellationToken);
            sb.AppendLine($"clientmanager_pool_active_slots{{pool=\"{EscapeLabel(pool.Id)}\"}} {activeCount}");
        }
        sb.AppendLine();

        return sb.ToString();
    }

    private static string EscapeLabel(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");
    }
}
