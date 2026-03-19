using System.Text.Json.Serialization;

namespace ClientManager.Shared.Models.Enums;

/// <summary>
/// Specifies the type of entity that a global rate limit applies to.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum GlobalRateLimitTarget
{
    /// <summary>
    /// The global rate limit targets a service.
    /// </summary>
    Service,

    /// <summary>
    /// The global rate limit targets a resource pool.
    /// </summary>
    ResourcePool
}
