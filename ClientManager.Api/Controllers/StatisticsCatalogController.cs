using Asp.Versioning;
using ClientManager.Api.Services.Interfaces;
using ClientManager.Shared.Models.Problems;
using ClientManager.Shared.Models.Responses;
using ClientManager.Shared.Models.Search;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ClientManager.Api.Controllers;

/// <summary>
/// Per-service and per-resource-pool statistics search and detail endpoints.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/statistics")]
[Tags("Statistics")]
public class StatisticsCatalogController(IStatisticsService statisticsService) : ControllerBase
{
    /// <summary>
    /// Searches services and returns paginated per-service usage statistics including client counts and global rate limit presence.
    /// </summary>
    /// <param name="query">Query with filters, sort, and pagination. Pass an empty body or null for all results.</param>
    /// <param name="cancellationToken">Token used to cancel the service statistics search before it completes.</param>
    /// <returns>Matching per-service statistics and total count.</returns>
    /// <response code="200">Returns matching per-service statistics.</response>
    /// <response code="503">The storage service is temporarily unavailable.</response>
    [HttpPost("services/search")]
    [ProducesResponseType(typeof(SearchResult<ServiceStatisticsResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> SearchServices(
        [FromBody] DocumentQuery? query,
        CancellationToken cancellationToken)
    {
        var serviceStatistics = await statisticsService.SearchServicesAsync(query ?? DocumentQuery.All, cancellationToken);
        return Ok(serviceStatistics);
    }

    /// <summary>
    /// Returns detailed statistics for a specific service, including which clients have access.
    /// </summary>
    /// <param name="serviceId">The unique identifier of the service.</param>
    /// <param name="cancellationToken">Token used to cancel the service statistics lookup before it completes.</param>
    /// <returns>Detailed service statistics.</returns>
    /// <response code="200">Returns the service's detailed statistics.</response>
    /// <response code="404">No service was found with the given identifier.</response>
    /// <response code="503">The storage service is temporarily unavailable.</response>
    [HttpGet("services/{serviceId}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetServiceDetails(string serviceId, CancellationToken cancellationToken)
    {
        var serviceDetails = await statisticsService.GetServiceDetailsAsync(serviceId, cancellationToken);
        return Ok(serviceDetails);
    }

    /// <summary>
    /// Searches resource pools and returns paginated per-pool utilization statistics including active allocations and available slots.
    /// </summary>
    /// <param name="query">Query with filters, sort, and pagination. Pass an empty body or null for all results.</param>
    /// <param name="cancellationToken">Token used to cancel the resource pool statistics search before it completes.</param>
    /// <returns>Matching per-pool statistics and total count.</returns>
    /// <response code="200">Returns matching per-pool statistics.</response>
    /// <response code="503">The storage service is temporarily unavailable.</response>
    [HttpPost("resource-pools/search")]
    [ProducesResponseType(typeof(SearchResult<ResourcePoolStatisticsResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> SearchResourcePools(
        [FromBody] DocumentQuery? query,
        CancellationToken cancellationToken)
    {
        var resourcePoolStatistics = await statisticsService.SearchResourcePoolsAsync(query ?? DocumentQuery.All, cancellationToken);
        return Ok(resourcePoolStatistics);
    }

    /// <summary>
    /// Returns detailed statistics for a specific resource pool, including per-client allocation counts.
    /// </summary>
    /// <param name="resourcePoolId">The unique identifier of the resource pool.</param>
    /// <param name="cancellationToken">Token used to cancel the resource pool statistics lookup before it completes.</param>
    /// <returns>Detailed resource pool statistics.</returns>
    /// <response code="200">Returns the pool's detailed statistics.</response>
    /// <response code="404">No resource pool was found with the given identifier.</response>
    /// <response code="503">The storage service is temporarily unavailable.</response>
    [HttpGet("resource-pools/{resourcePoolId}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetResourcePoolDetails(string resourcePoolId, CancellationToken cancellationToken)
    {
        var resourcePoolDetails = await statisticsService.GetResourcePoolDetailsAsync(resourcePoolId, cancellationToken);
        return Ok(resourcePoolDetails);
    }
}
