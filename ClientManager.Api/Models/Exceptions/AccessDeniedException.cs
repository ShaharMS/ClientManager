namespace ClientManager.Api.Models.Exceptions;

public class AccessDeniedException : Exception
{
    public string ClientId { get; }
    public string ServiceId { get; }

    public AccessDeniedException(string clientId, string serviceId)
        : base($"Client '{clientId}' does not have access to service '{serviceId}'")
    {
        ClientId = clientId;
        ServiceId = serviceId;
    }
}
