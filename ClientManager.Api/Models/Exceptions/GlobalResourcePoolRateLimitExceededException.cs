namespace ClientManager.Api.Models.Exceptions;

/// <summary>
/// Thrown when the system-wide aggregate rate limit for a resource pool has been exceeded.
/// </summary>
public class GlobalResourcePoolRateLimitExceededException(int? retryAfterSeconds = null) : RateLimitedException("Global resource pool rate limit exceeded", retryAfterSeconds)
{
}
