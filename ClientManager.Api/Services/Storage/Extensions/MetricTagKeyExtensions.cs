using ClientManager.Shared.Models.Enums;

namespace ClientManager.Api.Services.Storage.Extensions;

/// <summary>
/// Converts metric tag enums to their serialized tag names.
/// </summary>
public static class MetricTagKeyExtensions
{
    public static string ToTagName(this MetricTagKey key) => key switch
    {
        MetricTagKey.ClientId => "clientId",
        MetricTagKey.ServiceId => "serviceId",
        MetricTagKey.Reason => "reason",
        _ => key.ToString()
    };
}