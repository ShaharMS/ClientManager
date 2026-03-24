using ClientManager.Shared.Models.Enums;


namespace ClientManager.Shared.Models.Entities;

/// <summary>
/// Defines a named pool of finite, stateful resources that clients acquire and release
/// (databases, message brokers, streaming endpoints, etc.).
///
/// <para><strong>Capacity model</strong></para>
/// <para>
///     <see cref="MaxSlots"/> is the hard, system-wide ceiling - no more than this many
///     connections may be active at the same time across <em>all</em> clients. Individual
///     clients can be further restricted via <see cref="ResourcePoolSettings.MaxSlots"/> in
///     their <see cref="ClientConfiguration.ResourcePools"/> dictionary (e.g. "this pool
///     allows 100 total connections, but client X may hold at most 10").
/// </para>
///
/// <para><strong>Slot lifecycle</strong></para>
/// <para>
///     A slot is created on a successful acquire, held by the client for the duration of its
///     work, and freed either by an explicit release or automatically after
///     <see cref="AllocationTtl"/> expires. Expired allocations are cleaned up by a background
///     service (<c>AllocationCleanupService</c>) and do not produce a
///     <see cref="UsageEventType.Released"/> usage event.
/// </para>
///
/// <para><strong>Rate limits on top of slot quotas</strong></para>
/// <para>
///     Optionally, a <see cref="GlobalRateLimit"/> with
///     <see cref="TargetType.ResourcePool"/> can be configured to cap the
///     <em>frequency</em> of acquisition attempts, independent of how many slots are free.
///     This prevents thundering-herd bursts from overwhelming the backing resource.
/// </para>
/// </summary>
public record ResourcePool
{
    /// <summary>
    /// Unique identifier for the resource pool.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Human-readable display name, used for error messages and metrics tags.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Maximum number of concurrent slots available system-wide, across all clients.
    /// This is the absolute hard cap - once reached, all further acquisitions are denied
    /// until a slot is released or expires.
    /// </summary>
    public uint MaxSlots { get; init; }

    /// <summary>
    /// Duration after which an unreleased allocation automatically expires. Acts as a
    /// safety net for clients that crash or forget to release - without this, leaked slots
    /// would permanently reduce the pool's usable capacity.
    /// </summary>
    public TimeSpan AllocationTtl { get; init; }

    /// <summary>
    /// UTC timestamp when this resource pool was registered in the system.
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}
