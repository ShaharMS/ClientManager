namespace ClientManager.Api.Models.Responses;

/// <summary>
/// Root response object for Grafana OpenMetrics JSON endpoint.
/// </summary>
/// <param name="Metrics">The list of metric definitions.</param>
public record GrafanaMetricsResponse(
    IReadOnlyList<MetricDefinition> Metrics
);
