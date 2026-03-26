namespace ClientManager.Api.Models.Exceptions;

/// <summary>
/// Thrown when a client has no configuration entry for the requested service at all. 
/// Here we are deny-by-default, so the error is mapped to HTTP 401 by the error-handling middleware.
/// </summary>
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