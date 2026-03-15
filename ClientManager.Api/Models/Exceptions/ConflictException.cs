namespace ClientManager.Api.Models.Exceptions;

public class ConflictException : Exception
{
    public ConflictException(string message) : base(message) { }
}
