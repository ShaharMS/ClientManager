namespace ClientManager.Shared.Models.Entities;

/// <summary>
/// Represents one active (or formerly active) slot held by a client in a resource pool.
///
/// <para>
///     Created when <c>ResourceAllocationService.AcquireAsync</c> succeeds. Freed in one of
///     two ways: the client calls <c>ReleaseAsync</c> (sets <see cref="IsReleased"/> and emits
///     a <see cref="Enums.UsageEventType.Released"/> event), or the allocation outlives its
///     <see cref="ExpiresAt"/> and is cleaned up by <c>AllocationCleanupService</c> (no
///     Released event emitted - the slot is silently reclaimed).
/// </para>
/// </summary>
public record ResourceAllocation
{
    /// <summary>
    /// Unique identifier for this allocation.
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// ID of the resource pool this slot belongs to.
    /// </summary>
    public string ResourcePoolId { get; init; } = string.Empty;

    /// <summary>
    /// ID of the client holding this slot.
    /// </summary>
    public string ClientId { get; init; } = string.Empty;

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
