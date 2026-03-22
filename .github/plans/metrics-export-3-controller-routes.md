# Plan: Multi-Platform Metrics Export — Step 3: Controller Routes

> **Status**: 🔲 Not started
> **Prerequisite**: [metrics-export-2-grafana-service.md](metrics-export-2-grafana-service.md)
> **Next**: None — this is the final step.
> **Parent**: [metrics-export-overview.md](metrics-export-overview.md)

## TL;DR

Restructure `MetricsController` to expose two platform-specific routes (`/prometheus`, `/grafana`) and remove the generic `/metrics` endpoint. Update Swagger documentation accordingly.

## Reference Pattern

In [ClientManager.Api/Controllers/MetricsController.cs](../../ClientManager.Api/Controllers/MetricsController.cs):
- XML documentation on controller and action methods
- `[ApiController]`, `[Tags]` attributes on controller
- `[HttpGet]`, `[ProducesResponseType]` attributes on action methods
- Inject services via constructor

## Steps

### 1. Update MetricsController with both services

Replace the controller to inject both export services and expose platform routes:

```csharp
using ClientManager.Api.Interfaces;
using ClientManager.Api.Models.Responses;
using Microsoft.AspNetCore.Mvc;

namespace ClientManager.Api.Controllers;

/// <summary>
/// Exposes system metrics in multiple formats for different monitoring platforms.
/// </summary>
[ApiController]
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
    [HttpGet("/prometheus")]
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
    [HttpGet("/grafana")]
    [ProducesResponseType(typeof(GrafanaMetricsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetGrafanaMetrics(CancellationToken cancellationToken)
    {
        var metrics = await _grafanaExportService.ExportMetricsAsync(cancellationToken);
        return Ok(metrics);
    }
}
```

### 2. Verify OpenTelemetry Prometheus endpoint is separate

The built-in OpenTelemetry Prometheus scraping endpoint at `/metrics` (from `app.MapPrometheusScrapingEndpoint("/metrics")` in `Program.cs`) is independent of this controller. Confirm it can remain as-is or update the path if desired.

If the OpenTelemetry endpoint should also move to `/prometheus`, update `Program.cs`:

```csharp
// Before:
app.MapPrometheusScrapingEndpoint("/metrics");

// After:
app.MapPrometheusScrapingEndpoint("/prometheus/otel");
```

This keeps OpenTelemetry auto-instrumented metrics at a separate sub-path.

### 3. Update any HTTP test files

If `ClientManager.Api.http` or similar test files reference `/metrics`, update them to use `/prometheus` or `/grafana`.

## Verification

- Project compiles without errors
- `/prometheus` endpoint returns `text/plain; version=0.0.4` with Prometheus format
- `/grafana` endpoint returns `application/json` with the `GrafanaMetricsResponse` structure
- `/metrics` no longer handled by `MetricsController` (may still be handled by OpenTelemetry middleware)
- Swagger UI shows both endpoints under the "Metrics" tag with proper documentation
- **UI: Navigate to Swagger at `/swagger` — verify both `/prometheus` and `/grafana` endpoints appear with correct response schemas**
- **UI: Try each endpoint in Swagger — `/prometheus` returns plain text, `/grafana` returns JSON with metrics array**
