using ClientManager.Shared.Models.Enums;

namespace ClientManager.Api.Services.Interfaces;

/// <summary>
/// Records usage events (requests, allocations) for historical tracking.
/// </summary>
public interface IUsageRecorder
{
    /// <summary>
    /// Records a service request event (granted or denied) for a client.
    /// </summary>
    void RecordServiceRequest(string clientId, string serviceId, UsageEventType eventType);

    /// <summary>
    /// Records a resource pool allocation event (granted or denied) for a client.
    /// </summary>
    void RecordAllocationEvent(string clientId, string resourcePoolId, UsageEventType eventType);
}
