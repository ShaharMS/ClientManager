using Microsoft.AspNetCore.Http;

namespace ClientManager.Api.Models.Exceptions;

/// <summary>
/// Base type for typed conflict exceptions that map to HTTP 409.
/// </summary>
public abstract class ConflictException : HttpProblemException
{
    protected ConflictException(string message)
        : base(StatusCodes.Status409Conflict, "Conflict", message) { }
}
