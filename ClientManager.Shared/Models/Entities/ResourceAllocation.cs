namespace ClientManager.Shared.Models.Entities;

/// <summary>
/// Represents an active allocation of a resource pool slot by a client.
/// Allocations auto-expire after <see cref="ExpiresAt"/> if not explicitly released.
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
