namespace ClientManager.Api.Models.Responses;

/// <summary>
/// Represents a single metric value with optional labels.
/// </summary>
/// <param name="Labels">Optional dictionary of label key-value pairs.</param>
/// <param name="Value">The numeric value of the metric.</param>
public record MetricValue(
    Dictionary<string, string>? Labels,
    double Value
);
