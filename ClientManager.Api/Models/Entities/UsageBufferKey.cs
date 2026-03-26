using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Enums;

namespace ClientManager.Api.Models.Entities;

/// <summary>
/// Composite key identifying a specific usage metric in the in-memory buffer.
/// Each unique combination maps to an independent counter that the persistence
/// service flushes into a <see cref="UsageSnapshot"/> document.
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
