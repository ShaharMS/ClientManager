using System.Text.Json.Serialization;

namespace ClientManager.Shared.Models.Enums;

/// <summary>
/// Specifies the type of entity that were working with, in the context of quotas/rate limiting.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TargetType
{
    /// <summary>
    /// A rate limit that targets a service.
    /// <para>
    ///     A service is any accessible program, API or resource with which clients 
    ///     don't open and maintain an active connection against, but rather send 
    ///     requests to, and receive responses from. The consumer doesnt mind which 
    ///     instance of the service is responding, as long as it is up and running 
    ///     and can handle the request.
    /// </para>
    /// </summary>
    Service,

    /// <summary>
    /// A quota constraint that targets a resource pool.
    /// <para>
    ///     A resource pool is a logical grouping of resources that clients can 
    ///     connect to and maintain an active, stateful connection against. This 
    ///     usually applies to databases, message brokers, or streamers, where
    ///     client connections need state maintenance and management by the server
    ///     hosting the resource pool.  
    /// </para>
    /// </summary>
    ResourcePool
}
