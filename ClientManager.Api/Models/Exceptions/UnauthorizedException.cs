using Microsoft.AspNetCore.Http;

namespace ClientManager.Api.Models.Exceptions;

/// <summary>
/// Thrown when a client has no access configuration for the requested service.
/// Mapped to HTTP 401 by the error-handling middleware.
/// </summary>
public class UnauthorizedException(string message, string? errorCode = null) : HttpProblemException(StatusCodes.Status401Unauthorized, "Unauthorized", message, errorCode: errorCode)
{
}
