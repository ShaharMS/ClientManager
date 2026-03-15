namespace ClientManager.Api.Models.Exceptions;

public class ClientDisabledException : Exception
{
    public string ClientId { get; }

    public ClientDisabledException(string clientId)
        : base($"Client '{clientId}' is disabled")
    {
        ClientId = clientId;
    }
}
