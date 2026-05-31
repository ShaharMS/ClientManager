using Asp.Versioning;
using ClientManager.Shared.Models.Responses;
using ClientManager.Shared.Models.Problems;
using ClientManager.Api.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ClientManager.Api.Controllers;

/// <summary>
/// Exposes system metrics in multiple formats for different monitoring platforms.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/metrics")]
[Tags("Metrics")]
public class MetricsController : ControllerBase
{
    private readonly IMetricsService _metricsService;

    /// <summary>
    /// Initializes a new instance of <see cref="MetricsController"/>.
    /// </summary>
    /// <param name="metricsService">The metrics read service.</param>
    public MetricsController(IMetricsService metricsService)
    {
        _metricsService = metricsService;
    }

    /// <summary>
    /// Returns system metrics in Prometheus exposition format.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Prometheus-formatted metrics text.</returns>
    /// <response code="200">Returns Prometheus exposition format metrics.</response>
    /// <response code="503">The storage service is temporarily unavailable.</response>
    [HttpGet("prometheus")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetPrometheusMetrics(CancellationToken cancellationToken)
    {
        var metrics = await _metricsService.GetPrometheusMetricsAsync(cancellationToken);
        return Content(metrics, "text/plain; version=0.0.4; charset=utf-8");
    }

    /// <summary>
    /// Returns system metrics in OpenMetrics JSON format for Grafana.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>JSON object containing all metrics with labels.</returns>
    /// <response code="200">Returns OpenMetrics JSON format metrics.</response>
    /// <response code="503">The storage service is temporarily unavailable.</response>
    [HttpGet("grafana")]
    [ProducesResponseType(typeof(GrafanaMetricsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetGrafanaMetrics(CancellationToken cancellationToken)
    {
        var metrics = await _metricsService.GetGrafanaMetricsAsync(cancellationToken);
        return Ok(metrics);
    }
}
