using Microsoft.AspNetCore.Http;

namespace ClientManager.Api.Models.Exceptions;

/// <summary>
/// Thrown when access is denied because the client, service, or policy forbids the request.
/// Mapped to HTTP 403 by the error-handling middleware.
/// </summary>
public class ForbiddenException(string message, string? errorCode = null) : HttpProblemException(StatusCodes.Status403Forbidden, "Forbidden", message, errorCode: errorCode)
{
}
