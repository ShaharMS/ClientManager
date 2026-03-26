namespace ClientManager.Api.Models.Exceptions;

/// <summary>
/// Thrown when the system-wide aggregate rate limit for a service has been exceeded.
/// </summary>
public class GlobalServiceRateLimitExceededException(int? retryAfterSeconds = null) : RateLimitedException("Global service rate limit exceeded", retryAfterSeconds)
{
}
