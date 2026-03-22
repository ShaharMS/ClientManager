using ClientManager.Api.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ClientManager.Api.Controllers;

/// <summary>
/// Exposes system metrics in Prometheus exposition format for scraping.
/// </summary>
[ApiController]
[Tags("Metrics")]
public class MetricsController : ControllerBase
{
    private readonly IPrometheusExportService _prometheusExportService;

    /// <summary>
    /// Initializes a new instance of <see cref="MetricsController"/>.
    /// </summary>
    /// <param name="prometheusExportService">Service that generates Prometheus metrics text.</param>
    public MetricsController(IPrometheusExportService prometheusExportService)
    {
        _prometheusExportService = prometheusExportService;
    }

    /// <summary>
    /// Returns system metrics in Prometheus exposition format.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Prometheus-formatted metrics text.</returns>
    /// <response code="200">Returns Prometheus exposition format metrics.</response>
    [HttpGet("/metrics")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMetrics(CancellationToken cancellationToken)
    {
        var metrics = await _prometheusExportService.ExportMetricsAsync(cancellationToken);
        return Content(metrics, "text/plain; version=0.0.4; charset=utf-8");
    }
}
