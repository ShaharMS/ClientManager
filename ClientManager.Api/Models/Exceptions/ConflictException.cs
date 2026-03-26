namespace ClientManager.Api.Models.Exceptions;

/// <summary>
/// Thrown when a create operation would violate a uniqueness constraint
/// (e.g., duplicate client ID or duplicate global rate limit for a target).
/// Mapped to HTTP 409 by the error-handling middleware.
/// </summary>
public class ConflictException : Exception
{
    public ConflictException(string message) : base(message) { }
}
