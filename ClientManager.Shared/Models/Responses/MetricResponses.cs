namespace ClientManager.Shared.Models.Responses;

/// <summary>
/// Root response object for Grafana OpenMetrics JSON endpoint.
/// </summary>
/// <param name="Metrics">The list of metric definitions.</param>
public record GrafanaMetricsResponse(
    IReadOnlyList<MetricDefinition> Metrics);

/// <summary>
/// Represents a metric definition with its values in OpenMetrics JSON format.
/// </summary>
/// <param name="Name">The name of the metric.</param>
/// <param name="Type">The metric type (e.g., gauge, counter).</param>
/// <param name="Help">A description of what the metric measures.</param>
/// <param name="Values">The list of metric values with their labels.</param>
public record MetricDefinition(
    string Name,
    string Type,
    string Help,
    IReadOnlyList<MetricValue> Values);

/// <summary>
/// Represents a single metric value with optional labels.
/// </summary>
/// <param name="Labels">Optional dictionary of label key-value pairs.</param>
/// <param name="Value">The numeric value of the metric.</param>
public record MetricValue(
    Dictionary<string, string>? Labels,
    double Value);
