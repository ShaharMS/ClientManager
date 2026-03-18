using ClientManager.Api.Models.Exceptions;
using ClientManager.Api.Models.Responses;
using ClientManager.DataAccess.Interfaces;
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
[Route("api/statistics")]
[Tags("Statistics")]
public class StatisticsController : ControllerBase
{
    private readonly IClientConfigurationRepository _clientConfigRepository;
    private readonly IEntityRepository<Service> _serviceRepository;
    private readonly IEntityRepository<ResourcePool> _poolRepository;
    private readonly IResourceAllocationRepository _allocationRepository;
    private readonly IGlobalRateLimitRepository _globalRateLimitRepository;

    /// <summary>
    /// Initializes a new instance of <see cref="StatisticsController"/>.
    /// </summary>
    /// <param name="clientConfigRepository">Repository for client configurations.</param>
    /// <param name="serviceRepository">Repository for service definitions.</param>
    /// <param name="poolRepository">Repository for resource pool definitions.</param>
    /// <param name="allocationRepository">Repository for resource allocation state.</param>
    /// <param name="globalRateLimitRepository">Repository for global rate limits.</param>
    public StatisticsController(
        IClientConfigurationRepository clientConfigRepository,
        IEntityRepository<Service> serviceRepository,
        IEntityRepository<ResourcePool> poolRepository,
        IResourceAllocationRepository allocationRepository,
        IGlobalRateLimitRepository globalRateLimitRepository)
    {
        _clientConfigRepository = clientConfigRepository;
        _serviceRepository = serviceRepository;
        _poolRepository = poolRepository;
        _allocationRepository = allocationRepository;
        _globalRateLimitRepository = globalRateLimitRepository;
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
        var clients = await _clientConfigRepository.GetAllAsync(cancellationToken);
        var services = await _serviceRepository.GetAllAsync(cancellationToken);
        var pools = await _poolRepository.GetAllAsync(cancellationToken);

        var activeAllocations = 0;
        foreach (var pool in pools)
        {
            activeAllocations += await _allocationRepository.GetActiveCountAsync(pool.Id, cancellationToken);
        }

        return Ok(new SystemOverviewResponse(
            TotalClients: clients.Count,
            EnabledClients: clients.Count(c => c.IsEnabled),
            TotalServices: services.Count,
            EnabledServices: services.Count(s => s.IsEnabled),
            TotalResourcePools: pools.Count,
            ActiveAllocations: activeAllocations));
    }

    /// <summary>
    /// Returns summary statistics for all clients.
    /// </summary>
    /// <returns>A list of per-client summary statistics.</returns>
    /// <response code="200">Returns per-client summaries.</response>
    [HttpGet("clients")]
    [ProducesResponseType(typeof(IReadOnlyList<ClientSummaryResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetClients(CancellationToken cancellationToken)
    {
        var clients = await _clientConfigRepository.GetAllAsync(cancellationToken);

        var summaries = clients.Select(c => new ClientSummaryResponse(
            ClientId: c.Id,
            Name: c.Name,
            IsEnabled: c.IsEnabled,
            ServiceCount: c.Services.Count,
            ResourcePoolCount: c.ResourcePools.Count,
            HasGlobalRateLimit: c.GlobalRateLimit is not null)).ToList();

        return Ok(summaries);
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
        var config = await _clientConfigRepository.GetByIdAsync(clientId, cancellationToken);
        if (config is null)
        {
            throw new NotFoundException($"Client '{clientId}' not found");
        }

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
            var activeCount = await _allocationRepository.GetActiveCountByClientAsync(poolId, clientId, cancellationToken);
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
    /// Returns per-service usage statistics including client counts and global rate limit presence.
    /// </summary>
    /// <returns>A list of per-service statistics.</returns>
    /// <response code="200">Returns per-service statistics.</response>
    [HttpGet("services")]
    [ProducesResponseType(typeof(IReadOnlyList<ServiceStatisticsResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetServices(CancellationToken cancellationToken)
    {
        var services = await _serviceRepository.GetAllAsync(cancellationToken);
        var clients = await _clientConfigRepository.GetAllAsync(cancellationToken);

        var results = new List<ServiceStatisticsResponse>();
        foreach (var service in services)
        {
            var clientCount = clients.Count(c => c.Services.ContainsKey(service.Id));
            var globalLimit = await _globalRateLimitRepository.GetByTargetAsync(
                service.Id, GlobalRateLimitTarget.Service, cancellationToken);

            results.Add(new ServiceStatisticsResponse(
                ServiceId: service.Id,
                Name: service.Name,
                IsEnabled: service.IsEnabled,
                ClientCount: clientCount,
                HasGlobalRateLimit: globalLimit is not null));
        }

        return Ok(results);
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
        var service = await _serviceRepository.GetByIdAsync(serviceId, cancellationToken);
        if (service is null)
        {
            throw new NotFoundException($"Service '{serviceId}' not found");
        }

        var clients = await _clientConfigRepository.GetAllAsync(cancellationToken);
        var globalLimit = await _globalRateLimitRepository.GetByTargetAsync(
            serviceId, GlobalRateLimitTarget.Service, cancellationToken);

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
    /// Returns per-resource-pool utilization statistics including active allocations and available slots.
    /// </summary>
    /// <returns>A list of per-pool statistics.</returns>
    /// <response code="200">Returns per-pool statistics.</response>
    [HttpGet("resource-pools")]
    [ProducesResponseType(typeof(IReadOnlyList<ResourcePoolStatisticsResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetResourcePools(CancellationToken cancellationToken)
    {
        var pools = await _poolRepository.GetAllAsync(cancellationToken);

        var results = new List<ResourcePoolStatisticsResponse>();
        foreach (var pool in pools)
        {
            var activeCount = await _allocationRepository.GetActiveCountAsync(pool.Id, cancellationToken);
            var globalLimit = await _globalRateLimitRepository.GetByTargetAsync(
                pool.Id, GlobalRateLimitTarget.ResourcePool, cancellationToken);

            results.Add(new ResourcePoolStatisticsResponse(
                ResourcePoolId: pool.Id,
                Name: pool.Name,
                MaxSlots: (int)pool.MaxSlots,
                ActiveAllocations: activeCount,
                AvailableSlots: (int)pool.MaxSlots - activeCount,
                HasGlobalRateLimit: globalLimit is not null));
        }

        return Ok(results);
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
        var pool = await _poolRepository.GetByIdAsync(resourcePoolId, cancellationToken);
        if (pool is null)
        {
            throw new NotFoundException($"Resource pool '{resourcePoolId}' not found");
        }

        var activeCount = await _allocationRepository.GetActiveCountAsync(pool.Id, cancellationToken);
        var globalLimit = await _globalRateLimitRepository.GetByTargetAsync(
            pool.Id, GlobalRateLimitTarget.ResourcePool, cancellationToken);

        var clients = await _clientConfigRepository.GetAllAsync(cancellationToken);
        var clientDetails = new List<object>();

        foreach (var client in clients.Where(c => c.ResourcePools.ContainsKey(resourcePoolId)))
        {
            var clientActiveCount = await _allocationRepository.GetActiveCountByClientAsync(
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
}
