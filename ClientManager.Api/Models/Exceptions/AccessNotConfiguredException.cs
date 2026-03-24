namespace ClientManager.Api.Models.Exceptions;

public class AccessNotConfiguredException : Exception
{
    public string ClientId { get; }
    public string ServiceId { get; }

    public AccessNotConfiguredException(string clientId, string serviceId)
        : base($"Client '{clientId}' has no access configuration for service '{serviceId}'")
    {
        ClientId = clientId;
        ServiceId = serviceId;
    }
}