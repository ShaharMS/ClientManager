using ClientManager.Api.Interfaces;
using ClientManager.Shared.Models.Enums;

namespace ClientManager.Api.Services.UsageTracking;

/// <summary>
/// Records usage events by incrementing counters in a <see cref="UsageBuffer"/>.
/// </summary>
public class UsageRecorder : IUsageRecorder
{
    private readonly UsageBuffer _buffer;

    /// <summary>
    /// Initializes a new instance of <see cref="UsageRecorder"/>.
    /// </summary>
    /// <param name="buffer">The shared in-memory usage buffer.</param>
    public UsageRecorder(UsageBuffer buffer)
    {
        _buffer = buffer;
    }

    /// <inheritdoc />
    public void RecordServiceRequest(string clientId, string serviceId, UsageEventType eventType)
    {
        var key = new UsageBufferKey(clientId, TargetType.Service, serviceId, eventType);
        _buffer.Increment(key);
    }

    /// <inheritdoc />
    public void RecordAllocationEvent(string clientId, string resourcePoolId, UsageEventType eventType)
    {
        var key = new UsageBufferKey(clientId, TargetType.ResourcePool, resourcePoolId, eventType);
        _buffer.Increment(key);
    }
}
