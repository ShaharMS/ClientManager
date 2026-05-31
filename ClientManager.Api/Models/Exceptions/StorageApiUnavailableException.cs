using Microsoft.AspNetCore.Http;

namespace ClientManager.Api.Models.Exceptions;

/// <summary>
/// Thrown when the internal storage-facing API is unavailable or short-circuited.
/// Mapped to HTTP 503 by the error-handling middleware.
/// </summary>
public class StorageApiUnavailableException : HttpProblemException
{
    public StorageApiUnavailableException(
        string message,
        int? retryAfterSeconds = null,
        Exception? innerException = null)
        : base(StatusCodes.Status503ServiceUnavailable, "Service Unavailable", message, retryAfterSeconds, innerException)
    {
    }
}