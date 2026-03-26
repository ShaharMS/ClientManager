namespace ClientManager.Api.Models.Exceptions;

/// <summary>
/// Thrown when a client configuration cannot be found by its identifier.
/// </summary>
public class ClientNotFoundException(string clientId) : NotFoundException($"Client '{clientId}' not found")
{
    public string ClientId { get; } = clientId;
}
