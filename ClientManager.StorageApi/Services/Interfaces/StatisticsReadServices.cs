using ClientManager.Shared.Models.Enums;
using ClientManager.Shared.Models.Responses;

namespace ClientManager.StorageApi.Services.Interfaces;

/// <summary>
/// Composes dashboard-oriented statistics from storage-owned data.
/// </summary>
public interface IStatisticsService
{
    Task<GlobalUsageStatsResponse> GetGlobalUsageStatsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TargetUsageTimeSeriesResponse>> GetUsageTimeSeriesAsync(
        TargetType targetType,
        IEnumerable<string> targetIds,
        IEnumerable<string>? clientIds,
        DateTime? from = null,
        DateTime? to = null,
        BucketGranularity? granularity = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TargetClientUsageBreakdownResponse>> GetClientUsageBreakdownAsync(
        TargetType targetType,
        IEnumerable<string> targetIds,
        IEnumerable<string>? clientIds,
        DateTime? from = null,
        DateTime? to = null,
        BucketGranularity? granularity = null,
        CancellationToken cancellationToken = default);

    Task<ClientSummariesResponse> GetClientSummariesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HistoricalUsageResponse>> GetHistoricalUsageAsync(
        IEnumerable<string> targetIds,
        TargetType targetType,
        string? clientId,
        DateTime from,
        DateTime to,
        BucketGranularity granularity,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ClientHistoricalUsageResponse>> GetHistoricalUsageByClientAsync(
        IEnumerable<string> targetIds,
        TargetType targetType,
        IEnumerable<string> clientIds,
        DateTime from,
        DateTime to,
        BucketGranularity granularity,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Generates Prometheus exposition output from storage-owned read models.
/// </summary>
public interface IPrometheusExportService
{
    Task<string> ExportMetricsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Generates Grafana-oriented JSON metrics from storage-owned read models.
/// </summary>
public interface IGrafanaExportService
{
    Task<object> ExportMetricsAsync(CancellationToken cancellationToken = default);
}