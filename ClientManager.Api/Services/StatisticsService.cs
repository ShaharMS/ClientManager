using ClientManager.DataAccess.Databases.Interfaces;
using ClientManager.DataAccess.Repositories.Interfaces;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Requests;
using ClientManager.Shared.Models.Responses;
using ClientManager.Shared.Models.Search;
using ClientManager.Api.Services.Interfaces;

namespace ClientManager.Api.Services;

/// <summary>
/// Composes dashboard statistics from catalog data and precomputed usage documents.
/// </summary>
public sealed class StatisticsService : IStatisticsService
{
    private readonly IClientConfigurationDatabase _clientConfigurationDatabase;
    private readonly IEntityRepository<Service> _serviceRepository;
    private readonly IEntityRepository<ResourcePool> _resourcePoolRepository;
    private readonly IResourceAllocationDatabase _allocationDatabase;
    private readonly IStatisticsPrecomputedDatabase _precomputedDatabase;
    private readonly IStatisticsTimeseriesService _timeseriesService;
    private readonly IStorageReadCache _cache;

    public StatisticsService(
        IClientConfigurationDatabase clientConfigurationDatabase,
        IEntityRepository<Service> serviceRepository,
        IEntityRepository<ResourcePool> resourcePoolRepository,
        IResourceAllocationDatabase allocationDatabase,
        IStatisticsPrecomputedDatabase precomputedDatabase,
        IStatisticsTimeseriesService timeseriesService,
        IStorageReadCache cache)
    {
        _clientConfigurationDatabase = clientConfigurationDatabase;
        _serviceRepository = serviceRepository;
        _resourcePoolRepository = resourcePoolRepository;
        _allocationDatabase = allocationDatabase;
        _precomputedDatabase = precomputedDatabase;
        _timeseriesService = timeseriesService;
        _cache = cache;
    }

    public Task<SystemOverviewResponse> GetOverviewAsync(CancellationToken cancellationToken = default) =>
        _cache.GetOrCreateStatisticsClosedAsync(
            "overview",
            BuildOverviewAsync,
            cancellationToken);

    public Task<TimeseriesSearchResponse> SearchTimeseriesAsync(
        TimeseriesSearchRequest request,
        CancellationToken cancellationToken = default) =>
        _timeseriesService.SearchAsync(request, cancellationToken);

    private async Task<SystemOverviewResponse> BuildOverviewAsync(CancellationToken cancellationToken)
    {
        var enabledClientQuery = new DocumentQuery()
            .Where(nameof(ClientConfiguration.IsEnabled), FilterOperator.Equals, true);

        var allClientsCount = await _clientConfigurationDatabase.CountAsync(DocumentQuery.All, cancellationToken);
        var enabledClientsCount = await _clientConfigurationDatabase.CountAsync(enabledClientQuery, cancellationToken);
        var allServicesCount = await _serviceRepository.CountAsync(DocumentQuery.All, cancellationToken);
        var enabledServicesCount = await _serviceRepository.CountAsync(
            new DocumentQuery().Where(nameof(Service.IsEnabled), FilterOperator.Equals, true),
            cancellationToken);
        var allPools = await _resourcePoolRepository.SearchAsync(DocumentQuery.All, cancellationToken);

        var poolIds = allPools.Items.Select(static pool => pool.Id).ToArray();
        var poolCounts = await _allocationDatabase.GetActiveCountsForPoolsAsync(poolIds, cancellationToken);
        var activeAllocations = poolIds.Sum(poolId => poolCounts.GetValueOrDefault(poolId));

        var summary = await _precomputedDatabase.GetOverviewSummaryAsync(cancellationToken);
        var requestsPerMinute = await _timeseriesService.ComputeServiceRequestsPerMinuteAsync(cancellationToken);

        if (summary is not null)
        {
            return new SystemOverviewResponse(
                (int)allClientsCount,
                (int)enabledClientsCount,
                (int)allServicesCount,
                (int)enabledServicesCount,
                (int)allPools.TotalCount,
                activeAllocations,
                requestsPerMinute,
                summary.TotalPoolSlots,
                summary.AcquiredPoolSlots,
                summary.AcquisitionPercentage);
        }

        var totalSlots = allPools.Items.Sum(pool => (int)pool.MaxSlots);
        var acquiredSlots = activeAllocations;
        var acquisitionPercentage = totalSlots > 0
            ? Math.Round(acquiredSlots * 100.0 / totalSlots, 1)
            : 0;

        return new SystemOverviewResponse(
            (int)allClientsCount,
            (int)enabledClientsCount,
            (int)allServicesCount,
            (int)enabledServicesCount,
            (int)allPools.TotalCount,
            activeAllocations,
            requestsPerMinute,
            totalSlots,
            acquiredSlots,
            acquisitionPercentage);
    }
}
