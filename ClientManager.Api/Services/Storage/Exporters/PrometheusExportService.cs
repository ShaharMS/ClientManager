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
    private readonly IStatisticsPrecomputedDatabase _precomputedDatabase;
    private readonly IEntityRepository<ResourcePool> _poolRepository;
    private readonly IResourceAllocationDatabase _allocationDatabase;

    public PrometheusExportService(
        IStatisticsPrecomputedDatabase precomputedDatabase,
        IEntityRepository<ResourcePool> poolRepository,
        IResourceAllocationDatabase allocationDatabase)
    {
        _precomputedDatabase = precomputedDatabase;
        _poolRepository = poolRepository;
        _allocationDatabase = allocationDatabase;
    }

    public async Task<string> ExportMetricsAsync(CancellationToken cancellationToken = default)
    {
        var builder = new System.Text.StringBuilder();
        var summary = await _precomputedDatabase.GetOverviewSummaryAsync(cancellationToken);
        var requestsPerMinute = summary?.RequestsPerMinute ?? 0;

        builder.AppendLine("# HELP clientmanager_requests_per_minute Estimated requests per minute across all services.");
        builder.AppendLine("# TYPE clientmanager_requests_per_minute gauge");
        builder.AppendLine($"clientmanager_requests_per_minute {requestsPerMinute}");
        builder.AppendLine();

        var gauges = await _precomputedDatabase.GetLatestUsageGaugesAsync(cancellationToken);
        var entries = gauges?.Entries ?? [];

        builder.AppendLine("# HELP clientmanager_requests_total Total granted requests from latest buckets.");
        builder.AppendLine("# TYPE clientmanager_requests_total gauge");
        foreach (var entry in entries)
        {
            builder.AppendLine($"clientmanager_requests_total{{service=\"{EscapeLabel(entry.ServiceId)}\",client=\"{EscapeLabel(entry.ClientId)}\"}} {entry.GrantedCount}");
        }

        builder.AppendLine();
        builder.AppendLine("# HELP clientmanager_denied_total Total denied requests from latest buckets.");
        builder.AppendLine("# TYPE clientmanager_denied_total gauge");
        foreach (var entry in entries)
        {
            builder.AppendLine($"clientmanager_denied_total{{service=\"{EscapeLabel(entry.ServiceId)}\",client=\"{EscapeLabel(entry.ClientId)}\"}} {entry.DeniedCount}");
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

    private static string EscapeLabel(string value) =>
        value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");
}
