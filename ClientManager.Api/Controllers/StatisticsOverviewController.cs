using ClientManager.Api.Services.Interfaces;
using ClientManager.Shared.Models.Responses;
using ClientManager.Shared.Models.Problems;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ClientManager.Api.Controllers;

/// <summary>Dashboard statistics endpoints.</summary>
[ApiController]
[Route("api/v1/statistics")]
[Tags("Statistics")]
public class StatisticsOverviewController(IStatisticsService statisticsService) : ControllerBase
{
    /// <summary>Returns client count, service count, and current RPM.</summary>
    [HttpGet("overview")]
    [ProducesResponseType(typeof(SystemOverviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetOverview(CancellationToken cancellationToken) =>
        Ok(await statisticsService.GetOverviewAsync(cancellationToken));
}
