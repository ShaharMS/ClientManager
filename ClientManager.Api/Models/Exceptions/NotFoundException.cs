namespace ClientManager.Api.Models.Exceptions;

/// <summary>
/// Thrown when a requested entity does not exist in the data store.
/// Mapped to HTTP 404 by the error-handling middleware.
/// </summary>
public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}
