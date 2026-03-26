namespace ClientManager.Api.Models.Exceptions;

/// <summary>
/// Thrown when a resource pool has no available slots system-wide.
/// </summary>
public class NoSlotsAvailableException(string resourcePoolId) : RateLimitedException($"No slots available in pool '{resourcePoolId}'")
{
    public string ResourcePoolId { get; } = resourcePoolId;
}
