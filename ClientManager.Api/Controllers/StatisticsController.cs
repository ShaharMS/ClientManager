using Asp.Versioning;
using ClientManager.Api.Services.Interfaces;
using ClientManager.Shared.Contracts.Statistics;
using ClientManager.Shared.Models.Search;
using ClientManager.Shared.Models.Requests;
using ClientManager.Shared.Models.Responses;
using ClientManager.Shared.Models.Enums;
using ClientManager.Shared.Models.Problems;
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
    private readonly IStatisticsService _statisticsService;

    /// <summary>
    /// Initializes a new instance of <see cref="StatisticsController"/>.
    /// </summary>
    /// <param name="statisticsService">The statistics read service.</param>
    public StatisticsController(IStatisticsService statisticsService)
    {
        _statisticsService = statisticsService;
    }

    /// <summary>
    /// Returns a high-level system overview with counts of clients, services, pools, and active allocations.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the overview aggregation before it completes.</param>
    /// <returns>The system overview statistics.</returns>
    /// <response code="200">Returns the system overview.</response>
    /// <response code="503">The storage service is temporarily unavailable.</response>
    [HttpGet("overview")]
    [ProducesResponseType(typeof(SystemOverviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetOverview(CancellationToken cancellationToken)
    {
        var overview = await _statisticsService.GetOverviewAsync(cancellationToken);
        return Ok(overview);
    }

    /// <summary>
    /// Searches client configurations and returns paginated summary statistics.
    /// </summary>
    /// <param name="query">Query with filters, sort, and pagination. Pass an empty body or null for all results.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Matching per-client summary statistics and total count.</returns>
    /// <response code="200">Returns matching per-client summaries.</response>
    /// <response code="503">The storage service is temporarily unavailable.</response>
    [HttpPost("clients/search")]
    [ProducesResponseType(typeof(SearchResult<ClientSummaryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> SearchClients(
        [FromBody] DocumentQuery? query,
        CancellationToken cancellationToken)
    {
        var clientSummaries = await _statisticsService.SearchClientsAsync(query ?? DocumentQuery.All, cancellationToken);
        return Ok(clientSummaries);
    }

    /// <summary>
    /// Returns detailed statistics for a specific client, including per-pool active allocation counts.
    /// </summary>
    /// <param name="clientId">The unique identifier of the client.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Detailed client statistics.</returns>
    /// <response code="200">Returns the client's detailed statistics.</response>
    /// <response code="404">No client was found with the given identifier.</response>
    /// <response code="503">The storage service is temporarily unavailable.</response>
    [HttpGet("clients/{clientId}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetClientDetails(string clientId, CancellationToken cancellationToken)
    {
        var clientDetails = await _statisticsService.GetClientDetailsAsync(clientId, cancellationToken);
        return Ok(clientDetails);
    }

    /// <summary>
    /// Searches services and returns paginated per-service usage statistics including client counts and global rate limit presence.
    /// </summary>
    /// <param name="query">Query with filters, sort, and pagination. Pass an empty body or null for all results.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
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
        var serviceStatistics = await _statisticsService.SearchServicesAsync(query ?? DocumentQuery.All, cancellationToken);
        return Ok(serviceStatistics);
    }

    /// <summary>
    /// Returns detailed statistics for a specific service, including which clients have access.
    /// </summary>
    /// <param name="serviceId">The unique identifier of the service.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
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
        var serviceDetails = await _statisticsService.GetServiceDetailsAsync(serviceId, cancellationToken);
        return Ok(serviceDetails);
    }

    /// <summary>
    /// Searches resource pools and returns paginated per-pool utilization statistics including active allocations and available slots.
    /// </summary>
    /// <param name="query">Query with filters, sort, and pagination. Pass an empty body or null for all results.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
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
        var resourcePoolStatistics = await _statisticsService.SearchResourcePoolsAsync(query ?? DocumentQuery.All, cancellationToken);
        return Ok(resourcePoolStatistics);
    }

    /// <summary>
    /// Returns detailed statistics for a specific resource pool, including per-client allocation counts.
    /// </summary>
    /// <param name="resourcePoolId">The unique identifier of the resource pool.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
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
        var resourcePoolDetails = await _statisticsService.GetResourcePoolDetailsAsync(resourcePoolId, cancellationToken);
        return Ok(resourcePoolDetails);
    }

    /// <summary>
    /// Retrieves global usage statistics including request rate and pool acquisition.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Global usage statistics.</returns>
    /// <response code="200">Returns global usage statistics.</response>
    /// <response code="503">The storage service is temporarily unavailable.</response>
    [HttpGet("global-usage")]
    [ProducesResponseType(typeof(GlobalUsageStatsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetGlobalUsageStats(CancellationToken cancellationToken)
    {
        var globalUsage = await _statisticsService.GetGlobalUsageStatsAsync(cancellationToken);
        return Ok(globalUsage);
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
    /// <response code="503">The storage service is temporarily unavailable.</response>
    [HttpGet("usage-timeseries")]
    [ProducesResponseType(typeof(List<TargetUsageTimeSeriesResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetUsageTimeSeries(
        [FromQuery] TargetType filterType,
        [FromQuery] IdentifierList targetIds,
        [FromQuery] IdentifierList? clientIds,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] BucketGranularity? granularity,
        CancellationToken cancellationToken)
    {
        var usageTimeSeries = await _statisticsService.GetUsageTimeSeriesAsync(
            filterType,
            targetIds,
            clientIds,
            from,
            to,
            granularity,
            cancellationToken);
        return Ok(usageTimeSeries);
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
    /// <response code="503">The storage service is temporarily unavailable.</response>
    [HttpGet("client-usage-breakdown")]
    [ProducesResponseType(typeof(List<TargetClientUsageBreakdownResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetClientUsageBreakdown(
        [FromQuery] TargetType filterType,
        [FromQuery] IdentifierList targetIds,
        [FromQuery] IdentifierList? clientIds,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] BucketGranularity? granularity,
        CancellationToken cancellationToken)
    {
        var clientUsageBreakdown = await _statisticsService.GetClientUsageBreakdownAsync(
            filterType,
            targetIds,
            clientIds,
            from,
            to,
            granularity,
            cancellationToken);
        return Ok(clientUsageBreakdown);
    }

    /// <summary>
    /// Retrieves a paginated summary of all clients with their service and pool access statistics.
    /// </summary>
    /// <param name="paging">Pagination parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paginated client summary data for the dashboard table.</returns>
    /// <response code="200">Returns paginated client summaries.</response>
    /// <response code="503">The storage service is temporarily unavailable.</response>
    [HttpGet("client-summaries")]
    [ProducesResponseType(typeof(PagedResponse<ClientSummaryRow>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetClientSummaries([FromQuery] PagedRequest paging, CancellationToken cancellationToken)
    {
        var clientSummaries = await _statisticsService.GetClientSummariesAsync(paging, cancellationToken);
        return Ok(clientSummaries);
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
    /// <response code="503">The storage service is temporarily unavailable.</response>
    [HttpGet("historical-usage")]
    [ProducesResponseType(typeof(List<HistoricalUsageResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetHistoricalUsage(
        [FromQuery] TargetType filterType,
        [FromQuery] IdentifierList targetIds,
        [FromQuery] string? clientId,
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] BucketGranularity granularity,
        CancellationToken cancellationToken)
    {
        var historicalUsage = await _statisticsService.GetHistoricalUsageAsync(
            filterType, targetIds, clientId, from, to, granularity, cancellationToken);

        return Ok(historicalUsage);
    }

    /// <summary>
    /// Retrieves historical usage data for one or more services or resource pools split by client.
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
    /// <response code="503">The storage service is temporarily unavailable.</response>
    [HttpGet("historical-usage/by-client")]
    [ProducesResponseType(typeof(List<ClientHistoricalUsageResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetHistoricalUsageByClient(
        [FromQuery] TargetType filterType,
        [FromQuery] IdentifierList targetIds,
        [FromQuery] IdentifierList clientIds,
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] BucketGranularity granularity,
        CancellationToken cancellationToken)
    {
        var clientHistoricalUsage = await _statisticsService.GetHistoricalUsageByClientAsync(
            filterType,
            targetIds,
            clientIds,
            from,
            to,
            granularity,
            cancellationToken);

        return Ok(clientHistoricalUsage);
    }
}
