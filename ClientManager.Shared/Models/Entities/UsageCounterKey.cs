using ClientManager.Shared.Models.Enums;

namespace ClientManager.Shared.Models.Entities;

/// <summary>
/// Identifies an atomic usage counter for a single event type within a time bucket.
/// </summary>
public record UsageCounterKey(
    string ClientId,
    TargetType TargetType,
    string TargetId,
    BucketGranularity Granularity,
    DateTime BucketTimestamp,
    UsageEventType EventType);
