namespace ClientManager.Shared.Models.Entities;

/// <summary>
/// Per-client quota for a resource pool, stored in
/// <see cref="ClientConfiguration.ResourcePools"/> keyed by pool ID.
///
/// <para>
///     This caps how many slots <em>one specific client</em> can hold at the same time,
///     as opposed to <see cref="ResourcePool.MaxSlots"/> which caps the pool system-wide.
///     For example, a pool might allow 100 total connections but limit any single client
///     to 10 so that one greedy consumer cannot starve the others.
/// </para>
/// </summary>
public record ResourcePoolSettings
{
    /// <summary>
    /// Maximum number of concurrent slots this client may hold in the pool.
    /// Must be ≤ the pool's <see cref="ResourcePool.MaxSlots"/>; otherwise the
    /// system-wide cap would always be hit first.
    /// </summary>
    public uint MaxSlots { get; init; }
}
