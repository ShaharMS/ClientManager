namespace ClientManager.Api.Models.Exceptions;

/// <summary>
/// Thrown when a client's per-service or global rate limit has been exceeded.
/// </summary>
public class ClientRateLimitExceededException(int? retryAfterSeconds = null) : RateLimitedException("Rate limit exceeded", retryAfterSeconds)
{
}
