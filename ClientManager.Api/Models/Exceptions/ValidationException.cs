namespace ClientManager.Api.Models.Exceptions;

/// <summary>
/// Thrown when request input fails validation.
/// Mapped to HTTP 400 by the error-handling middleware.
/// </summary>
public class ValidationException : Exception
{
    public ValidationException(string message) : base(message) { }
}
