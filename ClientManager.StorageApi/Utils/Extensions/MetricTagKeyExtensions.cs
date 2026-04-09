using ClientManager.StorageApi.Models.Enums;

namespace ClientManager.StorageApi.Utils.Extensions;

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