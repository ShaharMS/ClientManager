namespace ClientManager.Api.Models.Exceptions;

/// <summary>
/// Thrown when a client's per-pool slot quota has been reached.
/// </summary>
public class ClientSlotLimitReachedException(string resourcePoolId) : RateLimitedException($"Client slot limit reached for pool '{resourcePoolId}'")
{
    public string ResourcePoolId { get; } = resourcePoolId;
}
