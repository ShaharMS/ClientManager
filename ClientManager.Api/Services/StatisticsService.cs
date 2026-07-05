using System.Text.Json;
using ClientManager.Api.Models.Exceptions;
using ClientManager.Api.Services.Interfaces;
using ClientManager.Api.Utils.Extensions;
using ClientManager.DataAccess.Databases.Interfaces;
using ClientManager.DataAccess.Repositories.Interfaces;
using ClientManager.Shared.Contracts.Statistics;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Enums;
using ClientManager.Shared.Models.Requests;
using ClientManager.Shared.Models.Responses;
using ClientManager.Shared.Models.Search;
namespace ClientManager.Api.Services;

/// <summary>
/// Composes dashboard-oriented statistics directly from in-process storage. Read-model projections
/// (overview, summary searches, and entity details) are built from the data-access stores, while
/// usage analytics delegate to the in-process usage statistics service.
/// </summary>
public class StatisticsService : IStatisticsService
{
    private readonly IClientConfigurationDatabase _clientConfigurationDatabase;
    private readonly IEntityRepository<Service> _serviceRepository;
    private readonly IEntityRepository<ResourcePool> _resourcePoolRepository;
    private readonly IResourceAllocationDatabase _allocationDatabase;
    private readonly IGlobalRateLimitDatabase _globalRateLimitDatabase;
    private readonly IUsageStatisticsService _usageStatisticsService;

    /// <summary>
    /// Initializes a new instance of <see cref="StatisticsService"/>.
    /// </summary>
    public StatisticsService(
        IClientConfigurationDatabase clientConfigurationDatabase,
        IEntityRepository<Service> serviceRepository,
        IEntityRepository<ResourcePool> resourcePoolRepository,
        IResourceAllocationDatabase allocationDatabase,
        IGlobalRateLimitDatabase globalRateLimitDatabase,
        IUsageStatisticsService usageStatisticsService)
    {
        _clientConfigurationDatabase = clientConfigurationDatabase;
        _serviceRepository = serviceRepository;
        _resourcePoolRepository = resourcePoolRepository;
        _allocationDatabase = allocationDatabase;
        _globalRateLimitDatabase = globalRateLimitDatabase;
        _usageStatisticsService = usageStatisticsService;
    }

    /// <inheritdoc />
    public async Task<SystemOverviewResponse> GetOverviewAsync(CancellationToken cancellationToken = default)
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

        var activeAllocations = 0;
        foreach (var pool in allPools.Items)
        {
            activeAllocations += await _allocationDatabase.GetActiveCountAsync(pool.Id, cancellationToken);
        }

