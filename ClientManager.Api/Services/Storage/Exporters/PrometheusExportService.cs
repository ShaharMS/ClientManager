using System.Text;
using ClientManager.DataAccess.Databases.Interfaces;
using ClientManager.DataAccess.Repositories.Interfaces;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Enums;
using ClientManager.Api.Services.Interfaces;

namespace ClientManager.Api.Services.Storage.Exporters;

/// <summary>
/// Formats usage and allocation data as Prometheus exposition text.
/// </summary>
public class PrometheusExportService : IPrometheusExportService
{
    private readonly IUsageSnapshotDatabase _usageSnapshotDatabase;
    private readonly IEntityRepository<ResourcePool> _poolRepository;
    private readonly IResourceAllocationDatabase _allocationDatabase;
    private readonly IUsageStatisticsService _statisticsService;

    public PrometheusExportService(
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

    public async Task<string> ExportMetricsAsync(CancellationToken cancellationToken = default)
    {
        var builder = new StringBuilder();
        var globalStats = await _statisticsService.GetGlobalUsageStatsAsync(cancellationToken);

        builder.AppendLine("# HELP clientmanager_requests_per_minute Estimated requests per minute across all services.");
        builder.AppendLine("# TYPE clientmanager_requests_per_minute gauge");
        builder.AppendLine($"clientmanager_requests_per_minute {globalStats.RequestsPerMinute}");
        builder.AppendLine();

        var secondSnapshots = await _usageSnapshotDatabase.GetAllByGranularityAsync(BucketGranularity.Second, cancellationToken);
        var serviceSnapshots = secondSnapshots.Where(snapshot => snapshot.TargetType == TargetType.Service);

        if (!serviceSnapshots.Any())
        {
            serviceSnapshots = (await _usageSnapshotDatabase.GetAllByGranularityAsync(BucketGranularity.FiveMinute, cancellationToken))
                .Where(snapshot => snapshot.TargetType == TargetType.Service);
        }

        builder.AppendLine("# HELP clientmanager_requests_total Total granted requests from latest buckets.");
        builder.AppendLine("# TYPE clientmanager_requests_total gauge");
        foreach (var snapshot in serviceSnapshots)
        {
            var latest = snapshot.Buckets.MaxBy(bucket => bucket.Timestamp);
            if (latest is null)
            {
                continue;
            }

            builder.AppendLine($"clientmanager_requests_total{{service=\"{EscapeLabel(snapshot.TargetId)}\",client=\"{EscapeLabel(snapshot.ClientId)}\"}} {latest.GrantedCount}");
        }

        builder.AppendLine();
        builder.AppendLine("# HELP clientmanager_denied_total Total denied requests from latest buckets.");
        builder.AppendLine("# TYPE clientmanager_denied_total gauge");
        foreach (var snapshot in serviceSnapshots)
        {
            var latest = snapshot.Buckets.MaxBy(bucket => bucket.Timestamp);
            if (latest is null)
            {
                continue;
            }

            builder.AppendLine($"clientmanager_denied_total{{service=\"{EscapeLabel(snapshot.TargetId)}\",client=\"{EscapeLabel(snapshot.ClientId)}\"}} {latest.DeniedCount}");
        }

        builder.AppendLine();

        var pools = await _poolRepository.GetAllAsync(cancellationToken);
        var activeCountsByPool = await _allocationDatabase.GetActiveCountsByPoolAsync(cancellationToken);

        builder.AppendLine("# HELP clientmanager_pool_max_slots Maximum slots configured for a resource pool.");
        builder.AppendLine("# TYPE clientmanager_pool_max_slots gauge");
        foreach (var pool in pools)
        {
            builder.AppendLine($"clientmanager_pool_max_slots{{pool=\"{EscapeLabel(pool.Id)}\"}} {pool.MaxSlots}");
        }

        builder.AppendLine();
        builder.AppendLine("# HELP clientmanager_pool_active_slots Currently active resource pool slots.");
        builder.AppendLine("# TYPE clientmanager_pool_active_slots gauge");
        foreach (var pool in pools)
        {
            var activeCount = activeCountsByPool.GetValueOrDefault(pool.Id);
            builder.AppendLine($"clientmanager_pool_active_slots{{pool=\"{EscapeLabel(pool.Id)}\"}} {activeCount}");
        }

        builder.AppendLine();
        return builder.ToString();
    }

    private static string EscapeLabel(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");
    }
}