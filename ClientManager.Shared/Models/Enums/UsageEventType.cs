namespace ClientManager.Shared.Models.Enums;

/// <summary>
/// Classifies the outcome of an access-control or resource-allocation decision so that
/// usage statistics can be recorded and later aggregated by
/// <see cref="TargetType"/> (Service or ResourcePool).
///
/// <para><strong>How the system uses these values</strong></para>
/// <para>
///     Every time a client interacts with a <see cref="TargetType.Service"/> (via
///     <c>AccessControlService.CheckAccessAsync</c>) or a <see cref="TargetType.ResourcePool"/>
///     (via <c>ResourceAllocationService.AcquireAsync</c> / <c>ReleaseAsync</c>), exactly one
///     <see cref="UsageEventType"/> is emitted to the <c>IUsageRecorder</c>. These events are
///     buffered in memory, then periodically flushed to persistent storage by
///     <c>UsagePersistenceService</c>, where they become the <c>GrantedCount</c>,
///     <c>DeniedCount</c>, and <c>ReleasedCount</c> fields on each usage bucket.
/// </para>
///
/// <para><strong>Services vs. Resource Pools - why the distinction matters</strong></para>
/// <para>
///     <see cref="TargetType.Service"/> targets are stateless request/response endpoints (APIs,
///     microservices). The only throttling mechanism is <em>rate limiting</em> - capping how
///     many requests a client (or all clients globally) can make in a time window. A request
///     is either <see cref="Granted"/> or <see cref="Denied"/>; there is nothing to release.
/// </para>
/// <para>
///     <see cref="TargetType.ResourcePool"/> targets are stateful connection-oriented resources
///     (databases, message brokers, streamers). They are governed by <strong>two independent
///     constraints</strong> that serve different purposes:
/// </para>
/// <list type="number">
///     <item>
///         <description>
///             <strong>Slot quotas (concurrency caps)</strong> - limit how many connections can
///             exist <em>at the same time</em>. <see cref="Entities.ResourcePool.MaxSlots"/>
///             sets the system-wide ceiling (e.g. "this database accepts 100 total connections"),
///             while <see cref="Entities.ResourcePoolSettings.MaxSlots"/> in a client's
///             <see cref="Entities.ClientConfiguration.ResourcePools"/> sets a per-client ceiling
///             (e.g. "this client may hold at most 5 connections"). A slot is occupied from
///             <see cref="Granted"/> until <see cref="Released"/> (or until the allocation's TTL
///             expires and is cleaned up).
///         </description>
///     </item>
///     <item>
///         <description>
///             <strong>Rate limits (acquisition-frequency caps)</strong> - limit how <em>often</em>
///             clients can attempt to acquire slots, configured as a
///             <see cref="Entities.GlobalRateLimit"/> with
///             <see cref="TargetType.ResourcePool"/>. This exists to prevent thundering-herd
///             scenarios: even when plenty of slots are available, a burst of hundreds of
///             simultaneous acquire requests could overwhelm the backing resource. The rate limit
///             smooths out that burst without reducing the pool's actual capacity.
///         </description>
///     </item>
/// </list>
/// <para>
///     Because these two constraints are orthogonal, a pool can deny an acquisition for
///     <em>either</em> reason independently: "you already hold too many slots" (quota) or
///     "too many acquire attempts this minute" (rate limit). Both result in a
///     <see cref="Denied"/> event - the distinction is in the exception and metric tag, not the
///     event type.
/// </para>
/// </summary>
public enum UsageEventType
{
    /// <summary>
    /// The access check passed and the requested action was allowed.
    ///
    /// <para><strong>For <see cref="TargetType.Service"/>:</strong>
    ///     The client's request cleared every gate - the client is enabled, the service is
    ///     enabled, the client has an explicit access entry with <c>IsAllowed = true</c>,
    ///     the global service rate limit (shared across all clients) has capacity, and the
    ///     per-client rate limit (service-specific or the client's global rate limit) has
    ///     capacity. The request is forwarded to the service for processing.
    /// </para>
    ///
    /// <para><strong>For <see cref="TargetType.ResourcePool"/>:</strong>
    ///     A slot was successfully allocated. This means the client's per-client quota
    ///     (<see cref="Entities.ResourcePoolSettings.MaxSlots"/>) was not exhausted, the
    ///     global acquisition rate limit for this pool was not exceeded, and the system-wide
    ///     pool capacity (<see cref="Entities.ResourcePool.MaxSlots"/>) still had room. The
    ///     slot is now held by the client until it is either explicitly
    ///     <see cref="Released"/> or it expires after the pool's
    ///     <see cref="Entities.ResourcePool.AllocationTtl"/>.
    /// </para>
    /// </summary>
    Granted,

    /// <summary>
    /// The access check failed and the requested action was blocked.
    ///
    /// <para><strong>For <see cref="TargetType.Service"/>:</strong>
    ///     The request was rejected at one of several stages (checked in this order):
    ///     the client is disabled, the service is disabled, no access entry exists for
    ///     this client–service pair (401 Unauthorized), the entry has <c>IsAllowed = false</c>
    ///     (403 Forbidden), the global service rate limit is exhausted, or the per-client
    ///     rate limit is exhausted (429 Too Many Requests). The first failing check
    ///     short-circuits the pipeline.
    /// </para>
    ///
    /// <para><strong>For <see cref="TargetType.ResourcePool"/>:</strong>
    ///     Slot acquisition was rejected for one of three reasons (checked in this order):
    ///     the client's per-client slot quota for this pool has been reached, the global
    ///     acquisition rate limit for this pool has been exceeded, or the pool's system-wide
    ///     slot capacity is full. All three produce a <see cref="Denied"/> event; the specific
    ///     cause is captured in the associated metric tag and exception type.
    /// </para>
    /// </summary>
    Denied,

    /// <summary>
    /// A previously held resource pool slot was explicitly given back by the client.
    ///
    /// <para>
    ///     This event only applies to <see cref="TargetType.ResourcePool"/>. When a client
    ///     calls <c>ResourceAllocationService.ReleaseAsync</c>, the slot is marked as released
    ///     and becomes immediately available for other clients (or the same client) to acquire.
    /// </para>
    ///
    /// <para>
    ///     Allocations that are <em>not</em> explicitly released will eventually expire based on
    ///     <see cref="Entities.ResourcePool.AllocationTtl"/> and be cleaned up by the background
    ///     <c>AllocationCleanupService</c>. Expired allocations do <strong>not</strong> produce a
    ///     <see cref="Released"/> event - only explicit client-initiated releases do. This means
    ///     the ratio of <see cref="Granted"/> to <see cref="Released"/> events can reveal how
    ///     often clients are relying on TTL expiration instead of cleaning up after themselves.
    /// </para>
    ///
    /// <para>
    ///     This event is never emitted for <see cref="TargetType.Service"/> because service
    ///     interactions are stateless request/response exchanges with nothing to release.
    /// </para>
    /// </summary>
    Released
}
