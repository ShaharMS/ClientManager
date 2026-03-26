using ClientManager.Api.Models.Enums;
using System.Diagnostics;

namespace ClientManager.Api.Utils.Extensions;

/// <summary>
/// Extension methods for converting <see cref="MetricTagKey"/> to the camelCase
/// string used in <see cref="TagList"/> entries.
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

