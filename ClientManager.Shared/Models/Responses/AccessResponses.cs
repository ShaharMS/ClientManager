namespace ClientManager.Shared.Models.Responses;

/// <summary>
/// Response model for checking if a client has access to a service.
/// </summary>
public record AccessCheckResponse
{
    /// <summary>
    /// The ID of the client.
    /// </summary>
    public required string ClientId { get; init; }
    /// <summary>
    /// The ID of the service.
    /// </summary>
    public required string ServiceId { get; init; }
    /// <summary>
    /// The number of remaining requests the client can make.
    /// </summary>
    public int? RemainingRequests { get; init; }
}

/// <summary>
/// A full accessibility report for a client listing every registered service
/// and the client's current access status for each.
/// </summary>
public record ClientAccessibilityResponse
{
    /// <summary>
    /// The unique identifier of the client.
    /// </summary>
    public required string ClientId { get; init; }

    /// <summary>
    /// Per-service accessibility entries.
    /// </summary>
    public IReadOnlyList<ServiceAccessibility> Services { get; init; } = [];
}

/// <summary>
/// Accessibility status for a single client-service pair.
/// Rate limit fields are populated via a read-only peek (counters are not incremented).
/// </summary>
public record ServiceAccessibility
{
    /// <summary>
    /// The unique identifier of the service.
    /// </summary>
    public required string ServiceId { get; init; }

    /// <summary>
    /// Whether the client has an explicit allow entry for this service.
    /// </summary>
    public bool HasAccess { get; init; }

    /// <summary>
    /// Whether the client is currently at or over the rate limit for this service.
    /// </summary>
    public bool IsCurrentlyRateLimited { get; init; }

    /// <summary>
    /// The number of remaining requests before the rate limit is hit, if applicable.
    /// </summary>
    public int? RemainingRequests { get; init; }
}

/// <summary>
/// Response returned when a resource slot is successfully acquired.
/// </summary>
public record ResourceAcquireResponse
{
    /// <summary>
    /// The unique identifier of the allocation, used to release the slot later.
    /// </summary>
    public required string AllocationId { get; init; }

    /// <summary>
    /// The UTC time at which this allocation will expire if not released.
    /// After this time, the background cleanup service will reclaim the slot.
    /// </summary>
    public DateTime ExpiresAt { get; init; }
}

/// <summary>
/// Response returned when a resource allocation release request completes.
/// </summary>
public record ResourceReleaseResponse
{
    /// <summary>
    /// Whether the allocation transitioned from active to released during this request.
    /// </summary>
    public bool Released { get; init; }
}
