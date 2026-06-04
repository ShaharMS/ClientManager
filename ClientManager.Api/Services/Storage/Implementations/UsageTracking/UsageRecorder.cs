using ClientManager.Shared.Models.Enums;
using ClientManager.Api.Services.Storage.Models.Entities;
using ClientManager.Api.Services.Storage.Interfaces;

namespace ClientManager.Api.Services.Storage.Implementations.UsageTracking;

/// <summary>
/// Records runtime usage events into the in-memory buffer.
/// </summary>
public class UsageRecorder : IUsageRecorder
{
    private readonly UsageBuffer _buffer;

    public UsageRecorder(UsageBuffer buffer)
    {
        _buffer = buffer;
    }

    /// <inheritdoc />
    public void RecordServiceRequest(string clientId, string serviceId, UsageEventType eventType)
    {
        _buffer.Increment(new UsageBufferKey(clientId, TargetType.Service, serviceId, eventType));
    }

    /// <inheritdoc />
    public void RecordAllocationEvent(string clientId, string resourcePoolId, UsageEventType eventType)
    {
        _buffer.Increment(new UsageBufferKey(clientId, TargetType.ResourcePool, resourcePoolId, eventType));
    }
}