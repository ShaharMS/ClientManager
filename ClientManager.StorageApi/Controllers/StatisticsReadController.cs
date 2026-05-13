using Asp.Versioning;
using ClientManager.DataAccess.Databases.Interfaces;
using ClientManager.DataAccess.Repositories.Interfaces;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Enums;
using ClientManager.Shared.Models.Responses;
using ClientManager.Shared.Models.Search;
using ClientManager.StorageApi.Models.Exceptions;
using ClientManager.StorageApi.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ClientManager.StorageApi.Controllers;

/// <summary>
/// Provides storage-side statistics and read-model queries for the public API.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("internal/v{version:apiVersion}/statistics")]
[Tags("Statistics Reads")]
public class StatisticsReadController : ControllerBase
{
    private readonly IClientConfigurationDatabase _clientConfigDatabase;
    private readonly IEntityRepository<Service> _serviceRepository;
    private readonly IEntityRepository<ResourcePool> _poolRepository;
    private readonly IResourceAllocationDatabase _allocationDatabase;
    private readonly IGlobalRateLimitDatabase _globalRateLimitDatabase;
    private readonly IStatisticsService _statisticsService;

    /// <summary>
    /// Initializes a new instance of <see cref="StatisticsReadController"/>.
    /// </summary>
    public StatisticsReadController(
        IClientConfigurationDatabase clientConfigDatabase,
        IEntityRepository<Service> serviceRepository,
        IEntityRepository<ResourcePool> poolRepository,
        IResourceAllocationDatabase allocationDatabase,
        IGlobalRateLimitDatabase globalRateLimitDatabase,
        IStatisticsService statisticsService)
    {
        _clientConfigDatabase = clientConfigDatabase;
        _serviceRepository = serviceRepository;
        _poolRepository = poolRepository;
        _allocationDatabase = allocationDatabase;
        _globalRateLimitDatabase = globalRateLimitDatabase;
        _statisticsService = statisticsService;
    }

