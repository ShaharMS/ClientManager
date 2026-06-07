using Microsoft.AspNetCore.Http;

namespace ClientManager.Api.Models.Exceptions;

/// <summary>
/// Thrown when a request is denied because the applicable rate limit has been exceeded.
/// The middleware sets the <c>Retry-After</c> header when <see cref="HttpProblemException.RetryAfterSeconds"/> is present.
/// Mapped to HTTP 429 by the error-handling middleware.
/// </summary>
public class RateLimitedException : HttpProblemException
{
    public RateLimitedException(string message, int? retryAfterSeconds = null, string? errorCode = null)
        : base(StatusCodes.Status429TooManyRequests, "Too Many Requests", message, retryAfterSeconds, errorCode: errorCode)
    {
    }
}
