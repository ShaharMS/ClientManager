using Microsoft.AspNetCore.Http;

namespace ClientManager.Api.Models.Exceptions;

/// <summary>
/// Thrown when a requested entity does not exist in the data store.
/// Mapped to HTTP 404 by the error-handling middleware.
/// </summary>
public class NotFoundException : HttpProblemException
{
    public NotFoundException(string message, string? errorCode = null)
        : base(StatusCodes.Status404NotFound, "Not Found", message, errorCode: errorCode) { }
}
