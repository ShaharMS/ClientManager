namespace ClientManager.Api.Models.Exceptions;

/// <summary>
/// Thrown when a request is denied because the applicable rate limit has been exceeded.
/// The middleware sets the <c>Retry-After</c> header when <see cref="RetryAfterSeconds"/> is present.
/// Mapped to HTTP 429 by the error-handling middleware.
/// </summary>
public class RateLimitedException : Exception
{
    public int? RetryAfterSeconds { get; }

    public RateLimitedException(string message, int? retryAfterSeconds = null)
        : base(message)
    {
        RetryAfterSeconds = retryAfterSeconds;
    }
}
