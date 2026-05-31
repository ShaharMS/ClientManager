using System.Text.Json;
using ClientManager.Api.Services.Interfaces;
using ClientManager.Api.Services.Internal.Interfaces;
using ClientManager.Api.Utils.Extensions;
using ClientManager.Shared.Contracts.Statistics;
using ClientManager.Shared.Models.Enums;
using ClientManager.Shared.Models.Requests;
using ClientManager.Shared.Models.Responses;
using ClientManager.Shared.Models.Search;

namespace ClientManager.Api.Services.Implementations;

/// <summary>
/// Adapts public statistics requests onto the storage-facing <see cref="IStatisticsReadClient"/>.
/// Centralizes the optional identifier-list normalization and client-summary pagination that the
/// statistics controller previously owned inline.
/// </summary>
public class StatisticsService : IStatisticsService
{
    private readonly IStatisticsReadClient _statisticsReadClient;

    /// <summary>
    /// Initializes a new instance of <see cref="StatisticsService"/>.
    /// </summary>
    /// <param name="statisticsReadClient">Typed client for the storage-facing statistics endpoints.</param>
    public StatisticsService(IStatisticsReadClient statisticsReadClient)
    {
        _statisticsReadClient = statisticsReadClient;
    }

    /// <inheritdoc />
    public Task<SystemOverviewResponse> GetOverviewAsync(CancellationToken cancellationToken = default) =>
        _statisticsReadClient.GetOverviewAsync(cancellationToken);

    /// <inheritdoc />
    public Task<SearchResult<ClientSummaryResponse>> SearchClientsAsync(DocumentQuery query, CancellationToken cancellationToken = default) =>
        _statisticsReadClient.SearchClientSummariesAsync(query, cancellationToken);

    /// <inheritdoc />
    public Task<JsonElement> GetClientDetailsAsync(string clientId, CancellationToken cancellationToken = default) =>
        _statisticsReadClient.GetClientDetailsAsync(clientId, cancellationToken);

    /// <inheritdoc />
    public Task<SearchResult<ServiceStatisticsResponse>> SearchServicesAsync(DocumentQuery query, CancellationToken cancellationToken = default) =>
        _statisticsReadClient.SearchServiceStatisticsAsync(query, cancellationToken);

    /// <inheritdoc />
    public Task<JsonElement> GetServiceDetailsAsync(string serviceId, CancellationToken cancellationToken = default) =>
        _statisticsReadClient.GetServiceDetailsAsync(serviceId, cancellationToken);

    /// <inheritdoc />
    public Task<SearchResult<ResourcePoolStatisticsResponse>> SearchResourcePoolsAsync(DocumentQuery query, CancellationToken cancellationToken = default) =>
        _statisticsReadClient.SearchResourcePoolStatisticsAsync(query, cancellationToken);

    /// <inheritdoc />
    public Task<JsonElement> GetResourcePoolDetailsAsync(string resourcePoolId, CancellationToken cancellationToken = default) =>
        _statisticsReadClient.GetResourcePoolDetailsAsync(resourcePoolId, cancellationToken);

    /// <inheritdoc />
    public Task<GlobalUsageStatsResponse> GetGlobalUsageStatsAsync(CancellationToken cancellationToken = default) =>
        _statisticsReadClient.GetGlobalUsageStatsAsync(cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<TargetUsageTimeSeriesResponse>> GetUsageTimeSeriesAsync(
        TargetType filterType,
        IdentifierList targetIds,
        IdentifierList? clientIds,
        DateTime? from,
        DateTime? to,
        BucketGranularity? granularity,
        CancellationToken cancellationToken = default) =>
        _statisticsReadClient.GetUsageTimeSeriesAsync(
            filterType,
            targetIds.Values,
            ResolveOptionalIds(clientIds),
            from,
            to,
            granularity,
            cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<TargetClientUsageBreakdownResponse>> GetClientUsageBreakdownAsync(
        TargetType filterType,
        IdentifierList targetIds,
        IdentifierList? clientIds,
        DateTime? from,
        DateTime? to,
        BucketGranularity? granularity,
        CancellationToken cancellationToken = default) =>
        _statisticsReadClient.GetClientUsageBreakdownAsync(
            filterType,
            targetIds.Values,
            ResolveOptionalIds(clientIds),
            from,
            to,
            granularity,
            cancellationToken);

    /// <inheritdoc />
    public async Task<PagedResponse<ClientSummaryRow>> GetClientSummariesAsync(PagedRequest paging, CancellationToken cancellationToken = default)
    {
        var summaries = await _statisticsReadClient.GetClientSummariesAsync(cancellationToken);
        return summaries.Rows.ToPagedResponse(paging);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<HistoricalUsageResponse>> GetHistoricalUsageAsync(
        TargetType filterType,
        IdentifierList targetIds,
        string? clientId,
        DateTime from,
        DateTime to,
        BucketGranularity granularity,
        CancellationToken cancellationToken = default) =>
        _statisticsReadClient.GetHistoricalUsageAsync(
            filterType,
            targetIds.Values,
            clientId,
            from,
            to,
            granularity,
            cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<ClientHistoricalUsageResponse>> GetHistoricalUsageByClientAsync(
        TargetType filterType,
        IdentifierList targetIds,
        IdentifierList clientIds,
        DateTime from,
        DateTime to,
        BucketGranularity granularity,
        CancellationToken cancellationToken = default) =>
        _statisticsReadClient.GetHistoricalUsageByClientAsync(
            filterType,
            targetIds.Values,
            clientIds.Values,
            from,
            to,
            granularity,
            cancellationToken);

    private static IEnumerable<string>? ResolveOptionalIds(IdentifierList? identifiers) =>
        identifiers is { HasValues: true } ? identifiers.Values : null;
}
