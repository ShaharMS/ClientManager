using Asp.Versioning;
using ClientManager.Shared.Models.Responses;
using ClientManager.StorageApi.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ClientManager.StorageApi.Controllers;

/// <summary>
/// Exposes storage-side metrics payloads for the public API proxy layer.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("internal/v{version:apiVersion}/metrics")]
[Tags("Metrics Reads")]
public class MetricsReadController : ControllerBase
{
    private readonly IPrometheusExportService _prometheusExportService;
    private readonly IGrafanaExportService _grafanaExportService;

    /// <summary>
    /// Initializes a new instance of <see cref="MetricsReadController"/>.
    /// </summary>
    public MetricsReadController(
        IPrometheusExportService prometheusExportService,
        IGrafanaExportService grafanaExportService)
    {
        _prometheusExportService = prometheusExportService;
        _grafanaExportService = grafanaExportService;
    }

    /// <summary>
    /// Returns Prometheus exposition text.
    /// </summary>
    [HttpGet("prometheus")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPrometheusMetrics(CancellationToken cancellationToken)
    {
        var metrics = await _prometheusExportService.ExportMetricsAsync(cancellationToken);
        return Content(metrics, "text/plain; version=0.0.4; charset=utf-8");
    }

    /// <summary>
    /// Returns Grafana JSON metrics.
    /// </summary>
    [HttpGet("grafana")]
    [ProducesResponseType(typeof(GrafanaMetricsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetGrafanaMetrics(CancellationToken cancellationToken)
    {
        return Ok(await _grafanaExportService.ExportMetricsAsync(cancellationToken));
    }
}