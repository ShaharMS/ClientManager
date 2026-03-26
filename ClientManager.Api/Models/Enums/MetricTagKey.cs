namespace ClientManager.Api.Models.Enums;

/// <summary>
/// Defines the tag keys used in OpenTelemetry metric instruments,
/// replacing raw string literals with a type-safe enum to avoid typos
/// and ensure consistency across all instrumented services.
/// </summary>
public enum MetricTagKey
{
    ClientId,
    ServiceId,
    ResourcePoolId,
    AllocationId,
    Reason
}
