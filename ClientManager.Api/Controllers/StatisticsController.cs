using Asp.Versioning;
using ClientManager.Api.Models.Exceptions;
using ClientManager.Shared.Models.Search;
using ClientManager.Shared.Models.Requests;
using ClientManager.Shared.Models.Responses;
using ClientManager.Api.Services.Interfaces;
using ClientManager.Api.Utils.Extensions;
using ClientManager.DataAccess.Databases.Interfaces;
using ClientManager.DataAccess.Repositories.Interfaces;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ClientManager.Api.Controllers;

/// <summary>
/// Provides human-readable JSON statistics about system state, including
/// client counts, service usage, and resource pool utilization.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/statistics")]
[Tags("Statistics")]
public class StatisticsController : ControllerBase
{
    private readonly IClientConfigurationDatabase _clientConfigDatabase;
    private readonly IEntityRepository<Service> _serviceRepository;
    private readonly IEntityRepository<ResourcePool> _poolRepository;
    private readonly IResourceAllocationDatabase _allocationDatabase;
    private readonly IGlobalRateLimitDatabase _globalRateLimitDatabase;
    private readonly IStatisticsService _statisticsService;

    /// <summary>
    /// Initializes a new instance of <see cref="StatisticsController"/>.
    /// </summary>
    /// <param name="clientConfigDatabase">Database for client configurations.</param>
    /// <param name="serviceRepository">Repository for service definitions.</param>
    /// <param name="poolRepository">Repository for resource pool definitions.</param>
    /// <param name="allocationDatabase">Database for resource allocation state.</param>
    /// <param name="globalRateLimitDatabase">Database for global rate limits.</param>
    /// <param name="statisticsService">Service for aggregated dashboard statistics.</param>
    public StatisticsController(
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
    /// Returns a high-level system overview with counts of clients, services, pools, and active allocations.
    /// </summary>
    /// <returns>The system overview statistics.</returns>
    /// <response code="200">Returns the system overview.</response>
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
            TotalClients: (int)allClients.TotalCount,
            EnabledClients: (int)enabledClients.TotalCount,
            TotalServices: (int)allServices.TotalCount,
            EnabledServices: (int)enabledServices.TotalCount,
            TotalResourcePools: (int)allPools.TotalCount,
            ActiveAllocations: activeAllocations));
    }

    /// <summary>
    /// Searches client configurations and returns paginated summary statistics.
    /// </summary>
    /// <param name="query">Query with filters, sort, and pagination. Pass an empty body or null for all results.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Matching per-client summary statistics and total count.</returns>
    /// <response code="200">Returns matching per-client summaries.</response>
    [HttpPost("clients/search")]
    [ProducesResponseType(typeof(SearchResult<ClientSummaryResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SearchClients(
        [FromBody] DocumentQuery? query,
        CancellationToken cancellationToken)
    {
        var result = await _clientConfigDatabase.SearchAsync(query ?? DocumentQuery.All, cancellationToken);

        var summaries = result.Items.Select(c => new ClientSummaryResponse(
            ClientId: c.Id,
            Name: c.Name,
            IsEnabled: c.IsEnabled,
            ServiceCount: c.Services.Count,
            ResourcePoolCount: c.ResourcePools.Count,
            HasGlobalRateLimit: c.GlobalRateLimit is not null)).ToList();

        return Ok(new SearchResult<ClientSummaryResponse>(summaries, result.TotalCount));
    }

    /// <summary>
    /// Returns detailed statistics for a specific client, including per-pool active allocation counts.
    /// </summary>
    /// <param name="clientId">The unique identifier of the client.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Detailed client statistics.</returns>
    /// <response code="200">Returns the client's detailed statistics.</response>
    /// <response code="404">No client was found with the given identifier.</response>
    [HttpGet("clients/{clientId}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetClientDetails(string clientId, CancellationToken cancellationToken)
    {
        var config = await _clientConfigDatabase.GetByIdAsync(clientId, cancellationToken) ?? throw new NotFoundException($"Client '{clientId}' not found");
        var services = new Dictionary<string, object>();
        foreach (var (serviceId, settings) in config.Services)
        {
            services[serviceId] = new
            {
                isAllowed = settings.IsAllowed,
                hasRateLimit = settings.RateLimit is not null
            };
        }

        var resourcePools = new Dictionary<string, object>();
        foreach (var (poolId, poolSettings) in config.ResourcePools)
        {
            var activeCount = await _allocationDatabase.GetActiveCountByClientAsync(
                poolId, clientId, cancellationToken);
            resourcePools[poolId] = new
            {
                maxSlots = poolSettings.MaxSlots,
                activeAllocations = activeCount
            };
        }

        object? globalRateLimit = null;
        if (config.GlobalRateLimit is not null)
        {
            globalRateLimit = new
            {
                strategy = config.GlobalRateLimit.Strategy.ToString(),
                maxRequests = config.GlobalRateLimit.MaxRequests,
                windowSeconds = config.GlobalRateLimit.Window.TotalSeconds
            };
        }

        return Ok(new
        {
            clientId = config.Id,
            name = config.Name,
            isEnabled = config.IsEnabled,
            services,
            resourcePools,
            globalRateLimit
        });
    }

    /// <summary>
    /// Searches services and returns paginated per-service usage statistics including client counts and global rate limit presence.
    /// </summary>
    /// <param name="query">Query with filters, sort, and pagination. Pass an empty body or null for all results.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Matching per-service statistics and total count.</returns>
    /// <response code="200">Returns matching per-service statistics.</response>
    [HttpPost("services/search")]
    [ProducesResponseType(typeof(SearchResult<ServiceStatisticsResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SearchServices(
        [FromBody] DocumentQuery? query,
        CancellationToken cancellationToken)
    {
        var serviceResult = await _serviceRepository.SearchAsync(query ?? DocumentQuery.All, cancellationToken);
        var clients = await _clientConfigDatabase.GetAllAsync(cancellationToken);

        var results = new List<ServiceStatisticsResponse>();
        foreach (var service in serviceResult.Items)
        {
            var clientCount = clients.Count(c => c.Services.ContainsKey(service.Id));
            var globalLimit = await _globalRateLimitDatabase.GetByTargetAsync(
                service.Id, TargetType.Service, cancellationToken);

            results.Add(new ServiceStatisticsResponse(
                ServiceId: service.Id,
                Name: service.Name,
                IsEnabled: service.IsEnabled,
                ClientCount: clientCount,
                HasGlobalRateLimit: globalLimit is not null));
        }

        return Ok(new SearchResult<ServiceStatisticsResponse>(results, serviceResult.TotalCount));
    }

    /// <summary>
    /// Returns detailed statistics for a specific service, including which clients have access.
    /// </summary>
    /// <param name="serviceId">The unique identifier of the service.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Detailed service statistics.</returns>
    /// <response code="200">Returns the service's detailed statistics.</response>
    /// <response code="404">No service was found with the given identifier.</response>
    [HttpGet("services/{serviceId}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetServiceDetails(string serviceId, CancellationToken cancellationToken)
    {
        var service = await _serviceRepository.GetByIdAsync(serviceId, cancellationToken) ?? throw new NotFoundException($"Service '{serviceId}' not found");
        var clients = await _clientConfigDatabase.GetAllAsync(cancellationToken);
        var globalLimit = await _globalRateLimitDatabase.GetByTargetAsync(
            serviceId, TargetType.Service, cancellationToken);

        var clientDetails = clients
            .Where(c => c.Services.ContainsKey(serviceId))
            .Select(c => new
            {
                clientId = c.Id,
                name = c.Name,
                isEnabled = c.IsEnabled,
                isAllowed = c.Services[serviceId].IsAllowed,
                hasRateLimit = c.Services[serviceId].RateLimit is not null
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
    /// Searches resource pools and returns paginated per-pool utilization statistics including active allocations and available slots.
    /// </summary>
    /// <param name="query">Query with filters, sort, and pagination. Pass an empty body or null for all results.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Matching per-pool statistics and total count.</returns>
    /// <response code="200">Returns matching per-pool statistics.</response>
    [HttpPost("resource-pools/search")]
    [ProducesResponseType(typeof(SearchResult<ResourcePoolStatisticsResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SearchResourcePools(
        [FromBody] DocumentQuery? query,
        CancellationToken cancellationToken)
    {
        var poolResult = await _poolRepository.SearchAsync(query ?? DocumentQuery.All, cancellationToken);

        var results = new List<ResourcePoolStatisticsResponse>();
        foreach (var pool in poolResult.Items)
        {
            var activeCount = await _allocationDatabase.GetActiveCountAsync(pool.Id, cancellationToken);
            var globalLimit = await _globalRateLimitDatabase.GetByTargetAsync(
                pool.Id, TargetType.ResourcePool, cancellationToken);

            results.Add(new ResourcePoolStatisticsResponse(
                ResourcePoolId: pool.Id,
                Name: pool.Name,
                MaxSlots: (int)pool.MaxSlots,
                ActiveAllocations: activeCount,
                AvailableSlots: (int)pool.MaxSlots - activeCount,
                HasGlobalRateLimit: globalLimit is not null));
        }

        return Ok(new SearchResult<ResourcePoolStatisticsResponse>(results, poolResult.TotalCount));
    }

    /// <summary>
    /// Returns detailed statistics for a specific resource pool, including per-client allocation counts.
    /// </summary>
    /// <param name="resourcePoolId">The unique identifier of the resource pool.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Detailed resource pool statistics.</returns>
    /// <response code="200">Returns the pool's detailed statistics.</response>
    /// <response code="404">No resource pool was found with the given identifier.</response>
    [HttpGet("resource-pools/{resourcePoolId}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetResourcePoolDetails(string resourcePoolId, CancellationToken cancellationToken)
    {
        var pool = await _poolRepository.GetByIdAsync(resourcePoolId, cancellationToken) ?? throw new NotFoundException($"Resource pool '{resourcePoolId}' not found");
        var activeCount = await _allocationDatabase.GetActiveCountAsync(pool.Id, cancellationToken);
        var globalLimit = await _globalRateLimitDatabase.GetByTargetAsync(
            pool.Id, TargetType.ResourcePool, cancellationToken);

        var clients = await _clientConfigDatabase.GetAllAsync(cancellationToken);
        var clientDetails = new List<object>();

        foreach (var client in clients.Where(c => c.ResourcePools.ContainsKey(resourcePoolId)))
        {
            var clientActiveCount = await _allocationDatabase.GetActiveCountByClientAsync(
                resourcePoolId, client.Id, cancellationToken);

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
    /// Retrieves global usage statistics including request rate and pool acquisition.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Global usage statistics.</returns>
    /// <response code="200">Returns global usage statistics.</response>
    [HttpGet("global-usage")]
    [ProducesResponseType(typeof(GlobalUsageStatsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetGlobalUsageStats(CancellationToken cancellationToken)
    {
        var result = await _statisticsService.GetGlobalUsageStatsAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves usage over time for one or more services or resource pools.
    /// </summary>
    /// <param name="filterType">The target type: Service or ResourcePool.</param>
    /// <param name="targetIds">Comma-separated IDs of the services or resource pools.</param>
    /// <param name="clientIds">Optional comma-separated client IDs to filter by.</param>
    /// <param name="from">Optional start of the time range (UTC, ISO 8601).</param>
    /// <param name="to">Optional end of the time range (UTC, ISO 8601).</param>
    /// <param name="granularity">Optional bucket granularity: Second, FiveMinute, Hour, or Day.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Per-target time-series data for usage and capacity.</returns>
    /// <response code="200">Returns per-target usage time-series data.</response>
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
        var targetIdList = ParseIds(targetIds);
        var clientIdList = ParseClientIds(clientIds);
        var result = await _statisticsService.GetUsageTimeSeriesAsync(
            filterType, targetIdList, clientIdList, from, to, granularity, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves per-client usage breakdown for one or more services or resource pools.
    /// </summary>
    /// <param name="filterType">The target type: Service or ResourcePool.</param>
    /// <param name="targetIds">Comma-separated IDs of the services or resource pools.</param>
    /// <param name="clientIds">Optional comma-separated client IDs to filter by.</param>
    /// <param name="from">Optional start of the time range (UTC, ISO 8601).</param>
    /// <param name="to">Optional end of the time range (UTC, ISO 8601).</param>
    /// <param name="granularity">Optional bucket granularity: Second, FiveMinute, Hour, or Day.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Per-target client usage breakdowns.</returns>
    /// <response code="200">Returns per-target client usage breakdowns.</response>
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
        var targetIdList = ParseIds(targetIds);
        var clientIdList = ParseClientIds(clientIds);
        var result = await _statisticsService.GetClientUsageBreakdownAsync(
            filterType, targetIdList, clientIdList, from, to, granularity, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves a paginated summary of all clients with their service and pool access statistics.
    /// </summary>
    /// <param name="paging">Pagination parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paginated client summary data for the dashboard table.</returns>
    /// <response code="200">Returns paginated client summaries.</response>
    [HttpGet("client-summaries")]
    [ProducesResponseType(typeof(PagedResponse<ClientSummaryRow>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetClientSummaries([FromQuery] PagedRequest paging, CancellationToken cancellationToken)
    {
        var result = await _statisticsService.GetClientSummariesAsync(cancellationToken);
        return Ok(result.Rows.ToPagedResponse(paging));
    }

    /// <summary>
    /// Retrieves historical usage data for one or more services or resource pools over a time range.
    /// </summary>
    /// <param name="filterType">The target type: Service or ResourcePool.</param>
    /// <param name="targetIds">Comma-separated IDs of the services or resource pools.</param>
    /// <param name="clientId">Optional: filter to a single client.</param>
    /// <param name="from">Start of the time range (UTC, ISO 8601).</param>
    /// <param name="to">End of the time range (UTC, ISO 8601).</param>
    /// <param name="granularity">Bucket granularity: Second, FiveMinute, Hour, or Day.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Historical usage data points per target within the requested range.</returns>
    /// <response code="200">Returns the historical usage data.</response>
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
        var targetIdList = ParseIds(targetIds);
        var result = await _statisticsService.GetHistoricalUsageAsync(
            targetIdList, filterType, clientId, from, to, granularity, cancellationToken);

        return Ok(result);
    }

    private static IEnumerable<string> ParseIds(string ids)
    {
        return ids.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static IEnumerable<string>? ParseClientIds(string? clientIds)
    {
        if (string.IsNullOrWhiteSpace(clientIds))
            return null;

        return clientIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
