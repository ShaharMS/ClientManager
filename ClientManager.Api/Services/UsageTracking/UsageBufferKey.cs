using ClientManager.Shared.Models.Enums;

namespace ClientManager.Api.Services.UsageTracking;

/// <summary>
/// Composite key identifying a specific usage metric in the in-memory buffer.
/// </summary>
/// <param name="ClientId">The client that generated the event.</param>
/// <param name="TargetType">Whether the target is a Service or ResourcePool.</param>
/// <param name="TargetId">The identifier of the service or resource pool.</param>
/// <param name="EventType">Whether the event was granted or denied.</param>
public record UsageBufferKey(
    string ClientId,
    TargetType TargetType,
    string TargetId,
    UsageEventType EventType);
