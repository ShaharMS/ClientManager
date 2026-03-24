using Asp.Versioning;
using ClientManager.Api.Interfaces;
using ClientManager.Api.Models.Responses;
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
    private readonly IPrometheusExportService _prometheusExportService;
    private readonly IGrafanaExportService _grafanaExportService;

    /// <summary>
    /// Initializes a new instance of <see cref="MetricsController"/>.
    /// </summary>
    /// <param name="prometheusExportService">Service that generates Prometheus metrics text.</param>
    /// <param name="grafanaExportService">Service that generates Grafana JSON metrics.</param>
    public MetricsController(
        IPrometheusExportService prometheusExportService,
        IGrafanaExportService grafanaExportService)
    {
        _prometheusExportService = prometheusExportService;
        _grafanaExportService = grafanaExportService;
    }

    /// <summary>
    /// Returns system metrics in Prometheus exposition format.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Prometheus-formatted metrics text.</returns>
    /// <response code="200">Returns Prometheus exposition format metrics.</response>
    [HttpGet("prometheus")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPrometheusMetrics(CancellationToken cancellationToken)
    {
        var metrics = await _prometheusExportService.ExportMetricsAsync(cancellationToken);
        return Content(metrics, "text/plain; version=0.0.4; charset=utf-8");
    }

    /// <summary>
    /// Returns system metrics in OpenMetrics JSON format for Grafana.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>JSON object containing all metrics with labels.</returns>
    /// <response code="200">Returns OpenMetrics JSON format metrics.</response>
    [HttpGet("grafana")]
    [ProducesResponseType(typeof(GrafanaMetricsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetGrafanaMetrics(CancellationToken cancellationToken)
    {
        var metrics = await _grafanaExportService.ExportMetricsAsync(cancellationToken);
        return Ok(metrics);
    }
}
