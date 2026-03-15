namespace ClientManager.Shared.Models.Entities;

/// <summary>
/// Defines a named pool of finite resources with a system-wide slot limit.
/// Individual clients may have lower caps via <see cref="ResourcePoolSettings.MaxSlots"/>.
/// </summary>
public record ResourcePool
{
    /// <summary>
    /// Unique identifier for the resource pool.
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Human-readable display name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// System-wide maximum number of concurrent allocations.
    /// </summary>
    public uint MaxSlots { get; init; }

    /// <summary>
    /// Duration after which unreleased allocations automatically expire.
    /// </summary>
    public TimeSpan AllocationTtl { get; init; }

    /// <summary>
    /// UTC timestamp when this resource pool was created.
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}
