namespace ClientManager.Api.Models.Configuration;

/// <summary>
/// Startup settings for observability exporters (metrics and traces).
/// </summary>
public sealed class ObservabilityOptions
{
    /// <summary>
    /// The configuration section name.
    /// </summary>
    public const string SectionName = "Observability";

    /// <summary>
    /// The absolute OTLP endpoint that traces are exported to. When null or empty,
    /// OTLP trace export is disabled and only in-process instrumentation remains active.
    /// </summary>
    public string? OtlpEndpoint { get; init; }
}
