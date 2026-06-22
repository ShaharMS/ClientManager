using ClientManager.Shared.Models.Enums;

namespace ClientManager.Shared.Models.Entities;

/// <summary>
/// Composite key for the in-memory usage buffer.
/// </summary>
/// <param name="ClientId">The client that generated the event.</param>
/// <param name="TargetType">The target scope for the event.</param>
/// <param name="TargetId">The target identifier.</param>
/// <param name="EventType">The recorded event type.</param>
/// <param name="DenialCategory">Required when <paramref name="EventType"/> is <see cref="UsageEventType.Denied"/>.</param>
public record UsageBufferKey(
    string ClientId,
    TargetType TargetType,
    string TargetId,
    UsageEventType EventType,
    UsageDenialCategory? DenialCategory = null);
