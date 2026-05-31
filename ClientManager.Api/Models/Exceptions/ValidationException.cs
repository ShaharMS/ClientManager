using Microsoft.AspNetCore.Http;

namespace ClientManager.Api.Models.Exceptions;

/// <summary>
/// Thrown when request input fails validation.
/// Mapped to HTTP 400 by the error-handling middleware.
/// </summary>
public class ValidationException : HttpProblemException
{
    public ValidationException(string message)
        : base(StatusCodes.Status400BadRequest, "Bad Request", message) { }
}
