namespace ClientManager.Shared.Models.Entities;

/// <summary>
/// Represents one active (or formerly active) slot held by a client in a resource pool.
///
/// <para>
///     Created when a resource aquisition attempt succeeds. Freed in one of
///     two ways: the client explicitly releases the resource (and emits
///     a <see cref="Enums.UsageEventType.Released"/> event), or the allocation outlives its
///     <see cref="ExpiresAt"/> and is cleaned up by a cleanup service (no
///     Released event emitted - the slot is silently reclaimed).
/// </para>
/// </summary>
public record ResourceAllocation
{
    /// <summary>
    /// Unique identifier for this allocation.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// ID of the resource pool this slot belongs to.
    /// </summary>
    public required string ResourcePoolId { get; init; }

    /// <summary>
    /// ID of the client holding this slot.
    /// </summary>
    public required string ClientId { get; init; }

    /// <summary>
    /// UTC timestamp when this slot was acquired.
    /// </summary>
    public DateTime AcquiredAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// UTC timestamp when this allocation expires if not released.
    /// </summary>
    public DateTime ExpiresAt { get; init; }

    /// <summary>
    /// Whether this allocation has been explicitly released.
    /// </summary>
    public bool IsReleased { get; init; } = false;
}
