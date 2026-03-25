namespace ClientManager.Api.Services.Instrumentation;

/// <summary>
/// Defines the tag keys used in OpenTelemetry metric instruments,
/// replacing raw string literals with a type-safe enum to avoid typos.
/// </summary>
public enum MetricTagKey
{
    ClientId,
    ServiceId,
    ResourcePoolId,
    AllocationId,
    Reason
}

/// <summary>
/// Extension methods for converting <see cref="MetricTagKey"/> to the camelCase
/// string used in <see cref="System.Diagnostics.TagList"/> entries.
/// </summary>
public static class MetricTagKeyExtensions
{
    public static string ToTagName(this MetricTagKey key) => key switch
    {
        MetricTagKey.ClientId => "clientId",
        MetricTagKey.ServiceId => "serviceId",
        MetricTagKey.ResourcePoolId => "resourcePoolId",
        MetricTagKey.AllocationId => "allocationId",
        MetricTagKey.Reason => "reason",
        _ => key.ToString()
    };
}
