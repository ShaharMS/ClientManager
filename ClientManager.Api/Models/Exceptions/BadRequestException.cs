using Microsoft.AspNetCore.Http;

namespace ClientManager.Api.Models.Exceptions;

/// <summary>
/// Thrown when request input is invalid or refers to an unknown client on a runtime gatekeeping path.
/// Mapped to HTTP 400 by the error-handling middleware.
/// </summary>
public class BadRequestException(string message, string? errorCode = null) : HttpProblemException(StatusCodes.Status400BadRequest, "Bad Request", message, errorCode: errorCode)
{
}
