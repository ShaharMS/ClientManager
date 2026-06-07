using Microsoft.AspNetCore.Http;

namespace ClientManager.Api.Models.Exceptions;

/// <summary>
/// Expected failure when a create or update conflicts with existing data. Mapped to HTTP 409.
/// </summary>
public class ConflictException(string message, string? errorCode = null)
    : HttpProblemException(StatusCodes.Status409Conflict, "Conflict", message, errorCode: errorCode);
