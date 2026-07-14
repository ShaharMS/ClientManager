using ClientManager.Api.Services.Interfaces;
using ClientManager.Shared.Models.Responses;
using ClientManager.Shared.Models.Problems;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ClientManager.Api.Controllers;

/// <summary>
/// Dashboard statistics endpoints.
/// </summary>
/// <remarks>
/// <para>
/// These read-only endpoints aggregate lightweight counters for the Admin UI dashboard cards:
/// total clients, total services, and current requests per minute.
/// </para>
/// <para>
/// Request-per-minute values come from the shared in-storage RPM ring (a five-minute average).
/// </para>
/// </remarks>
[ApiController]
[Route("api/v2/statistics")]
[Tags("Statistics")]
public class StatisticsOverviewController(IStatisticsService statisticsService) : ControllerBase
{
    /// <summary>
    /// Returns system overview counts and the current requests-per-minute gauge.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the overview query before it completes.</param>
    /// <returns>Client count, service count, and current RPM.</returns>
    /// <response code="200">Returns the dashboard overview.</response>
    /// <response code="503">The storage service is temporarily unavailable.</response>
    [HttpGet("overview")]
    [ProducesResponseType(typeof(SystemOverviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetOverview(CancellationToken cancellationToken) =>
        Ok(await statisticsService.GetOverviewAsync(cancellationToken));
}