    /// <summary>
    /// Returns the storage-side system overview read model.
    /// </summary>
    [HttpGet("overview")]
    [ProducesResponseType(typeof(SystemOverviewResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOverview(CancellationToken cancellationToken)
    {
        var enabledClientQuery = new DocumentQuery()
            .Where(nameof(ClientConfiguration.IsEnabled), FilterOperator.Equals, true)
            .WithPagination(0, 1);
        var enabledServiceQuery = new DocumentQuery()
            .Where(nameof(Service.IsEnabled), FilterOperator.Equals, true)
            .WithPagination(0, 1);

        var allClients = await _clientConfigDatabase.SearchAsync(DocumentQuery.All.WithPagination(0, 1), cancellationToken);
        var enabledClients = await _clientConfigDatabase.SearchAsync(enabledClientQuery, cancellationToken);
        var allServices = await _serviceRepository.SearchAsync(DocumentQuery.All.WithPagination(0, 1), cancellationToken);
        var enabledServices = await _serviceRepository.SearchAsync(enabledServiceQuery, cancellationToken);
        var allPools = await _poolRepository.SearchAsync(DocumentQuery.All, cancellationToken);

        var activeAllocations = 0;
        foreach (var pool in allPools.Items)
        {
            activeAllocations += await _allocationDatabase.GetActiveCountAsync(pool.Id, cancellationToken);
        }

        return Ok(new SystemOverviewResponse(
            (int)allClients.TotalCount,
            (int)enabledClients.TotalCount,
            (int)allServices.TotalCount,
            (int)enabledServices.TotalCount,
            (int)allPools.TotalCount,
            activeAllocations));
    }

    /// <summary>
    /// Searches client summary read models.
    /// </summary>
    [HttpPost("clients/search")]
    [ProducesResponseType(typeof(SearchResult<ClientSummaryResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SearchClients([FromBody] DocumentQuery? query, CancellationToken cancellationToken)
    {
        var result = await _clientConfigDatabase.SearchAsync(query ?? DocumentQuery.All, cancellationToken);
        var items = result.Items.Select(configuration => new ClientSummaryResponse(
            configuration.Id,
            configuration.Name,
            configuration.IsEnabled,
            configuration.Services.Count,
            configuration.ResourcePools.Count,
            configuration.GlobalRateLimit is not null)).ToList();

        return Ok(new SearchResult<ClientSummaryResponse>(items, result.TotalCount));
    }

    /// <summary>
    /// Returns detailed statistics for a specific client.
    /// </summary>
    [HttpGet("clients/{clientId}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetClientDetails(string clientId, CancellationToken cancellationToken)
    {
        var configuration = await _clientConfigDatabase.GetByIdAsync(clientId, cancellationToken)
            ?? throw new ClientNotFoundException(clientId);

        var services = new Dictionary<string, object>();
        foreach (var (serviceId, settings) in configuration.Services)
        {
            services[serviceId] = new { isAllowed = settings.IsAllowed, hasRateLimit = settings.RateLimit is not null };
        }

        var resourcePools = new Dictionary<string, object>();
        foreach (var (poolId, poolSettings) in configuration.ResourcePools)
        {
            var activeCount = await _allocationDatabase.GetActiveCountByClientAsync(poolId, clientId, cancellationToken);
            resourcePools[poolId] = new { maxSlots = poolSettings.MaxSlots, activeAllocations = activeCount };
        }

        object? globalRateLimit = null;
        if (configuration.GlobalRateLimit is not null)
        {
            globalRateLimit = new
            {
                strategy = configuration.GlobalRateLimit.Strategy.ToString(),
                maxRequests = configuration.GlobalRateLimit.MaxRequests,
                windowSeconds = configuration.GlobalRateLimit.Window.TotalSeconds
            };
        }

        return Ok(new
        {
            clientId = configuration.Id,
            name = configuration.Name,
            isEnabled = configuration.IsEnabled,
            services,
            resourcePools,
            globalRateLimit
        });
    }

    /// <summary>
    /// Searches per-service statistics read models.
    /// </summary>
    [HttpPost("services/search")]
    [ProducesResponseType(typeof(SearchResult<ServiceStatisticsResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SearchServices([FromBody] DocumentQuery? query, CancellationToken cancellationToken)
    {
        var serviceResult = await _serviceRepository.SearchAsync(query ?? DocumentQuery.All, cancellationToken);
        var clients = await _clientConfigDatabase.GetAllAsync(cancellationToken);
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

        return Ok(new SearchResult<ServiceStatisticsResponse>(items, serviceResult.TotalCount));
    }

    /// <summary>
    /// Returns detailed statistics for a specific service.
    /// </summary>
    [HttpGet("services/{serviceId}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetServiceDetails(string serviceId, CancellationToken cancellationToken)
    {
        var service = await _serviceRepository.GetByIdAsync(serviceId, cancellationToken)
            ?? throw new ServiceNotFoundException(serviceId);
        var clients = await _clientConfigDatabase.GetAllAsync(cancellationToken);
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

        return Ok(new
        {
            serviceId = service.Id,
            name = service.Name,
            isEnabled = service.IsEnabled,
            clientCount = clientDetails.Count,
            hasGlobalRateLimit = globalLimit is not null,
            clients = clientDetails
        });
    }

    /// <summary>
    /// Searches per-resource-pool statistics read models.
    /// </summary>
    [HttpPost("resource-pools/search")]
    [ProducesResponseType(typeof(SearchResult<ResourcePoolStatisticsResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SearchResourcePools([FromBody] DocumentQuery? query, CancellationToken cancellationToken)
    {
        var poolResult = await _poolRepository.SearchAsync(query ?? DocumentQuery.All, cancellationToken);
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

        return Ok(new SearchResult<ResourcePoolStatisticsResponse>(items, poolResult.TotalCount));
    }

    /// <summary>
    /// Returns detailed statistics for a specific resource pool.
    /// </summary>
    [HttpGet("resource-pools/{resourcePoolId}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetResourcePoolDetails(string resourcePoolId, CancellationToken cancellationToken)
    {
        var pool = await _poolRepository.GetByIdAsync(resourcePoolId, cancellationToken)
            ?? throw new ResourcePoolNotFoundException(resourcePoolId);
        var activeCount = await _allocationDatabase.GetActiveCountAsync(pool.Id, cancellationToken);
        var globalLimit = await _globalRateLimitDatabase.GetByTargetAsync(pool.Id, TargetType.ResourcePool, cancellationToken);
        var clients = await _clientConfigDatabase.GetAllAsync(cancellationToken);
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

        return Ok(new
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

    /// <summary>
    /// Returns global usage statistics.
    /// </summary>
    [HttpGet("global-usage")]
    [ProducesResponseType(typeof(GlobalUsageStatsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetGlobalUsageStats(CancellationToken cancellationToken)
    {
        return Ok(await _statisticsService.GetGlobalUsageStatsAsync(cancellationToken));
    }

    /// <summary>
    /// Returns usage-over-time data for one or more targets.
    /// </summary>
    [HttpGet("usage-timeseries")]
    [ProducesResponseType(typeof(List<TargetUsageTimeSeriesResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUsageTimeSeries(
        [FromQuery] TargetType filterType,
        [FromQuery] string targetIds,
        [FromQuery] string? clientIds,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] BucketGranularity? granularity,
        CancellationToken cancellationToken)
    {
        var result = await _statisticsService.GetUsageTimeSeriesAsync(
            filterType,
            ParseIds(targetIds),
            ParseClientIds(clientIds),
            from,
            to,
            granularity,
            cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Returns per-client usage breakdown data for one or more targets.
    /// </summary>
    [HttpGet("client-usage-breakdown")]
    [ProducesResponseType(typeof(List<TargetClientUsageBreakdownResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetClientUsageBreakdown(
        [FromQuery] TargetType filterType,
        [FromQuery] string targetIds,
        [FromQuery] string? clientIds,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] BucketGranularity? granularity,
        CancellationToken cancellationToken)
    {
        var result = await _statisticsService.GetClientUsageBreakdownAsync(
            filterType,
            ParseIds(targetIds),
            ParseClientIds(clientIds),
            from,
            to,
            granularity,
            cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Returns dashboard client-summary rows.
    /// </summary>
    [HttpGet("client-summaries")]
    [ProducesResponseType(typeof(ClientSummariesResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetClientSummaries(CancellationToken cancellationToken)
    {
        return Ok(await _statisticsService.GetClientSummariesAsync(cancellationToken));
    }

    /// <summary>
    /// Returns historical usage data for one or more targets.
    /// </summary>
    [HttpGet("historical-usage")]
    [ProducesResponseType(typeof(List<HistoricalUsageResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHistoricalUsage(
        [FromQuery] TargetType filterType,
        [FromQuery] string targetIds,
        [FromQuery] string? clientId,
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] BucketGranularity granularity,
        CancellationToken cancellationToken)
    {
        var result = await _statisticsService.GetHistoricalUsageAsync(
            ParseIds(targetIds),
            filterType,
            clientId,
            from,
            to,
            granularity,
            cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Returns historical usage data for one or more target and client combinations.
    /// </summary>
    /// <param name="filterType">The target type: Service or ResourcePool.</param>
    /// <param name="targetIds">Comma-separated IDs of the services or resource pools.</param>
    /// <param name="clientIds">Comma-separated client IDs included in the response.</param>
    /// <param name="from">Start of the time range (UTC, ISO 8601).</param>
    /// <param name="to">End of the time range (UTC, ISO 8601).</param>
    /// <param name="granularity">Bucket granularity: Second, FiveMinute, Hour, or Day.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Historical usage data points for each requested target-client pair.</returns>
    /// <response code="200">Returns the historical usage data for each requested target-client pair.</response>
    [HttpGet("historical-usage/by-client")]
    [ProducesResponseType(typeof(List<ClientHistoricalUsageResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHistoricalUsageByClient(
        [FromQuery] TargetType filterType,
        [FromQuery] string targetIds,
        [FromQuery] string clientIds,
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] BucketGranularity granularity,
        CancellationToken cancellationToken)
    {
        var result = await _statisticsService.GetHistoricalUsageByClientAsync(
            ParseIds(targetIds),
            filterType,
            ParseIds(clientIds),
            from,
            to,
            granularity,
            cancellationToken);

        return Ok(result);
    }

    private static IEnumerable<string> ParseIds(string ids)
    {
        return ids.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static IEnumerable<string>? ParseClientIds(string? clientIds)
    {
        if (string.IsNullOrWhiteSpace(clientIds))
        {
            return null;
        }

        return clientIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}