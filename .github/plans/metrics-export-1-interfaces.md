# Plan: Multi-Platform Metrics Export — Step 1: Interfaces

> **Status**: 🔲 Not started
> **Prerequisite**: None — this is the first step.
> **Next**: [metrics-export-2-grafana-service.md](metrics-export-2-grafana-service.md)
> **Parent**: [metrics-export-overview.md](metrics-export-overview.md)

## TL;DR

Create the `IGrafanaExportService` interface for JSON-formatted metrics export. The existing `IPrometheusExportService` remains unchanged since its contract is already well-defined.

## Reference Pattern

In [ClientManager.Api/Interfaces/IPrometheusExportService.cs](../../ClientManager.Api/Interfaces/IPrometheusExportService.cs):
- XML documentation on interface and method
- Single async method returning formatted output
- CancellationToken parameter with default value

## Steps

### 1. Create IGrafanaExportService interface

Create `ClientManager.Api/Interfaces/IGrafanaExportService.cs`:

```csharp
namespace ClientManager.Api.Interfaces;

/// <summary>
/// Formats usage statistics in OpenMetrics JSON format for Grafana consumption.
/// </summary>
public interface IGrafanaExportService
{
    /// <summary>
    /// Generates OpenMetrics JSON format metrics.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>JSON-formatted metrics object.</returns>
    Task<object> ExportMetricsAsync(CancellationToken cancellationToken = default);
}
```

### 2. Create metrics response DTOs

Create `ClientManager.Api/Models/Responses/MetricValue.cs` for individual metric data points:

```csharp
namespace ClientManager.Api.Models.Responses;

/// <summary>
/// Represents a single metric value with optional labels.
/// </summary>
public record MetricValue(
    Dictionary<string, string>? Labels,
    double Value
);
```

Create `ClientManager.Api/Models/Responses/MetricDefinition.cs` for metric metadata:

```csharp
namespace ClientManager.Api.Models.Responses;

/// <summary>
/// Represents a metric definition with its values in OpenMetrics JSON format.
/// </summary>
public record MetricDefinition(
    string Name,
    string Type,
    string Help,
    List<MetricValue> Values
);
```

Create `ClientManager.Api/Models/Responses/GrafanaMetricsResponse.cs` for the top-level response:

```csharp
namespace ClientManager.Api.Models.Responses;

/// <summary>
/// Root response object for Grafana OpenMetrics JSON endpoint.
/// </summary>
public record GrafanaMetricsResponse(
    List<MetricDefinition> Metrics
);
```

## Verification

- Project compiles without errors after adding the interface and DTOs
- `IGrafanaExportService` is visible in the `ClientManager.Api.Interfaces` namespace
- All three DTO records are in the `ClientManager.Api.Models.Responses` namespace
- **UI: Not applicable for this step — no UI changes**
