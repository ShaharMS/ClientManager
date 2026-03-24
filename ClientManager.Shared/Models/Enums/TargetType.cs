using System.Text.Json.Serialization;
using ClientManager.Shared.Models.Entities;

namespace ClientManager.Shared.Models.Enums;

/// <summary>
/// Distinguishes the two fundamental kinds of targets the system manages access to.
///
/// <para>
///     The target type drives which access-control path is taken and which constraints apply.
///     <see cref="Service"/> targets go through <c>AccessControlService</c> and are governed
///     purely by rate limits (request frequency). <see cref="ResourcePool"/> targets go through
///     <c>ResourceAllocationService</c> and are governed by both slot quotas (concurrency) and
///     rate limits (acquisition frequency).
/// </para>
///
/// <para>
///     This enum is used throughout the system - in <see cref="GlobalRateLimit"/> to
///     scope a rate-limit rule, in <see cref="UsageEventType"/> tracking to categorize events,
///     and in usage snapshots to partition statistics.
/// </para>
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TargetType
{
    /// <summary>
    /// A stateless, request/response target - typically an API, microservice, or web endpoint.
    ///
    /// <para>
    ///     Clients send discrete requests and receive responses; they do not hold open
    ///     connections or occupy capacity between requests. The caller does not care which
    ///     instance handles the request, only that the service is reachable.
    /// </para>
    /// <para>
    ///     Throttled exclusively via rate limits: per-client
    ///     (<see cref="ClientRateLimit"/>), per-client-per-service
    ///     (<see cref="ServiceAccessSettings.RateLimit"/>), and global-per-service
    ///     (<see cref="GlobalRateLimit"/>).
    /// </para>
    /// </summary>
    Service,

    /// <summary>
    /// A stateful, connection-oriented target - typically a database, message broker, or
    /// streaming endpoint.
    ///
    /// <para>
    ///     Clients acquire a <em>slot</em> (connection) and hold it for the duration of their
    ///     work. The server must maintain state for each active connection, so both the total
    ///     number of concurrent slots (<see cref="ResourcePool.MaxSlots"/>) and each
    ///     client's share (<see cref="ResourcePoolSettings.MaxSlots"/>) are capped.
    /// </para>
    /// <para>
    ///     In addition to slot quotas, resource pools can also have a
    ///     <see cref="GlobalRateLimit"/> that caps the <em>frequency</em> of
    ///     acquisition attempts - preventing thundering-herd bursts even when slots are
    ///     available.
    /// </para>
    /// </summary>
    ResourcePool
}
