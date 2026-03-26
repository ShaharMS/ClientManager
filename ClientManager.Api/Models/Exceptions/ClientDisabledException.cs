namespace ClientManager.Api.Models.Exceptions;

/// <summary>
/// Thrown when a request targets a client whose <c>IsEnabled</c> flag is <c>false</c>.
/// Mapped to HTTP 403 by the error-handling middleware.
/// </summary>
public class ClientDisabledException : Exception
{
    public string ClientId { get; }

    public ClientDisabledException(string clientId)
        : base($"Client '{clientId}' is disabled")
    {
        ClientId = clientId;
    }
}
