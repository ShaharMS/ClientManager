namespace ClientManager.Api.Models.Responses;

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
    List<MetricValue> Values
);
