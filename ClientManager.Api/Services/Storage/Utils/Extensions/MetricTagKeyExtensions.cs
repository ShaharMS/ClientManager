using ClientManager.Api.Services.Storage.Models.Enums;

namespace ClientManager.Api.Services.Storage.Utils.Extensions;

/// <summary>
/// Converts metric tag enums to their serialized tag names.
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