        return new SystemOverviewResponse(
            (int)allClientsCount,
            (int)enabledClientsCount,
            (int)allServicesCount,
            (int)enabledServicesCount,
            (int)allPools.TotalCount,
            activeAllocations);
    }

    /// <inheritdoc />
    public async Task<SearchResult<ClientSummaryResponse>> SearchClientsAsync(DocumentQuery query, CancellationToken cancellationToken = default)
    {
        var result = await _clientConfigurationDatabase.SearchAsync(query ?? DocumentQuery.All, cancellationToken);
        var items = result.Items.Select(configuration => new ClientSummaryResponse(
            configuration.Id,
            configuration.Name,
            configuration.IsEnabled,
            configuration.Services.Count,
            configuration.ResourcePools.Count,
            configuration.GlobalRateLimit is not null)).ToList();

        return new SearchResult<ClientSummaryResponse>(items, result.TotalCount);
    }

    /// <inheritdoc />
    public async Task<JsonElement> GetClientDetailsAsync(string clientId, CancellationToken cancellationToken = default)
    {
        var configuration = await _clientConfigurationDatabase.GetByIdAsync(clientId, cancellationToken)
            ?? throw DomainErrors.Client(clientId);

        var services = configuration.Services.ToDictionary(
            entry => entry.Key,
            entry => (object)new { isAllowed = entry.Value.IsAllowed, hasRateLimit = entry.Value.RateLimit is not null });

        var resourcePools = new Dictionary<string, object>();
        foreach (var (poolId, poolSettings) in configuration.ResourcePools)
        {
            var activeCount = await _allocationDatabase.GetActiveCountByClientAsync(poolId, clientId, cancellationToken);
            resourcePools[poolId] = new { maxSlots = poolSettings.MaxSlots, activeAllocations = activeCount };
        }

        return JsonSerializer.SerializeToElement(new
        {
            clientId = configuration.Id,
            name = configuration.Name,
            isEnabled = configuration.IsEnabled,
            services,
            resourcePools,
            globalRateLimit = BuildGlobalRateLimit(configuration.GlobalRateLimit)
        });
    }

    /// <inheritdoc />
    public async Task<SearchResult<ServiceStatisticsResponse>> SearchServicesAsync(DocumentQuery query, CancellationToken cancellationToken = default)
    {
        var serviceResult = await _serviceRepository.SearchAsync(query ?? DocumentQuery.All, cancellationToken);
        var clients = await _clientConfigurationDatabase.GetAllAsync(cancellationToken);
        var items = new List<ServiceStatisticsResponse>();

        foreach (var service in serviceResult.Items)
        {
            var clientCount = clients.Count(client => client.Services.ContainsKey(service.Id));
            var globalLimit = await _globalRateLimitDatabase.GetByTargetAsync(service.Id, TargetType.Service, cancellationToken);

            items.Add(new ServiceStatisticsResponse(
                service.Id,
                service.Name,
                service.IsEnabled,
                clientCount,
                globalLimit is not null));
        }

        return new SearchResult<ServiceStatisticsResponse>(items, serviceResult.TotalCount);
    }

    /// <inheritdoc />
    public async Task<JsonElement> GetServiceDetailsAsync(string serviceId, CancellationToken cancellationToken = default)
    {
        var service = await _serviceRepository.GetByIdAsync(serviceId, cancellationToken)
            ?? throw DomainErrors.Service(serviceId);
        var clients = await _clientConfigurationDatabase.GetAllAsync(cancellationToken);
        var globalLimit = await _globalRateLimitDatabase.GetByTargetAsync(serviceId, TargetType.Service, cancellationToken);

        var clientDetails = clients
            .Where(client => client.Services.ContainsKey(serviceId))
            .Select(client => new
            {
                clientId = client.Id,
                name = client.Name,
                isEnabled = client.IsEnabled,
                isAllowed = client.Services[serviceId].IsAllowed,
                hasRateLimit = client.Services[serviceId].RateLimit is not null
            })
            .ToList();

        return JsonSerializer.SerializeToElement(new
        {
            serviceId = service.Id,
            name = service.Name,
            isEnabled = service.IsEnabled,
            clientCount = clientDetails.Count,
            hasGlobalRateLimit = globalLimit is not null,
            clients = clientDetails
        });
    }

    /// <inheritdoc />
    public async Task<SearchResult<ResourcePoolStatisticsResponse>> SearchResourcePoolsAsync(DocumentQuery query, CancellationToken cancellationToken = default)
    {
        var poolResult = await _resourcePoolRepository.SearchAsync(query ?? DocumentQuery.All, cancellationToken);
        var items = new List<ResourcePoolStatisticsResponse>();

        foreach (var pool in poolResult.Items)
        {
            var activeCount = await _allocationDatabase.GetActiveCountAsync(pool.Id, cancellationToken);
            var globalLimit = await _globalRateLimitDatabase.GetByTargetAsync(pool.Id, TargetType.ResourcePool, cancellationToken);

            items.Add(new ResourcePoolStatisticsResponse(
                pool.Id,
                pool.Name,
                (int)pool.MaxSlots,
                activeCount,
                (int)pool.MaxSlots - activeCount,
                globalLimit is not null));
        }

        return new SearchResult<ResourcePoolStatisticsResponse>(items, poolResult.TotalCount);
    }

    /// <inheritdoc />
    public async Task<JsonElement> GetResourcePoolDetailsAsync(string resourcePoolId, CancellationToken cancellationToken = default)
    {
        var pool = await _resourcePoolRepository.GetByIdAsync(resourcePoolId, cancellationToken)
            ?? throw DomainErrors.ResourcePool(resourcePoolId);
        var activeCount = await _allocationDatabase.GetActiveCountAsync(pool.Id, cancellationToken);
        var globalLimit = await _globalRateLimitDatabase.GetByTargetAsync(pool.Id, TargetType.ResourcePool, cancellationToken);
        var clients = await _clientConfigurationDatabase.GetAllAsync(cancellationToken);

        var clientDetails = new List<object>();
        foreach (var client in clients.Where(configuration => configuration.ResourcePools.ContainsKey(resourcePoolId)))
        {
            var clientActiveCount = await _allocationDatabase.GetActiveCountByClientAsync(resourcePoolId, client.Id, cancellationToken);
            clientDetails.Add(new
            {
                clientId = client.Id,
                name = client.Name,
                maxSlots = client.ResourcePools[resourcePoolId].MaxSlots,
                activeAllocations = clientActiveCount
            });
        }

        return JsonSerializer.SerializeToElement(new
        {
            resourcePoolId = pool.Id,
            name = pool.Name,
            maxSlots = (int)pool.MaxSlots,
            activeAllocations = activeCount,
            availableSlots = (int)pool.MaxSlots - activeCount,
            hasGlobalRateLimit = globalLimit is not null,
            clients = clientDetails
        });
    }

    /// <inheritdoc />
    public Task<GlobalUsageStatsResponse> GetGlobalUsageStatsAsync(CancellationToken cancellationToken = default) =>
        _usageStatisticsService.GetGlobalUsageStatsAsync(cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<TargetUsageTimeSeriesResponse>> GetUsageTimeSeriesAsync(
        TargetType filterType,
        IdentifierList targetIds,
        IdentifierList? clientIds,
        DateTime? from,
        DateTime? to,
        BucketGranularity? granularity,
        CancellationToken cancellationToken = default) =>
        _usageStatisticsService.GetUsageTimeSeriesAsync(
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
        _usageStatisticsService.GetClientUsageBreakdownAsync(
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
        var clamped = paging.Clamp();
        var skip = (clamped.Page - 1) * clamped.PageSize;
        var clientPage = await _clientConfigurationDatabase.SearchAsync(
            DocumentQuery.All.WithPagination(skip, clamped.PageSize),
            cancellationToken);

        var rows = new List<ClientSummaryRow>();
        foreach (var client in clientPage.Items)
        {
            var accessibleServices = client.Services.Count(service => service.Value.IsAllowed);
            var totalMaxRequests = client.Services.Values
                .Where(service => service.RateLimit is not null)
                .Sum(service => service.RateLimit!.MaxRequests);
            var accessiblePools = client.ResourcePools.Count;
            var usedSlots = 0;
            var totalAccessibleSlots = 0;

            foreach (var (poolId, poolSettings) in client.ResourcePools)
            {
                totalAccessibleSlots += (int)poolSettings.MaxSlots;
                usedSlots += await _allocationDatabase.GetActiveCountByClientAsync(poolId, client.Id, cancellationToken);
            }

            rows.Add(new ClientSummaryRow(
                client.Id,
                client.Name,
                accessibleServices,
                totalMaxRequests,
                accessiblePools,
                usedSlots,
                totalAccessibleSlots));
        }

        var totalPages = (int)Math.Ceiling((double)clientPage.TotalCount / clamped.PageSize);
        return new PagedResponse<ClientSummaryRow>(
            rows,
            clamped.Page,
            clamped.PageSize,
            (int)Math.Min(int.MaxValue, clientPage.TotalCount),
            totalPages);
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
        _usageStatisticsService.GetHistoricalUsageAsync(
            targetIds.Values,
            filterType,
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
        _usageStatisticsService.GetHistoricalUsageByClientAsync(
            targetIds.Values,
            filterType,
            clientIds.Values,
            from,
            to,
            granularity,
            cancellationToken);

    private static object? BuildGlobalRateLimit(ClientRateLimit? globalRateLimit)
    {
        if (globalRateLimit is null)
        {
            return null;
        }

        return new
        {
            strategy = globalRateLimit.Strategy.ToString(),
            maxRequests = globalRateLimit.MaxRequests,
            windowSeconds = globalRateLimit.Window.TotalSeconds
        };
    }

    private static IEnumerable<string>? ResolveOptionalIds(IdentifierList? identifiers) =>
        identifiers is { HasValues: true } ? identifiers.Values : null;
}
