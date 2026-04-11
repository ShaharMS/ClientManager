namespace ClientManager.Api.Models.Exceptions;

/// <summary>
/// Base type for typed conflict exceptions that map to HTTP 409.
/// </summary>
public abstract class ConflictException : Exception
{
    protected ConflictException(string message) : base(message) { }
}
