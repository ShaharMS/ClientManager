using Asp.Versioning;
using ClientManager.Api.Services.Interfaces;
using ClientManager.Shared.Models.Problems;
using ClientManager.Shared.Models.Requests;
using ClientManager.Shared.Models.Responses;
using ClientManager.Shared.Models.Search;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ClientManager.Api.Controllers;

/// <summary>
/// High-level statistics and per-client summary endpoints.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/statistics")]
[Tags("Statistics")]
public class StatisticsOverviewController(IStatisticsService statisticsService) : ControllerBase
{
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
        var overview = await statisticsService.GetOverviewAsync(cancellationToken);
        return Ok(overview);
    }

    /// <summary>
    /// Searches client configurations and returns paginated summary statistics.
    /// </summary>
    /// <param name="query">Query with filters, sort, and pagination. Pass an empty body or null for all results.</param>
    /// <param name="cancellationToken">Token used to cancel the client statistics search before it completes.</param>
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
        var clientSummaries = await statisticsService.SearchClientsAsync(query ?? DocumentQuery.All, cancellationToken);
        return Ok(clientSummaries);
    }

    /// <summary>
    /// Returns detailed statistics for a specific client, including per-pool active allocation counts.
    /// </summary>
    /// <param name="clientId">The unique identifier of the client.</param>
    /// <param name="cancellationToken">Token used to cancel the client statistics lookup before it completes.</param>
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
        var clientDetails = await statisticsService.GetClientDetailsAsync(clientId, cancellationToken);
        return Ok(clientDetails);
    }

    /// <summary>
    /// Retrieves global usage statistics including request rate and pool acquisition.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the global usage statistics aggregation before it completes.</param>
    /// <returns>Global usage statistics.</returns>
    /// <response code="200">Returns global usage statistics.</response>
    /// <response code="503">The storage service is temporarily unavailable.</response>
    [HttpGet("global-usage")]
    [ProducesResponseType(typeof(GlobalUsageStatsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetGlobalUsageStats(CancellationToken cancellationToken)
    {
        var globalUsage = await statisticsService.GetGlobalUsageStatsAsync(cancellationToken);
        return Ok(globalUsage);
    }

    /// <summary>
    /// Retrieves a paginated summary of all clients with their service and pool access statistics.
    /// </summary>
    /// <param name="paging">Pagination parameters.</param>
    /// <param name="cancellationToken">Token used to cancel the client summaries retrieval before it completes.</param>
    /// <returns>Paginated client summary data for the dashboard table.</returns>
    /// <response code="200">Returns paginated client summaries.</response>
    /// <response code="503">The storage service is temporarily unavailable.</response>
    [HttpGet("client-summaries")]
    [ProducesResponseType(typeof(PagedResponse<ClientSummaryRow>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetClientSummaries([FromQuery] PagedRequest paging, CancellationToken cancellationToken)
    {
        var clientSummaries = await statisticsService.GetClientSummariesAsync(paging, cancellationToken);
        return Ok(clientSummaries);
    }
}
