using Asp.Versioning;
using ClientManager.Api.Services.Interfaces;
using ClientManager.Shared.Models.Problems;
using ClientManager.Shared.Models.Requests;
using ClientManager.Shared.Models.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ClientManager.Api.Controllers;

/// <summary>
/// Dashboard statistics endpoints.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/statistics")]
[Tags("Statistics")]
public class StatisticsOverviewController(IStatisticsService statisticsService) : ControllerBase
{
    /// <summary>
    /// Returns system overview counts and live usage gauges.
    /// </summary>
    [HttpGet("overview")]
    [ProducesResponseType(typeof(SystemOverviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetOverview(CancellationToken cancellationToken)
    {
        var overview = await statisticsService.GetOverviewAsync(cancellationToken);
        return Ok(overview);
    }

    /// <summary>
    /// Searches precomputed usage timeseries and returns chart-ready buckets.
    /// </summary>
    [HttpPost("timeseries/search")]
    [ProducesResponseType(typeof(TimeseriesSearchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> SearchTimeseries(
        [FromBody] TimeseriesSearchRequest request,
        CancellationToken cancellationToken)
    {
        var response = await statisticsService.SearchTimeseriesAsync(request, cancellationToken);
        return Ok(response);
    }
}
